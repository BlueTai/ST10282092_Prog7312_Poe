namespace Smart_X_Ecosystem.Services
{
    public class IngestionEngine
    {
        private readonly double[][] _telemetryBatches;
        private int _currentBatchIndex;

        public IngestionEngine()
        {
            _telemetryBatches = new double[10][];
            _currentBatchIndex = 0;
        }

        public void RecordBurst(double[] readings)
        {
            if (_currentBatchIndex < _telemetryBatches.Length)
            {
                _telemetryBatches[_currentBatchIndex] = readings;
                _currentBatchIndex++;
            }
        }

        public List<double> FlattenAllBatches()
        {
            var flatList = new List<double>();
            for (int i = 0; i < _currentBatchIndex; i++)
            {
                if (_telemetryBatches[i] != null)
                {
                    for (int j = 0; j < _telemetryBatches[i].Length; j++)
                    {
                        flatList.Add(_telemetryBatches[i][j]);
                    }
                }
            }
            return flatList;
        }
    }
}