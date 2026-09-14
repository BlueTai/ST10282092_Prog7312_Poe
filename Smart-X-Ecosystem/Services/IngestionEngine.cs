using System.Collections.Generic;

namespace Smart_X_Ecosystem.Services
{
    public class IngestionEngine
    {
        // Jagged array to manage bursts of high-frequency data
        private double[][] _rawTelemetryBuffer = new double[10][];
        public List<double> OptimizedTelemetryCollection { get; private set; } = new List<double>();

        public void BufferIncomingBurst(int zoneIndex, double[] burstData)
        {
            _rawTelemetryBuffer[zoneIndex] = burstData;
        }

        public void ProcessBufferToCollection()
        {
            foreach (var batch in _rawTelemetryBuffer)
            {
                if (batch != null)
                {
                    OptimizedTelemetryCollection.AddRange(batch);
                }
            }
        }
    }
}