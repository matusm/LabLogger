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
        // secrets
        private static readonly string AdafruitIoUsername = "XXXXX";
        private static readonly string AdafruitIoKey = "XXXXX";
        private static readonly string ThingSpeakWriteApiKey = "XXXXX";

        // global fields
        private static readonly int LOG_INTERVALL = 5;           // in minutes, must be divisor of 60
        private static readonly int LOG_INTERVALL_TOLERANCE = 4; // in seconds
        private static List<Room> rooms;
        private static DateTime timeStamp;
        private static RestClient clientTS;
        private static RestClient clientAdafruit;

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
            rooms = new List<Room>();
            // this data should be in a config file
            // directories must exist prior to use
            // /dev/tty.usbserial-FTY594BQ
            rooms.Add(new Room("/dev/tty.usbserial-FTY594BQ", "Home [Matus]", @""));
            //rooms.Add(new Room("COM6", "HP-204 [Matus]", @"D:\temp\Sensor1"));
            //rooms.Add(new Room("COM6", "HP-204 [Matus]", @"D:\temp\Sensor1"));
            //rooms.Add(new Room("COM6", "HP-204 [Matus]", @"D:\temp\Sensor1"));
            UpdateSensorValues();
            Console.WriteLine($"{rooms.Count} transmitters:");
            foreach (var room in rooms)
            {
                Console.WriteLine($" - {room}");
            }
            Console.WriteLine();
            clientTS = new RestClient("https://api.thingspeak.com/update");
            clientAdafruit = new RestClient("https://io.adafruit.com/api/v2/");
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
                PublishAdafruitIO();
                Thread.Sleep(1000 * LOG_INTERVALL_TOLERANCE);
                ResetSensorValues();
            }
        }

        /****************************************************************************************/

        private static void UpdateSensorValues()
        {
            foreach (var room in rooms)
                room.Device.Update();
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
                Console.WriteLine(room.ToString());
                Console.WriteLine($"csv -> {GenerateCsvLine(room)}");
            }
            Console.WriteLine();
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

        private static void PublishThingSpeak()
        {
            double field1 = 0;
            double field2 = 0;
            double field3 = 0;
            double field4 = 0;
            double field5 = 0;
            double field6 = 0;
            double field7 = 0;
            double field8 = 0;

            // quick and dirty
            // one should correspond rooms with the TS fields!
            field1 = rooms[0].Device.AirTemperature;
            field2 = rooms[0].Device.AirTemperatureMax;
            field3 = rooms[0].Device.AirTemperatureMin;
            field4 = rooms[0].Device.AirHumidity;
            field5 = rooms[0].Device.AirHumidityMax;
            field6 = rooms[0].Device.AirHumidityMin;
            field8 = rooms[0].Device.SampleSize;
            string data = $"?api_key={ThingSpeakWriteApiKey}&created_at={timeStamp.ToString("yyyy-MM-dd HH:mm:ss+000")}&field1={field1:F3}&field2={field2:F3}&field3={field3:F3}&field4={field4:F3}&field5={field5:F3}&field6={field6:F3}&field8={field8:F0}";

            RestRequest request = new RestRequest(data, DataFormat.Json);
            IRestResponse response = clientTS.Get(request);
            if(!response.IsSuccessful)
            {
                Console.WriteLine($"ThingSpeak response: {response.Content}");
                Console.WriteLine();
            }
        }

        /****************************************************************************************/

        private static void PublishAdafruitIO()
        {
            // TODO
            bool debug = true;
            if (debug)
                return;

            string data = $"{AdafruitIoUsername}/feeds/{AdafruitIoKey}/data/air-temperature";
            var ioMessage = new AdaIoMessage();
            ioMessage.value = rooms[0].Device.AirTemperature.ToString();
            //var ioMessageText = JsonConvert.SerializeObject(ioMessage);
            RestRequest request = new RestRequest(data, DataFormat.Json);
            IRestResponse response = clientAdafruit.Get(request);
            if (!response.IsSuccessful)
            {
                Console.WriteLine($"AdafruitIO response: {response.Content}");
                Console.WriteLine();
            }
        }

        /****************************************************************************************/

        private static void WriteDataToFile(string fileName, Room room)
        {
            try
            {
                StreamWriter writer = new StreamWriter(fileName, true);
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

        private static string GenerateCsvLine(Room room)
        {
            string timeStampTS = timeStamp.ToString("yyyy-MM-dd HH:mm:ss+000");
            return $"{timeStampTS},{room.Device.AirTemperature:F3},{room.Device.AirTemperatureMax:F2},{room.Device.AirTemperatureMin:F2},{room.Device.AirHumidity:F3},{room.Device.AirHumidityMax:F2},{room.Device.AirHumidityMin:F2},,{room.Device.SampleSize},48.2093,16.3182,210,";
        }

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

        private static string GenerateFileName(DateTime timeStamp)
        {
            string fileExtension = "csv";
            string baseName = timeStamp.ToString("yyyyMMdd");
            return baseName + "." + fileExtension;
        }
    }
}
