namespace SMOP.Comm.Packets
{
    public class Version : Response
    {
        public string Hardware { get; }
        public string Software { get; }
        public string Protocol { get; }
        public static Version? From(Response msg)
        {
            if (msg?.Type != PacketType.Version || msg?.Payload.Length != 3)
            {
                return null;
            }

            return new Version(msg.Payload);
        }
        public Version(byte[] data) : base(PacketType.Version, data)
        {
            Hardware = $"{(data[0] & 0xF0) >> 4}.{data[0] & 0x0F}";
            Software = $"{(data[1] & 0xF0) >> 4}.{data[1] & 0x0F}";
            Protocol = $"{(data[2] & 0xF0) >> 4}.{data[2] & 0x0F}";
        }
        public override string ToString()
        {
            return $"{_type} [Hardware: {Hardware}, Software: {Software}, Protocol: {Protocol}]";
        }
    }
}