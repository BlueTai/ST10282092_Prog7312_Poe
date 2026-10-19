using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Smart_X_Ecosystem.Models;

namespace Smart_X_Ecosystem.Services
{
    public class CommandStreamService
    {
        // 1. Standard FIFO Queue<T> for incoming telemetry packets (15 Marks)
        public Queue<string> TelemetryQueue { get; } = new();

        // 2. PriorityQueue for critical alerts bypassing normal queue (15 Marks)
        public PriorityQueue<string, int> CriticalAlertQueue { get; } = new();

        // 3. Stack<T> (FILO/LIFO) for manual override command undo history (15 Marks)
        public Stack<OverrideCommand> CommandUndoStack { get; } = new();

        // 4. Dictionary<TKey, TValue> keyed on MAC for O(1) lookups (15 Marks)
        public Dictionary<string, DeviceState> DeviceRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

        // 5. SortedDictionary keyed on timestamp for chronological ordered timeline (15 Marks)
        public SortedDictionary<DateTime, string> SensorTimeline { get; } = new();

        // 6. HashSet<T> for unique active error states / disconnected nodes (10 Marks)
        public HashSet<string> DisconnectedNodes { get; } = new(StringComparer.OrdinalIgnoreCase);

        // LU4 Theme 1 & Theme 4 structures (Jamro Ch. 4)
        public Hashtable HardwareMetadataTable { get; } = new()
        {
            { "00:1B:44", "Espressif Systems (ESP32-WROOM-32D)" },
            { "FIRMWARE", "v2.4.1-RTOS" }
        };

        public SortedSet<string> ActiveCategoriesSet { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "Hydroponics", "Power", "HVAC"
        };

        // Predictive Engine Tracking Data Structures (30 Marks)
        public List<string> RecordedUserHistory { get; } = new();
        private readonly Dictionary<string, int> _queryFrequency = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, int>> _actionCoOccurrence = new(StringComparer.OrdinalIgnoreCase);
        private string _lastTriggerEvent = string.Empty;

        public CommandStreamService()
        {
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

            foreach (var node in initialNodes)
            {
                DeviceRegistry[node.MacAddress] = node;
                AddTimelineLog($"[INIT] Registered {node.DeviceName} ({node.MacAddress}) in DeviceRegistry.");
            }

            DisconnectedNodes.Add("00:1B:44:11:3A:C2");
            CriticalAlertQueue.Enqueue("[CRITICAL] Voltage Spike (415.8V) on Substation Meter X (00:1B:44:11:3A:C2)", 1);
        }

        public void AddTimelineLog(string eventDescription)
        {
            DateTime key = DateTime.UtcNow;
            while (SensorTimeline.ContainsKey(key))
            {
                key = key.AddTicks(1);
            }
            SensorTimeline[key] = eventDescription;
        }

        // Extracts devices line-by-line from uploaded .txt file into DeviceRegistry
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

                    DeviceRegistry[mac] = device;
                    ActiveCategoriesSet.Add(category);
                    TelemetryQueue.Enqueue($"[FILE-IMPORT] Loaded {name} ({mac}) at {reading}{unit}");
                    AddTimelineLog($"[FILE-IMPORT] Extracted & registered '{name}' ({mac}) from uploaded .txt batch file.");
                    importedCount++;
                }
            }

            return importedCount;
        }

        public string IngestDeviceTelemetry(TelemetrySimulationRequest request)
        {
            if (!DeviceRegistry.TryGetValue(request.MacAddress, out var device))
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

            device.LastReading = request.Reading;
            device.LastUpdated = DateTime.UtcNow;

            if (request.SimulateDisconnect)
            {
                device.Status = "Offline";
                bool isNewDisconnect = DisconnectedNodes.Add(device.MacAddress);
                if (isNewDisconnect)
                {
                    string alertMsg = $"[DISCONNECT] {device.DeviceName} ({device.MacAddress}) dropped offline!";
                    CriticalAlertQueue.Enqueue(alertMsg, 1);
                    AddTimelineLog(alertMsg);
                    return "Disconnect registered in DisconnectedNodes (HashSet) and routed to CriticalAlertQueue.";
                }
                return "Duplicate disconnect suppressed by DisconnectedNodes (HashSet<string>) — no duplicate alert created!";
            }

            bool isMoistureDrop = device.Category.Equals("Hydroponics", StringComparison.OrdinalIgnoreCase) && request.Reading < 20.0;
            bool isVoltageSpike = device.Category.Equals("Power", StringComparison.OrdinalIgnoreCase) && request.Reading > 380.0;

            if (request.IsCriticalAlert || isMoistureDrop || isVoltageSpike)
            {
                device.Status = "Alert";
                DisconnectedNodes.Add(device.MacAddress);

                if (isMoistureDrop)
                {
                    _lastTriggerEvent = "Moisture Sensor B < 20%";
                    RecordedUserHistory.Add($"[SENSOR EVENT] {device.DeviceName} dropped to {request.Reading}% (< 20%)");
                }

                string critMsg = $"[PRIORITY 1] Critical reading ({request.Reading}{device.Unit}) on {device.DeviceName} ({device.MacAddress})";
                CriticalAlertQueue.Enqueue(critMsg, 1);
                AddTimelineLog(critMsg);
                return "Critical alert bypassed TelemetryQueue via CriticalAlertQueue (Priority 1).";
            }

            device.Status = "Online";
            DisconnectedNodes.Remove(device.MacAddress);
            string stdMsg = $"[FIFO] {device.DeviceName} ({device.MacAddress}) reported {request.Reading}{device.Unit}";
            TelemetryQueue.Enqueue(stdMsg);
            AddTimelineLog(stdMsg);
            return "Standard telemetry enqueued in TelemetryQueue (FIFO).";
        }

        public string ProcessNextQueuedItem()
        {
            if (CriticalAlertQueue.TryDequeue(out string? critAlert, out int priority))
            {
                string log = $"[DEQUEUED PRIORITY {priority}] {critAlert}";
                AddTimelineLog(log);
                return log;
            }

            if (TelemetryQueue.TryDequeue(out string? stdPacket))
            {
                string log = $"[DEQUEUED FIFO] {stdPacket}";
                AddTimelineLog(log);
                return log;
            }

            return "Both CriticalAlertQueue and TelemetryQueue are empty.";
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

            CommandUndoStack.Push(command);

            if (toggleRelay)
            {
                device.IsRelayActive = !device.IsRelayActive;
            }
            device.Status = "Online";
            device.LastUpdated = DateTime.UtcNow;
            DisconnectedNodes.Remove(device.MacAddress);

            RecordedUserHistory.Add($"[COMMAND] {device.DeviceName}: {actionDescription}");
            RecordPatternInteraction(device.DeviceName);
            AddTimelineLog($"[OVERRIDE] Executed '{actionDescription}' on {device.DeviceName}. Pushed to CommandUndoStack.");
        }

        public OverrideCommand? UndoLastCommand()
        {
            if (CommandUndoStack.Count == 0 || !CommandUndoStack.TryPop(out var lastCommand))
                return null;

            if (DeviceRegistry.TryGetValue(lastCommand.TargetMac, out var device))
            {
                device.IsRelayActive = lastCommand.PreviousRelayState;
                device.Status = lastCommand.PreviousStatus;
                if (device.Status != "Online")
                {
                    DisconnectedNodes.Add(device.MacAddress);
                }
                AddTimelineLog($"[UNDO] Reverted '{lastCommand.ActionDescription}' on {device.DeviceName} via CommandUndoStack.Pop().");
            }

            return lastCommand;
        }

        public void RecordUserSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return;
            string cleanTerm = searchTerm.Trim();
            RecordedUserHistory.Add($"[SEARCH] '{cleanTerm}'");

            _queryFrequency[cleanTerm] = _queryFrequency.GetValueOrDefault(cleanTerm, 0) + 1;
            RecordPatternInteraction(cleanTerm);
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

        public List<PredictiveRecommendation> GetPredictiveRecommendations()
        {
            var recommendations = new List<PredictiveRecommendation>();

            // Pattern 1: Moisture drop below 20% -> Suggest Pump A
            if (DeviceRegistry.TryGetValue("00:1B:44:11:3A:B7", out var moistureSensor) && moistureSensor.LastReading < 20.0)
            {
                int pumpPatternCount = 0;
                if (_actionCoOccurrence.TryGetValue("Moisture Sensor B < 20%", out var transitions))
                {
                    pumpPatternCount = transitions.Where(k => k.Key.Contains("Pump", StringComparison.OrdinalIgnoreCase)).Sum(k => k.Value);
                }

                recommendations.Add(new PredictiveRecommendation
                {
                    Title = "Automated Insight: Moisture Drop → Activate Pump A",
                    Reason = $"Moisture Sensor B is at {moistureSensor.LastReading}% (< 20%). Recorded history shows operators search/trigger 'Pump A' after moisture drops ({pumpPatternCount} recorded pattern matches).",
                    SuggestedCommand = "Switch ON Water Pump A (Irrigation Override)",
                    TargetMac = "00:1B:44:11:3A:B8",
                    Confidence = pumpPatternCount > 0 ? $"Learned Pattern ({92 + Math.Min(pumpPatternCount, 7)}%)" : "Threshold Rule (90%)"
                });
            }

            // Pattern 2: Faulty node proactive highlights
            foreach (var faultyMac in DisconnectedNodes)
            {
                if (DeviceRegistry.TryGetValue(faultyMac, out var faultyDevice) && faultyDevice.MacAddress != "00:1B:44:11:3A:B7")
                {
                    recommendations.Add(new PredictiveRecommendation
                    {
                        Title = $"Faulty Node Highlight: {faultyDevice.DeviceName}",
                        Reason = $"Device ({faultyDevice.MacAddress}) is currently in '{faultyDevice.Status}' state with reading {faultyDevice.LastReading}{faultyDevice.Unit}. Proactively surfaced before manual query.",
                        SuggestedCommand = $"Reset & Toggle Relay on {faultyDevice.DeviceName}",
                        TargetMac = faultyDevice.MacAddress,
                        Confidence = "Fault Detection (96%)"
                    });
                }
            }

            // Pattern 3: Sequential behavioral co-occurrence from search/command transitions
            if (!string.IsNullOrEmpty(_lastTriggerEvent) && _actionCoOccurrence.TryGetValue(_lastTriggerEvent, out var nextTargets))
            {
                var bestFollowUp = nextTargets.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
                if (bestFollowUp.Value > 0)
                {
                    var matchedDevice = DeviceRegistry.Values.FirstOrDefault(d =>
                        d.DeviceName.Contains(bestFollowUp.Key, StringComparison.OrdinalIgnoreCase) ||
                        d.Category.Contains(bestFollowUp.Key, StringComparison.OrdinalIgnoreCase));

                    if (matchedDevice != null && recommendations.All(r => r.TargetMac != matchedDevice.MacAddress))
                    {
                        recommendations.Add(new PredictiveRecommendation
                        {
                            Title = $"Learned Workflow: Next Action on {matchedDevice.DeviceName}",
                            Reason = $"Based on your recorded history, after '{_lastTriggerEvent}', you followed up with '{bestFollowUp.Key}' ({bestFollowUp.Value}x).",
                            SuggestedCommand = $"Toggle Smart Relay on {matchedDevice.DeviceName}",
                            TargetMac = matchedDevice.MacAddress,
                            Confidence = "History Pattern (89%)"
                        });
                    }
                }
            }

            return recommendations;
        }
    }
}