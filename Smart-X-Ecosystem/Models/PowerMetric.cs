namespace Smart_X_Ecosystem.Models
{
    public struct PowerMetric
    {
        public double Voltage { get; set; }
        public double Amperage { get; set; }
        public int Wattage { get; set; }

        public double Kilowatts => (Voltage > 0 && Amperage > 0)
            ? (Voltage * Amperage) / 1000.0
            : (Wattage > 0 ? Wattage / 1000.0 : 0.0);

        public PowerMetric(int wattage)
        {
            Wattage = wattage;
            Voltage = 230.0;
            Amperage = wattage > 0 ? (double)wattage / 230.0 : 0.0;
        }

        public PowerMetric(double voltage, double amperage)
        {
            Voltage = voltage;
            Amperage = amperage;
            Wattage = (int)(voltage * amperage);
        }

        // Direct aggregation of sensor values
        public static PowerMetric operator +(PowerMetric a, PowerMetric b)
        {
            return new PowerMetric(a.Voltage + b.Voltage, a.Amperage + b.Amperage)
            {
                Wattage = a.Wattage + b.Wattage
            };
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
