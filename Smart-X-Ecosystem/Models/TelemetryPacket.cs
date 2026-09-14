using System;
using System.ComponentModel.DataAnnotations;

namespace Smart_X_Ecosystem.Models
{
    public class TelemetryPacket<T>
    {
        [Required(ErrorMessage = "MAC Address is required.")]
        [RegularExpression("^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$", ErrorMessage = "Invalid MAC Address format.")]
        public string MacAddress { get; set; }

        [Required(ErrorMessage = "Deployment Location is required.")]
        public string DeploymentLocation { get; set; }

        [Required(ErrorMessage = "Sensor Category is required.")]
        public string SensorCategory { get; set; }

        public DateTime Timestamp { get; set; }
        public T Payload { get; set; }

        public TelemetryPacket()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}