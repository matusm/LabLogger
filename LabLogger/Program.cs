using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using RestSharp;

namespace LabLogger
{
    class Program
    {
        private static int LOG_INTERVALL;           // in minutes, must be divisor of 60
        private static int LOG_INTERVALL_TOLERANCE; // in seconds
        private static Room[] rooms;
        private static DateTime timeStamp;
        private static string ThingSpeakWriteApiKey;
        private static RestClient clientTS;

        /****************************************************************************************/
        // mimic an Arduino sketch

        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            
            Setup();
            while (true)
            {
                Loop();
            }
        }

        /****************************************************************************************/

        private static void Setup()
        {
            Console.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}");
            List<Room> roomsList = new List<Room>();
            // directories must exist prior to use!
            // roomsList.Add(new Room("/dev/tty.usbserial-FTY594BQ", "Home [Matus]", @""));
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Port1))
                roomsList.Add(new Room(Properties.Settings.Default.Port1, Properties.Settings.Default.Location1, Properties.Settings.Default.FileLocation1));
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Port2))
                roomsList.Add(new Room(Properties.Settings.Default.Port2, Properties.Settings.Default.Location2, Properties.Settings.Default.FileLocation2));
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Port3))
                roomsList.Add(new Room(Properties.Settings.Default.Port3, Properties.Settings.Default.Location3, Properties.Settings.Default.FileLocation3));
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.Port4))
                roomsList.Add(new Room(Properties.Settings.Default.Port4, Properties.Settings.Default.Location4, Properties.Settings.Default.FileLocation4));
            LOG_INTERVALL = Properties.Settings.Default.LogIntervallMinutes;
            LOG_INTERVALL_TOLERANCE = Properties.Settings.Default.LogToleranceSeconds;


            rooms = roomsList.ToArray();

            UpdateSensorValues();
            Console.WriteLine($"Averaging intervall {LOG_INTERVALL} min.");
            Console.WriteLine($"{rooms.Length} transmitter(s):");
            foreach (var room in rooms)
            {
                Console.WriteLine($" - {room}");
            }
            Console.WriteLine();
            
            if (Properties.Settings.Default.ThingSpeakEnable)
            {
                clientTS = new RestClient("https://api.thingspeak.com/update");
            }
            ThingSpeakWriteApiKey = Properties.Settings.Default.ThingSpeakWriteApiKey;
        }

        /****************************************************************************************/

        private static void Loop()
        {
            UpdateSensorValues();
            timeStamp = rooms[0].Device.TimeStamp;
            if (TimeForLogging(timeStamp))
            {
                SaveData(GenerateFileName(timeStamp));
                WriteDataToConsole();
                PublishThingSpeak();
                Thread.Sleep(1000 * LOG_INTERVALL_TOLERANCE);
                ResetSensorValues();
            }
        }

        /****************************************************************************************/

        private static void UpdateSensorValues()
        {
            foreach (var room in rooms)
                room.Device.Update();
            Thread.Sleep(1000);
        }

        /****************************************************************************************/

        private static void ResetSensorValues()
        {
            foreach (var room in rooms)
                room.Device.Reset();
        }

        /****************************************************************************************/

        private static void WriteDataToConsole()
        {
            foreach (var room in rooms)
            {
                Transmitter device = room.Device;
                string textLine = $"{timeStamp.ToString("HH:mm")} [{room.RoomName}]       {device.AirTemperature:F2} ± {device.AirTemperatureRange / 2:F2} °C       {device.AirHumidity:F1} ± {device.AirHumidityRange / 2:F1} %";
                Console.WriteLine(textLine);
            }
        }

        /****************************************************************************************/

        private static void SaveData(string baseFileName)
        {
            foreach (var room in rooms)
            {
                string fullFileName = Path.Combine(room.Path, baseFileName);
                WriteDataToFile(fullFileName, room);
            };
        }

        /****************************************************************************************/

        private static void PublishThingSpeak() => PublishThingSpeak("");

        private static void PublishThingSpeak(string status)
        {
            if (!Properties.Settings.Default.ThingSpeakEnable) 
                return;
            double[] field = new double[8];
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = double.NaN;
            }
            for (int i = 0; i < rooms.Length; i++)
            {
                field[2 * i] = rooms[i].Device.AirTemperature;
                field[2 * i + 1] = rooms[i].Device.AirHumidity;
                status += $"{rooms[i].Device.TransmitterID} ";
            }
            // the sample size of the first transmitter is reported too
            // this may change
            field[7] = rooms[0].Device.SampleSize;

            string data = $"?api_key={ThingSpeakWriteApiKey}&created_at={timeStamp.ToString("yyyy-MM-dd HH:mm:ss+000")}" +
                $"&field1={field[0]:F3}&field2={field[1]:F3}&field3={field[2]:F3}&field4={field[3]:F3}&field5={field[4]:F3}&field6={field[5]:F3}&field7={field[6]:F3}&field8={field[7]:F0}&status={status}";

            RestRequest request = new RestRequest(data, DataFormat.Json);
            IRestResponse response = clientTS.Get(request);
            if(!response.IsSuccessful)
            {
                Console.WriteLine($"ThingSpeak response: {response.Content}");
                Console.WriteLine();
            }
        }

        private static void PublishStatusThingSpeak(string status)
        {
            if (!Properties.Settings.Default.ThingSpeakEnable)
                return;
            string data = $"?api_key={ThingSpeakWriteApiKey}&created_at={timeStamp.ToString("yyyy-MM-dd HH:mm:ss+000")}&status={status}";
            RestRequest request = new RestRequest(data, DataFormat.Json);
            IRestResponse response = clientTS.Get(request);
            if (!response.IsSuccessful)
            {
                Console.WriteLine($"ThingSpeak response: {response.Content}");
                Console.WriteLine();
            }
        }

        /****************************************************************************************/

        private static void WriteDataToFile(string fileName, Room room)
        {
            try
            {
                StreamWriter writer = new StreamWriter(fileName, true);
                if (IsTextFileEmpty(fileName))
                    writer.WriteLine(GenerateCsvHeader());
                writer.WriteLine(GenerateCsvLine(room));
                writer.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine($"{fileName} could not be updated!");
            }
        }

        /****************************************************************************************/

        private static bool IsTextFileEmpty(string fileName)
        {
            var info = new FileInfo(fileName);
            if (info.Length == 0)
                return true;
            // only if your use case can involve files with 1 or a few bytes of content.
            if (info.Length < 6)
            {
                var content = File.ReadAllText(fileName);
                return content.Length == 0;
            }
            return false;
        }

        /****************************************************************************************/

        private static string GenerateCsvLine(Room room)
        {
            string timeStampTS = timeStamp.ToString("yyyy-MM-dd HH:mm:ss+000");
            return $"{timeStampTS},{room.RoomName},{room.Device.AirTemperature:F3},{room.Device.AirTemperatureMax:F2},{room.Device.AirTemperatureMin:F2},{room.Device.AirHumidity:F3},{room.Device.AirHumidityMax:F2},{room.Device.AirHumidityMin:F2},{room.Device.SampleSize},{room.Device.TransmitterID}";
        }

        private static string GenerateCsvHeader() => $"timestamp,room,average temperature,maximum temperature,minimum temperature,average humidity,maximum humidity,minimum humidity,sample size,transmitter SN";

        /****************************************************************************************/

        private static bool TimeForLogging(DateTime timeStamp)
        {
            int minutes = timeStamp.Minute;
            int seconds = timeStamp.Second;
            if (seconds <= (LOG_INTERVALL_TOLERANCE - 1))
            {
                if (minutes % LOG_INTERVALL == 0)
                {
                    return true;
                }
            }
            return false;
        }

        /****************************************************************************************/

        private static string GenerateFileName(DateTime timeStamp) => $"{timeStamp.ToString("yyyyMMdd")}.csv";

        /****************************************************************************************/

    }
}
