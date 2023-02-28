using System.Collections.Generic;

namespace SMOP.Comm.Packets
{
    public class Devices : Response
    {
        public static int MaxOdorModuleCount { get; } = 9;

        public bool HasBaseModule { get; }
        public bool HasDilutionModule { get; }
        
        public bool HasOdor(int index)
        {
            return index < MaxOdorModuleCount ? _payload![index + 1] != 0 : false;
        }

        public static Devices? From(Response msg)
        {
            if (msg?.Type != PacketType.Devices || msg?.Payload.Length != MaxOdorModuleCount + 2)
            {
                return null;
            }

            return new Devices(msg.Payload);
        }

        public Devices(byte[] data) : base(PacketType.Version, data)
        {
            HasBaseModule = data[0] != 0;
            HasDilutionModule = data[10] != 0;
        }

        public override string ToString()
        {
            var flags = new List<string>()
            {
                $"Base module: {HasBaseModule}",
            };
            for (int i = 0; i < MaxOdorModuleCount; i++ )
            {
                flags.Add($"Odor{i + 1}: {HasOdor(i)}");
            }
            flags.Add($"Dilution module: {HasDilutionModule}");
            return $"{_type} [{string.Join(", ", flags)}]";
        }
    }
}
