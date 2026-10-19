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

    public class PredictiveRecommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string SuggestedCommand { get; set; } = string.Empty;
        public string TargetMac { get; set; } = string.Empty;
        public string Confidence { get; set; } = "High (94%)";
    }
}