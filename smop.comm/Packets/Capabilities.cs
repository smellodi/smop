using System;
using System.Collections.Generic;

namespace SMOP.Comm.Packets
{
    public class Capabilities : Response
    {
        public bool Has(Device.Capabality capability) => _caps[capability];

        public static Capabilities? From(Response msg)
        {
            if (msg?.Type != PacketType.Capabilities || msg?.Payload.Length != 17)
            {
                return null;
            }

            return new Capabilities(msg.Payload);
        }

        public Capabilities(byte[] data) : base(PacketType.Capabilities, data)
        {
            foreach (var capID in Enum.GetValues(typeof(Device.Capabality)))
            {
                _caps.Add((Device.Capabality)capID, data[(int)capID] != 0);
            }
        }

        public override string ToString()
        {
            var caps = new List<string>();
            foreach (var (key, value) in _caps)
            {
                caps.Add($"{key} = {value}");
            }
            return $"{_type} [{string.Join(", ", caps)}]";
        }

        // Internal

        readonly Dictionary<Device.Capabality, bool> _caps = new();
    }
}
