namespace Smart_X_Ecosystem.Models
{
    public struct PowerMetric
    {
        public int Wattage { get; set; }

        public PowerMetric(int wattage)
        {
            Wattage = wattage;
        }

        // Direct aggregation of sensor values
        public static PowerMetric operator +(PowerMetric a, PowerMetric b)
        {
            return new PowerMetric(a.Wattage + b.Wattage);
        }

        // Delta comparison
        public static bool operator >(PowerMetric a, PowerMetric b)
        {
            return a.Wattage > b.Wattage;
        }

        public static bool operator <(PowerMetric a, PowerMetric b)
        {
            return a.Wattage < b.Wattage;
        }
    }
}