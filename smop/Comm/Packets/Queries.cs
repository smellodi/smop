namespace SMOP.Comm.Packets
{
    public class QueryVersion : Request
    {
        public QueryVersion() : base(PacketType.QueryVersion) { }
    }
    public class QueryDevices : Request
    {
        public QueryDevices() : base(PacketType.QueryDevices) { }
    }
    public class QueryCapabilities : Request
    {
        /// <param name="deviceID">Device ID in range 0..10</param>
        public QueryCapabilities(byte deviceID) : base(PacketType.QueryCapabilities, new byte[] { deviceID }) { }
        public QueryCapabilities(Device.ID deviceID) : base(PacketType.QueryCapabilities, new byte[] { (byte)deviceID }) { }
    }
}
