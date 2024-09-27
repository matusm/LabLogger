using At.Matus.DataSeriesPod;
using Bev.Instruments.EplusE.EExx;
using System;
using System.Threading;

namespace LabLogger
{
    class Transmitter
    {
        public Transmitter(string sensorIp)
        {
            device = new EExx(sensorIp);
            airTemperature = new DataSeriesPod($"[t] {device.InstrumentID}");
            airHumidity = new DataSeriesPod($"[h] {device.InstrumentID}");
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
        public string TransmitterID => device.InstrumentID;
        public string TransmitterSN => device.InstrumentSerialNumber;

        public void Update()
        {
            var values = device.GetValues();
            airTemperature.Update(values.Temperature);
            airHumidity.Update(values.Humidity);
            TimeStamp = values.TimeStamp;
            Thread.Sleep(100);
        }

        public void Reset()
        {
            airTemperature.Restart();
            airHumidity.Restart();
        }

        private readonly EExx device;
        private readonly DataSeriesPod airTemperature;
        private readonly DataSeriesPod airHumidity;

        public override string ToString() => TransmitterID;

    }
}
