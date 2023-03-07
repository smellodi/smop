namespace SMOP.Comm.Packets
{
    public class QueryVersion : Request
    {
        public QueryVersion() : base(Type.QueryVersion) { ExpectedResponse = Type.Version; }
        internal QueryVersion(byte[] buffer) : base(buffer) { ExpectedResponse = Type.Version; }
    }
    public class QueryDevices : Request
    {
        public QueryDevices() : base(Type.QueryDevices) { ExpectedResponse = Type.Devices; }
        internal QueryDevices(byte[] buffer) : base(buffer) { ExpectedResponse = Type.Devices; }
    }
    public class QueryCapabilities : Request
    {
        public Device.ID Device => (Device.ID)_payload![0];

        /// <param name="deviceID">Device ID in range 0..10</param>
        public QueryCapabilities(byte deviceID) : base(Type.QueryCapabilities, new byte[] { deviceID }) { ExpectedResponse = Type.Capabilities; }
        public QueryCapabilities(Device.ID deviceID) : base(Type.QueryCapabilities, new byte[] { (byte)deviceID }) { ExpectedResponse = Type.Capabilities; }
        internal QueryCapabilities(byte[] buffer) : base(buffer) { ExpectedResponse = Type.Capabilities; }
        public override string ToString() => $"{_type} for device '{Device}'";
    }
}
