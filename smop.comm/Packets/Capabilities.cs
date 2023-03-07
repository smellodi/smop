using System;
using System.Collections.Generic;
using System.Linq;

namespace SMOP.Comm.Packets
{
    public class Capabilities : Response
    {
        public bool Has(Device.Capability capability) => _caps[capability];

        public static Capabilities? From(Response response)
        {
            if (response?.Type != Type.Capabilities || response?.Payload.Length != 17)
            {
                return null;
            }

            return new Capabilities(response.Payload);
        }

        public Capabilities(byte[] data) : base(Type.Capabilities, data)
        {
            foreach (var capID in Enum.GetValues(typeof(Device.Capability)))
            {
                _caps.Add((Device.Capability)capID, data[(int)capID] != 0);
            }
        }

        public override string ToString()
        {
            var caps = _caps.Select(cap => $"{cap.Key} = {cap.Value.AsFlag()}");
            return $"{_type}\n    {string.Join("\n    ", caps)}";
        }

        // Internal

        readonly Dictionary<Device.Capability, bool> _caps = new();

        internal Capabilities(Dictionary<Device.Capability,bool> caps) : base(Type.Capabilities, caps.Select(cap => (byte)(cap.Value ? 0xFF : 0)).ToArray())
        {
            _caps = caps;
        }
    }
}
