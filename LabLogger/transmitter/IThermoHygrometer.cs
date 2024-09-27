namespace LabLogger
{
    public interface IThermoHygrometer
    {
        string InstrumentID { get; }
        double GetTemperature();
        double GetHumidity();
    }
}
