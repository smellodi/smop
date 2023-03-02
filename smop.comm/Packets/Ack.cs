namespace SMOP.Comm.Packets
{
    public class Ack : Response
    {
        public Result Result { get; }
        public static Ack? From(Response response)
        {
            if (response?.Type != Type.Ack || response?.Payload.Length != 1)
            {
                return null;
            }

            return new Ack((Result)response.Payload[0]);
        }
        public Ack(Result result) : base(Type.Ack, new byte[] { (byte)result })
        {
            Result = result;
        }
        public override string ToString()
        {
            return $"{_type} [{Result}]";
        }
    }
}
