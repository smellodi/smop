using System.Linq;

namespace SMOP.Comm.Packets
{
    public class Version : Response
    {
        public string Hardware { get; }
        public string Software { get; }
        public string Protocol { get; }
        public static Version? From(Response response)
        {
            if (response?.Type != Type.Version || response?.Payload.Length != 3)
            {
                return null;
            }

            return new Version(response.Payload);
        }
        public Version(byte[] payload) : base(Type.Version, payload)
        {
            Hardware = $"{(payload[0] & 0xF0) >> 4}.{payload[0] & 0x0F}";
            Software = $"{(payload[1] & 0xF0) >> 4}.{payload[1] & 0x0F}";
            Protocol = $"{(payload[2] & 0xF0) >> 4}.{payload[2] & 0x0F}";
        }
        public override string ToString() => $"{_type} [Hardware: {Hardware}, Software: {Software}, Protocol: {Protocol}]";

        // Internal

        internal Version(string[] version) : base(Type.Version, version.Select(v => (byte)((((byte)v[0] - '0') << 4) | (byte)v[2] - '0')).ToArray())
        {
            Hardware = version[0];
            Software = version[1];
            Protocol = version[2];
        }
    }
}