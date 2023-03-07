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
        public QueryCapabilities(Device.ID deviceID) : base(Type.QueryCapabilities, new byte[] { (byte)deviceID }) { ExpectedResponse = Type.Capabilities; }
        public override string ToString() => $"{_type} for device '{Device}'";
        internal QueryCapabilities(byte[] buffer) : base(buffer) { ExpectedResponse = Type.Capabilities; }
    }
}
