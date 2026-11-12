using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Smart_X_Ecosystem.Models;

namespace Smart_X_Ecosystem.Services
{
    public class CommandStreamService
    {
        private readonly MeshTopologyService _topologyService;
        private readonly object _syncLock = new();
        private long _sequenceCounter = 0;

        public event Action? OnStreamUpdated;

        // 1. Typed FIFO Queue<QueuedTelemetryPacket> (carries Device, Value, Unit, Timestamp)
        public Queue<QueuedTelemetryPacket> TelemetryQueue { get; } = new();

        // 2. Typed PriorityQueue<QueuedTelemetryPacket, int> (emptied first by BackgroundService)
        public PriorityQueue<QueuedTelemetryPacket, int> CriticalAlertQueue { get; } = new();

        // 3. LIFO Stack<OverrideCommand> for manual override undo history
        public Stack<OverrideCommand> CommandUndoStack { get; } = new();

        // 4. Dictionary<string, DeviceState> keyed on MAC address for O(1) lookups
        public Dictionary<string, DeviceState> DeviceRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

        // 5. SortedDictionary<TimelineKey, TimelineEntry> keyed by (Timestamp + Sequence) storing full chartable readings
        public SortedDictionary<TimelineKey, TimelineEntry> SensorTimeline { get; } = new();

        // 6. Unique Sets: DisconnectedNodes AND ActiveErrorStates
        public HashSet<string> DisconnectedNodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ActiveErrorStates { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Hashtable HardwareMetadataTable { get; } = new()
        {
            { "00:1B:44", "Espressif Systems (ESP32-WROOM-32D)" },
            { "FIRMWARE", "v3.0.0-RTOS" }
        };

        public SortedSet<string> ActiveCategoriesSet { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "Hydroponics", "Power", "HVAC"
        };

        // Predictive Action Engine Observation Counters (for purely count-based confidence)
        public List<string> RecordedUserHistory { get; } = new();
        private readonly Dictionary<string, int> _queryFrequency = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, int>> _actionCoOccurrence = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _deviceTotalPackets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _deviceFaultPackets = new(StringComparer.OrdinalIgnoreCase);
        private string _lastTriggerEvent = string.Empty;

        public string LastBackgroundProcessedSummary { get; private set; } = "Background worker standing by...";
        public int TotalPacketsProcessedByWorker { get; private set; }

        public CommandStreamService(MeshTopologyService topologyService)
        {
            _topologyService = topologyService;
            SeedInitialDevices();
        }

        private void SeedInitialDevices()
        {
            var initialNodes = new[]
            {
                new DeviceState { MacAddress = "00:1B:44:11:3A:B7", DeviceName = "Moisture Sensor B", Category = "Hydroponics", Status = "Online", LastReading = 42.5, MaxGaugeValue = 100.0, Unit = "%", IsRelayActive = false },
                new DeviceState { MacAddress = "00:1B:44:11:3A:B8", DeviceName = "Pump A", Category = "Hydroponics", Status = "Online", LastReading = 120.0, MaxGaugeValue = 200.0, Unit = "L/hr", IsRelayActive = true },
                new DeviceState { MacAddress = "00:1B:44:11:3A:C1", DeviceName = "Smart Relay Grid 1", Category = "Power", Status = "Online", LastReading = 230.4, MaxGaugeValue = 500.0, Unit = "V", IsRelayActive = true },
                new DeviceState { MacAddress = "00:1B:44:11:3A:C2", DeviceName = "Substation Meter X", Category = "Power", Status = "Alert", LastReading = 415.8, MaxGaugeValue = 500.0, Unit = "V", IsRelayActive = true },
                new DeviceState { MacAddress = "00:1B:44:11:3A:D9", DeviceName = "Greenhouse HVAC", Category = "HVAC", Status = "Online", LastReading = 24.0, MaxGaugeValue = 50.0, Unit = "°C", IsRelayActive = true }
            };

            // Seed baseline observation counts so initial confidence ratios are derived strictly from counts
            foreach (var node in initialNodes)
            {
                DeviceRegistry[node.MacAddress] = node;
                _deviceTotalPackets[node.MacAddress] = 5;
                _deviceFaultPackets[node.MacAddress] = node.Status == "Alert" ? 3 : 0;

                AddTimelineReading(new TimelineEntry
                {
                    MacAddress = node.MacAddress,
                    DeviceName = node.DeviceName,
                    Category = node.Category,
                    Value = node.LastReading,
                    Unit = node.Unit,
                    EventType = "INIT",
                    Summary = $"Registered {node.DeviceName} at {node.LastReading}{node.Unit}",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Seed historical co-occurrence counts (e.g., 4 out of 5 historical moisture drops were followed by Pump A activation)
            _actionCoOccurrence["Moisture Sensor B < 20%"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Pump A", 4 },
                { "Greenhouse HVAC", 1 }
            };

            DisconnectedNodes.Add("00:1B:44:11:3A:C2");
            ActiveErrorStates.Add("ERR_OVERVOLTAGE:00:1B:44:11:3A:C2");

            CriticalAlertQueue.Enqueue(new QueuedTelemetryPacket
            {
                MacAddress = "00:1B:44:11:3A:C2",
                DeviceName = "Substation Meter X",
                Category = "Power",
                Value = 415.8,
                Unit = "V",
                Priority = 1,
                IsCritical = true,
                EventType = "CRITICAL_ALERT",
                Timestamp = DateTime.UtcNow
            }, 1);
        }

        public void AddTimelineReading(TimelineEntry entry)
        {
            lock (_syncLock)
            {
                long seq = Interlocked.Increment(ref _sequenceCounter);
                var key = new TimelineKey(entry.Timestamp, seq);
                SensorTimeline[key] = entry;
            }
        }

        public int ImportDevicesFromText(string fileContent)
        {
            if (string.IsNullOrWhiteSpace(fileContent)) return 0;

            int importedCount = 0;
            var lines = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    string mac = parts[0].Trim();
                    string name = parts[1].Trim();
                    string category = parts[2].Trim();
                    double reading = parts.Length >= 4 && double.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double r) ? r : 50.0;
                    string unit = parts.Length >= 5 ? parts[4].Trim() : "units";

                    var device = new DeviceState
                    {
                        MacAddress = mac,
                        DeviceName = name,
                        Category = category,
                        LastReading = reading,
                        MaxGaugeValue = category.Equals("Power", StringComparison.OrdinalIgnoreCase) ? 500.0 : 100.0,
                        Unit = unit,
                        Status = "Online",
                        IsRelayActive = true,
                        LastUpdated = DateTime.UtcNow
                    };

                    lock (_syncLock)
                    {
                        DeviceRegistry[mac] = device;
                        ActiveCategoriesSet.Add(category);
                        _deviceTotalPackets[mac] = _deviceTotalPackets.GetValueOrDefault(mac, 0) + 1;

                        TelemetryQueue.Enqueue(new QueuedTelemetryPacket
                        {
                            MacAddress = mac,
                            DeviceName = name,
                            Category = category,
                            Value = reading,
                            Unit = unit,
                            Priority = 2,
                            IsCritical = false,
                            EventType = "BATCH_IMPORT",
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    _topologyService.RecordLiveTelemetryInAvlTree(device);
                    importedCount++;
                }
            }

            NotifyStateChanged();
            return importedCount;
        }

        public string IngestDeviceTelemetry(TelemetrySimulationRequest request)
        {
            DeviceState device;
            lock (_syncLock)
            {
                if (!DeviceRegistry.TryGetValue(request.MacAddress, out device!))
                {
                    device = new DeviceState
                    {
                        MacAddress = request.MacAddress,
                        DeviceName = $"ESP32-Node-{request.MacAddress[^2..]}",
                        Category = "Power",
                        MaxGaugeValue = 500.0,
                        Unit = "V"
                    };
                    DeviceRegistry[request.MacAddress] = device;
                }

                _deviceTotalPackets[device.MacAddress] = _deviceTotalPackets.GetValueOrDefault(device.MacAddress, 0) + 1;
            }

            var packet = new QueuedTelemetryPacket
            {
                MacAddress = device.MacAddress,
                DeviceName = device.DeviceName,
                Category = device.Category,
                Value = request.Reading,
                Unit = device.Unit,
                Timestamp = DateTime.UtcNow
            };

            if (request.SimulateDisconnect)
            {
                lock (_syncLock)
                {
                    _deviceFaultPackets[device.MacAddress] = _deviceFaultPackets.GetValueOrDefault(device.MacAddress, 0) + 1;
                    bool isNewDisconnect = DisconnectedNodes.Add(device.MacAddress);
                    ActiveErrorStates.Add($"ERR_NODE_OFFLINE:{device.MacAddress}");

                    if (!isNewDisconnect)
                    {
                        NotifyStateChanged();
                        return "Duplicate disconnect suppressed by DisconnectedNodes (HashSet<string>) — no duplicate packet queued.";
                    }

                    packet.Priority = 1;
                    packet.IsCritical = true;
                    packet.EventType = "DISCONNECT_FAULT";
                    CriticalAlertQueue.Enqueue(packet, 1);
                }

                _topologyService.SetNodeOnlineState(device.MacAddress, false);
                NotifyStateChanged();
                return "Typed disconnect packet enqueued in CriticalAlertQueue (Priority 1) & error added to ActiveErrorStates.";
            }

            bool isMoistureDrop = device.Category.Equals("Hydroponics", StringComparison.OrdinalIgnoreCase) && request.Reading < 20.0;
            bool isVoltageSpike = device.Category.Equals("Power", StringComparison.OrdinalIgnoreCase) && request.Reading > 380.0;

            if (request.IsCriticalAlert || isMoistureDrop || isVoltageSpike)
            {
                lock (_syncLock)
                {
                    _deviceFaultPackets[device.MacAddress] = _deviceFaultPackets.GetValueOrDefault(device.MacAddress, 0) + 1;
                    string errCode = isMoistureDrop ? $"ERR_LOW_MOISTURE:{device.MacAddress}" :
                                     isVoltageSpike ? $"ERR_OVERVOLTAGE:{device.MacAddress}" :
                                     $"ERR_THRESHOLD:{device.MacAddress}";
                    ActiveErrorStates.Add(errCode);

                    if (isMoistureDrop)
                    {
                        _lastTriggerEvent = "Moisture Sensor B < 20%";
                        RecordedUserHistory.Add($"[SENSOR EVENT] {device.DeviceName} dropped to {request.Reading}% (< 20%)");
                    }

                    packet.Priority = 1;
                    packet.IsCritical = true;
                    packet.EventType = "CRITICAL_TELEMETRY";
                    CriticalAlertQueue.Enqueue(packet, 1);
                }

                NotifyStateChanged();
                return "Typed critical packet enqueued in CriticalAlertQueue (Priority 1) for immediate background processing.";
            }

            lock (_syncLock)
            {
                packet.Priority = 2;
                packet.IsCritical = false;
                packet.EventType = "STANDARD_TELEMETRY";
                TelemetryQueue.Enqueue(packet);
            }

            NotifyStateChanged();
            return "Typed telemetry packet enqueued in TelemetryQueue (FIFO) for background worker processing.";
        }

        /// <summary>
        /// Invoked automatically by TelemetryQueueWorker (BackgroundService).
        /// Always drains CriticalAlertQueue (PriorityQueue) completely before dequeuing from TelemetryQueue (FIFO).
        /// </summary>
        public bool DequeueAndProcessNextPacketInBackground()
        {
            QueuedTelemetryPacket? packet = null;
            int priority = 2;

            lock (_syncLock)
            {
                if (!CriticalAlertQueue.TryDequeue(out packet, out priority))
                {
                    if (!TelemetryQueue.TryDequeue(out packet))
                    {
                        return false;
                    }
                    priority = packet.Priority;
                }

                if (DeviceRegistry.TryGetValue(packet.MacAddress, out var device))
                {
                    device.LastReading = packet.Value;
                    device.LastUpdated = packet.Timestamp;

                    if (packet.EventType == "DISCONNECT_FAULT")
                    {
                        device.Status = "Offline";
                    }
                    else if (packet.IsCritical)
                    {
                        device.Status = "Alert";
                    }
                    else
                    {
                        device.Status = "Online";
                        DisconnectedNodes.Remove(device.MacAddress);
                        ActiveErrorStates.RemoveWhere(e => e.EndsWith(device.MacAddress, StringComparison.OrdinalIgnoreCase));
                        _topologyService.SetNodeOnlineState(device.MacAddress, true);
                    }

                    _topologyService.RecordLiveTelemetryInAvlTree(device);
                }

                TotalPacketsProcessedByWorker++;
                LastBackgroundProcessedSummary =
                    $"[Worker #{TotalPacketsProcessedByWorker} | P{priority} {packet.EventType}] {packet.DeviceName} ({packet.MacAddress}) = {packet.Value}{packet.Unit} @ {packet.Timestamp.ToLocalTime():HH:mm:ss.fff}";
            }

            AddTimelineReading(new TimelineEntry
            {
                MacAddress = packet.MacAddress,
                DeviceName = packet.DeviceName,
                Category = packet.Category,
                Value = packet.Value,
                Unit = packet.Unit,
                EventType = packet.IsCritical ? $"PRIORITY_{priority}" : "FIFO_STD",
                Summary = $"{packet.DeviceName} reported {packet.Value}{packet.Unit} ({packet.EventType})",
                Timestamp = packet.Timestamp
            });

            NotifyStateChanged();
            return true;
        }

        public string ProcessNextQueuedItem()
        {
            bool processed = DequeueAndProcessNextPacketInBackground();
            return processed ? LastBackgroundProcessedSummary : "Both CriticalAlertQueue and TelemetryQueue are empty.";
        }

        public void IssueManualOverride(string macAddress, string actionDescription, bool toggleRelay)
        {
            if (!DeviceRegistry.TryGetValue(macAddress, out var device)) return;

            var command = new OverrideCommand
            {
                TargetMac = device.MacAddress,
                DeviceName = device.DeviceName,
                ActionDescription = actionDescription,
                PreviousRelayState = device.IsRelayActive,
                PreviousStatus = device.Status,
                IssuedAt = DateTime.UtcNow
            };

            lock (_syncLock)
            {
                CommandUndoStack.Push(command);

                if (toggleRelay)
                {
                    device.IsRelayActive = !device.IsRelayActive;
                }
                device.Status = "Online";
                device.LastUpdated = DateTime.UtcNow;
                DisconnectedNodes.Remove(device.MacAddress);
                ActiveErrorStates.RemoveWhere(e => e.EndsWith(device.MacAddress, StringComparison.OrdinalIgnoreCase));
                _topologyService.SetNodeOnlineState(device.MacAddress, true);

                RecordedUserHistory.Add($"[COMMAND] {device.DeviceName}: {actionDescription}");
                RecordPatternInteraction(device.DeviceName);
            }

            AddTimelineReading(new TimelineEntry
            {
                MacAddress = device.MacAddress,
                DeviceName = device.DeviceName,
                Category = device.Category,
                Value = device.LastReading,
                Unit = device.Unit,
                EventType = "OVERRIDE",
                Summary = $"Executed '{actionDescription}' on {device.DeviceName}",
                Timestamp = DateTime.UtcNow
            });

            NotifyStateChanged();
        }

        public OverrideCommand? UndoLastCommand()
        {
            OverrideCommand? lastCommand;
            DeviceState? device = null;

            lock (_syncLock)
            {
                if (CommandUndoStack.Count == 0 || !CommandUndoStack.TryPop(out lastCommand))
                    return null;

                if (DeviceRegistry.TryGetValue(lastCommand.TargetMac, out device))
                {
                    device.IsRelayActive = lastCommand.PreviousRelayState;
                    device.Status = lastCommand.PreviousStatus;
                    if (device.Status != "Online")
                    {
                        DisconnectedNodes.Add(device.MacAddress);
                        ActiveErrorStates.Add($"ERR_RESTORED_STATE:{device.MacAddress}");
                    }
                }
            }

            if (device != null && lastCommand != null)
            {
                AddTimelineReading(new TimelineEntry
                {
                    MacAddress = device.MacAddress,
                    DeviceName = device.DeviceName,
                    Category = device.Category,
                    Value = device.LastReading,
                    Unit = device.Unit,
                    EventType = "UNDO",
                    Summary = $"Reverted '{lastCommand.ActionDescription}' on {device.DeviceName}",
                    Timestamp = DateTime.UtcNow
                });
            }

            NotifyStateChanged();
            return lastCommand;
        }

        public void RecordUserSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return;
            string cleanTerm = searchTerm.Trim();

            lock (_syncLock)
            {
                RecordedUserHistory.Add($"[SEARCH] '{cleanTerm}'");
                _queryFrequency[cleanTerm] = _queryFrequency.GetValueOrDefault(cleanTerm, 0) + 1;
                RecordPatternInteraction(cleanTerm);
            }

            NotifyStateChanged();
        }

        private void RecordPatternInteraction(string currentEntity)
        {
            if (!string.IsNullOrEmpty(_lastTriggerEvent) && !string.Equals(_lastTriggerEvent, currentEntity, StringComparison.OrdinalIgnoreCase))
            {
                if (!_actionCoOccurrence.ContainsKey(_lastTriggerEvent))
                {
                    _actionCoOccurrence[_lastTriggerEvent] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }
                _actionCoOccurrence[_lastTriggerEvent][currentEntity] =
                    _actionCoOccurrence[_lastTriggerEvent].GetValueOrDefault(currentEntity, 0) + 1;
            }
            _lastTriggerEvent = currentEntity;
        }

        /// <summary>
        /// Computes every recommendation confidence strictly from observed counts (MatchedCount / TotalObservations * 100.0).
        /// </summary>
        public List<PredictiveRecommendation> GetPredictiveRecommendations()
        {
            var recommendations = new List<PredictiveRecommendation>();

            lock (_syncLock)
            {
                // 1. Co-occurrence pattern confidence computed strictly from transition counts
                if (DeviceRegistry.TryGetValue("00:1B:44:11:3A:B7", out var moistureSensor) && moistureSensor.LastReading < 20.0)
                {
                    int pumpMatches = 0;
                    int totalTransitionsFromDrop = 0;

                    if (_actionCoOccurrence.TryGetValue("Moisture Sensor B < 20%", out var transitions))
                    {
                        totalTransitionsFromDrop = transitions.Values.Sum();
                        pumpMatches = transitions
                            .Where(k => k.Key.Contains("Pump", StringComparison.OrdinalIgnoreCase))
                            .Sum(k => k.Value);
                    }

                    double computedConfidence = totalTransitionsFromDrop > 0
                        ? Math.Round((double)pumpMatches / totalTransitionsFromDrop * 100.0, 1)
                        : 0.0;

                    recommendations.Add(new PredictiveRecommendation
                    {
                        Title = "Automated Insight: Moisture Drop → Activate Pump A",
                        Reason = $"Moisture Sensor B is at {moistureSensor.LastReading}%. Co-occurrence count: {pumpMatches} 'Pump A' actions out of {totalTransitionsFromDrop} total post-drop actions.",
                        SuggestedCommand = "Switch ON Water Pump A (Irrigation Override)",
                        TargetMac = "00:1B:44:11:3A:B8",
                        MatchedCount = pumpMatches,
                        TotalObservations = totalTransitionsFromDrop,
                        ConfidencePercentage = computedConfidence
                    });
                }

                // 2. Fault detection confidence computed strictly from fault packet counts vs total packets for that device
                foreach (var errorState in ActiveErrorStates)
                {
                    var parts = errorState.Split(':');
                    if (parts.Length < 2) continue;
                    string errorType = parts[0];
                    string faultyMac = string.Join(":", parts.Skip(1));

                    if (DeviceRegistry.TryGetValue(faultyMac, out var faultyDevice) && faultyDevice.MacAddress != "00:1B:44:11:3A:B7")
                    {
                        int faultCount = _deviceFaultPackets.GetValueOrDefault(faultyMac, 1);
                        int totalPackets = Math.Max(1, _deviceTotalPackets.GetValueOrDefault(faultyMac, 1));
                        double computedFaultConfidence = Math.Round((double)faultCount / totalPackets * 100.0, 1);

                        recommendations.Add(new PredictiveRecommendation
                        {
                            Title = $"Active Error State ({errorType}): {faultyDevice.DeviceName}",
                            Reason = $"Fault ratio calculated from counts: {faultCount} anomalous packets out of {totalPackets} total packets received from {faultyDevice.MacAddress}.",
                            SuggestedCommand = $"Reset & Toggle Relay on {faultyDevice.DeviceName}",
                            TargetMac = faultyDevice.MacAddress,
                            MatchedCount = faultCount,
                            TotalObservations = totalPackets,
                            ConfidencePercentage = computedFaultConfidence
                        });
                    }
                }

                // 3. Query Frequency & Sequential Follow-Up Confidence computed strictly from search/command counts
                int totalSearches = _queryFrequency.Values.Sum();
                if (totalSearches > 0)
                {
                    var topQuery = _queryFrequency.OrderByDescending(kvp => kvp.Value).First();
                    var matchedDevice = DeviceRegistry.Values.FirstOrDefault(d =>
                        d.DeviceName.Contains(topQuery.Key, StringComparison.OrdinalIgnoreCase) ||
                        d.Category.Contains(topQuery.Key, StringComparison.OrdinalIgnoreCase) ||
                        d.MacAddress.Contains(topQuery.Key, StringComparison.OrdinalIgnoreCase));

                    if (matchedDevice != null && recommendations.All(r => r.TargetMac != matchedDevice.MacAddress))
                    {
                        double queryConfidence = Math.Round((double)topQuery.Value / totalSearches * 100.0, 1);
                        recommendations.Add(new PredictiveRecommendation
                        {
                            Title = $"Frequent Operator Target: {matchedDevice.DeviceName}",
                            Reason = $"Query frequency count: '{topQuery.Key}' accounted for {topQuery.Value} of {totalSearches} total recorded operator searches.",
                            SuggestedCommand = $"Toggle Smart Relay on {matchedDevice.DeviceName}",
                            TargetMac = matchedDevice.MacAddress,
                            MatchedCount = topQuery.Value,
                            TotalObservations = totalSearches,
                            ConfidencePercentage = queryConfidence
                        });
                    }
                }
            }

            return recommendations;
        }

        public void NotifyStateChanged() => OnStreamUpdated?.Invoke();
    }
}