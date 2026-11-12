using System;

namespace Smart_X_Ecosystem.Models
{
    public class DeviceState
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = "Online";
        public double LastReading { get; set; }
        public double MaxGaugeValue { get; set; } = 100.0;
        public string Unit { get; set; } = string.Empty;
        public bool IsRelayActive { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    // FIX: Typed packet object for TelemetryQueue and CriticalAlertQueue instead of plain strings
    public class QueuedTelemetryPacket
    {
        public string PacketId { get; set; } = Guid.NewGuid().ToString()[..8];
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int Priority { get; set; } = 2; // 1 = Critical Alert, 2 = Normal FIFO
        public bool IsCritical { get; set; }
        public string EventType { get; set; } = "TELEMETRY";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // FIX: Composite key (Timestamp + monotonic Sequence) so simultaneous readings never overwrite each other
    public readonly struct TimelineKey : IComparable<TimelineKey>, IEquatable<TimelineKey>
    {
        public DateTime Timestamp { get; }
        public long Sequence { get; }

        public TimelineKey(DateTime timestamp, long sequence)
        {
            Timestamp = timestamp;
            Sequence = sequence;
        }

        public int CompareTo(TimelineKey other)
        {
            int timeComparison = Timestamp.CompareTo(other.Timestamp);
            return timeComparison != 0 ? timeComparison : Sequence.CompareTo(other.Sequence);
        }

        public bool Equals(TimelineKey other) =>
            Timestamp == other.Timestamp && Sequence == other.Sequence;

        public override bool Equals(object? obj) =>
            obj is TimelineKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Timestamp, Sequence);

        public override string ToString() =>
            $"{Timestamp.ToLocalTime():HH:mm:ss.fff} #{Sequence}";
    }

    // FIX: Structured reading stored in SensorTimeline so numeric values can be charted directly
    public class TimelineEntry
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class OverrideCommand
    {
        public string CommandId { get; set; } = Guid.NewGuid().ToString()[..8];
        public string TargetMac { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string ActionDescription { get; set; } = string.Empty;
        public bool PreviousRelayState { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    }

    public class TelemetrySimulationRequest
    {
        public string MacAddress { get; set; } = string.Empty;
        public double Reading { get; set; }
        public bool IsCriticalAlert { get; set; }
        public bool SimulateDisconnect { get; set; }
    }

    // FIX: Confidence is numeric (double) and computed strictly from runtime event counts
    public class PredictiveRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string SuggestedCommand { get; set; } = string.Empty;
        public string TargetMac { get; set; } = string.Empty;
        public int MatchedCount { get; set; }
        public int TotalObservations { get; set; }
        public double ConfidencePercentage { get; set; }
    }
}