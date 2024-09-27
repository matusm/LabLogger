namespace LabLogger
{
    class Room
    {
        public Room(string ip, string roomName, string path)
        {
            RoomName = roomName.Trim();
            Path = path.Trim();
            Device = new Transmitter(ip);
        }

        public string RoomName { get; }
        public string Path { get; }
        internal Transmitter Device { get; }

        public override string ToString() => $"{Device.TransmitterID}  {RoomName}";
    }
}
