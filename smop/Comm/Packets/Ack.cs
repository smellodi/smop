namespace SMOP.Comm.Packets
{
    public class Ack : Response
    {
        public new Result Result { get; }
        public static Ack? From(Response msg)
        {
            if (msg?.Type != PacketType.Ack || msg?.Payload.Length != 1)
            {
                return null;
            }

            return new Ack((Result)msg.Payload[0]);
        }
        public Ack(Result result) : base(PacketType.Ack, new byte[] { (byte)result })
        {
            Result = result;
        }
        public override string ToString()
        {
            return $"{_type} [{Result}]";
        }
    }
}
