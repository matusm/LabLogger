using At.Matus.DataSeriesPod;
using System;
using System.Threading;

namespace LabLogger
{
    class Transmitter
    {
        public Transmitter(string sensorIp)
        {
            thermo = new EExxThermometer(sensorIp);
            airTemperature = new DataSeriesPod($"[t] {thermo.InstrumentID}");
            airHumidity = new DataSeriesPod($"[h] {thermo.InstrumentID}");
        }

        public double AirTemperature => airTemperature.AverageValue;
        public double AirTemperatureRange => airTemperature.Range;
        public double AirHumidity => airHumidity.AverageValue;
        public double AirHumidityRange => airHumidity.Range;
        public double AirTemperatureMax => airTemperature.MaximumValue;
        public double AirHumidityMax => airHumidity.MaximumValue;
        public double AirTemperatureMin => airTemperature.MinimumValue;
        public double AirHumidityMin => airHumidity.MinimumValue;

        public int SampleSize => (int)airTemperature.SampleSize;
        public double AveragingTime => airTemperature.Duration; // in seconds
        public DateTime TimeStamp { get; private set; }
        public string TransmitterID => thermo.InstrumentID;
        //public string TransmitterSN => thermo.InstrumentSerialNumber;

        public void Update()
        {
            airTemperature.Update(thermo.GetTemperature());
            airHumidity.Update(thermo.GetHumidity());
            TimeStamp = DateTime.UtcNow;
            Thread.Sleep(100);
        }

        public void Reset()
        {
            airTemperature.Restart();
            airHumidity.Restart();
        }

        private readonly IThermoHygrometer thermo;
        private readonly DataSeriesPod airTemperature;
        private readonly DataSeriesPod airHumidity;

        public override string ToString() => TransmitterID;

    }
}
