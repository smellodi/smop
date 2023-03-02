using System;
using System.Collections.Generic;
using System.Linq;

namespace SMOP.Comm.Packets
{
    public class SetActuators : Request
    {
        public Actuator[] Actuators { get; }
        public SetActuators(Actuator[] actuators) : base(Type.SetActuators, actuators.SelectMany(actuator => actuator.Query).ToArray()) { Actuators = actuators; }
        public override string ToString() => $"{_type} with:\n    {string.Join("\n    ", Actuators.Select(a => a.ToString()))}";
        internal SetActuators(byte[] buffer) : base(buffer)
        {
            List<Actuator> actuators = new();
            int index = 0;
            while (Actuator.TryFrom(_payload!, ref index, out Actuator? actuator))
                actuators.Add(actuator!);
            Actuators = actuators.ToArray();
        }
    }
    public class SetSystem : Request
    {
        public bool Fans { get; }
        public bool PIDs { get; }
        public SetSystem(bool fans, bool pids) : base(Type.SetSystem, new byte[] {
            (byte)(fans ? 1 : 0),
            (byte)(pids ? 1 : 0)
        }) { Fans = fans; PIDs = pids; }
        public override string ToString() => $"{_type}:" +
            $"\n    Fans {Fans.AsFlag()}" +
            $"\n    PIDs {PIDs.AsFlag()}";
        internal SetSystem(byte[] buffer) : base(buffer)
        {
            Fans = _payload![0] != 0;
            PIDs = _payload![1] != 0;
        }
    }

    public class SetMeasurements : Request
    {
        public enum Command { Stop = 0, Start = 1, Once = 2 }
        public Command Mode => (Command)_payload![0];
        public SetMeasurements(Command command) : base(Type.SetMeasurements, new byte[] { (byte)command })
        {
            if (Mode == Command.Once)
            {
                ExpectedResponse = Type.Data;
            }
        }
        public override string ToString() => $"{_type} {Mode}";
        internal SetMeasurements(byte[] buffer) : base(buffer)
        {
            if (Mode == Command.Once)
            {
                ExpectedResponse = Type.Data;
            }
        }
    }

    /// <summary>
    /// Debugging only, takes around 1500ms, does not send an ACKNOWLEDGE packet
    /// </summary>
    public class Reset : Request
    {
        public Reset() : base(Type.Reset) { }
    }

    /// <summary>
    /// Helping class for <see cref="SetActuators"/>
    /// </summary>
    public class Actuator
    {
        public byte[] Query { get; }
        public Device.ID DeviceID { get; }
        public Dictionary<Device.Controller, float> Capabilities { get; }
        public Actuator(Device.ID id, Dictionary<Device.Controller, float> caps)
        {
            if (caps.Count == 0)
            {
                throw new ArgumentException("Cannot be empty list", nameof(caps));
            }

            DeviceID = id;
            Capabilities = caps;

            var address = (byte)((byte)id | Packet.DEVICE_MASK);

            var query = new List<byte> { address };
            foreach (var (ctrl, value) in caps)
            {
                query.Add((byte)ctrl);
                FourBytes controllerValue = ctrl switch
                {
                    // value must be 0..1 (float)
                    Device.Controller.OdorantFlow =>
                        new FourBytes(Math.Max(Math.Min(value / Device.MAX_ODOR_FLOW_RATE, Device.MAX_ODOR_FLOW_RATE), 0)),
                    // value must be 0..1 (float)
                    Device.Controller.DilutionAirFlow =>
                        new FourBytes(Math.Max(Math.Min(value / Device.MAX_DILUTION_AIR_FLOW_RATE, Device.MAX_DILUTION_AIR_FLOW_RATE), 0)),
                    // value must be Celsius 0..50 (float)
                    Device.Controller.ChassisTemperature =>
                        new FourBytes(Math.Max(Math.Min(value, 50), 0)),
                    // value must tbe millliseconds (int):
                    //      >0 - sets the time for the valve to stay ON, ms
                    //      0  - turns the valve OFF
                    //      <0 - turns the valve ON
                    Device.Controller.OdorantValve or Device.Controller.OutputValve =>
                        new FourBytes((int)value),
                    // not permitted capabilities to be set
                    _ => throw new Exception($"Unsupported controller with id={(int)ctrl}")
                };
                query.AddRange(controllerValue.ToArray());
            }

            Query = query.ToArray();
        }
        public override string ToString() => $"{DeviceID}: {string.Join(", ", Capabilities.Select(c => $"{c.Key}={c.Value}"))}";
        internal static bool TryFrom(byte[] payload, ref int index, out Actuator? actuator)
        {
            actuator = null;

            Device.ID? device = null;
            var caps = new Dictionary<Device.Controller, float>();

            while ((index + 4) < payload.Length)    // at least 5 bytes must be ahead to continue the loop
            {
                byte deviceOrType = payload[index++];
                if ((deviceOrType & Packet.DEVICE_MASK) > 0)          // device type has 0x80 mask applied
                {
                    if (device != null)
                    {
                        index--;
                        break;
                    }

                    device = (Device.ID)(deviceOrType & ~Packet.DEVICE_MASK);
                    deviceOrType = payload[index++];
                }

                if ((index + 3) >= payload.Length || actuator == null)
                {
                    break;
                }

                var value = FourBytes.ToFloat(payload[index..(index + 4)]);
                Device.Controller ctrl = (Device.Controller)deviceOrType;
                actuator.Capabilities.Add(ctrl, value);
            }

            if (device != null && caps.Count > 0)
            {
                actuator = new Actuator((Device.ID)device, caps);
            }

            return actuator != null;
        }
    }
}
