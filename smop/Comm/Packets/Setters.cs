using System;
using System.Collections.Generic;
using System.Linq;

namespace SMOP.Comm.Packets
{
    public class SetActuators : Request
    {
        public SetActuators(Actuator[] actuators) : base(PacketType.SetActuators, actuators.SelectMany(actuator => actuator.Query).ToArray()) { }
    }
    public class SetSystem : Request
    {
        public SetSystem(bool fans, bool pids) : base(PacketType.SetSystem, new byte[] {
            (byte)(fans ? 1 : 0),
            (byte)(pids ? 1 : 0)
        }) { }
    }

    public class SetMeasurements : Request
    {
        public enum Command { Stop = 0, Start = 1, Once = 2 }
        public SetMeasurements(Command command) : base(PacketType.SetSystem, new byte[] { (byte)command }) { }
    }

    /// <summary>
    /// Debugging only, takes around 1500ms, does not send an ACKNOWLEDGE packet
    /// </summary>
    public class Reset : Request
    {
        public Reset() : base(PacketType.Reset) { }
    }

    /// <summary>
    /// Helping class for <see cref="SetActuators"/>
    /// </summary>
    public class Actuator
    {
        public byte[] Query { get; }
        public Actuator(Device.ID id, Dictionary<Device.Controller, float> caps)
        {
            if (caps.Count == 0)
            {
                throw new ArgumentException("Cannot be empty list", nameof(caps));
            }

            var address = (byte)((byte)id | 0x80);

            var query = new List<byte> { address };
            foreach (var (ctrl, value) in caps)
            {
                query.Add((byte)ctrl);
                BtoD controllerValue = ctrl switch
                {
                    // value must be 0..1 (float)
                    Device.Controller.OdorantFlow =>
                        new BtoD(Math.Max(Math.Min(value / Device.MAX_ODOR_FLOW_RATE, Device.MAX_ODOR_FLOW_RATE), 0)),
                    // value must be 0..1 (float)
                    Device.Controller.DilutionAirFlow =>
                        new BtoD(Math.Max(Math.Min(value / Device.MAX_DILUTION_AIR_FLOW_RATE, Device.MAX_DILUTION_AIR_FLOW_RATE), 0)),
                    // value must be Celsius 0..100 (float)
                    Device.Controller.ChassisTemperature =>
                        new BtoD(Math.Max(Math.Min(value, 50), 0)),
                    // value must tbe millliseconds (int):
                    //      >0 - sets the time for the valve to stay ON, ms
                    //      0  - turns the valve OFF
                    //      <0 - turns the valve ON
                    Device.Controller.OdorantValve or Device.Controller.OutputValve =>
                        new BtoD((int)value),
                    // not permitted capabilities to be set
                    _ => throw new Exception($"Unsupported controller with id={(int)ctrl}")
                };
                query.AddRange(controllerValue.ToArray());
            }

            Query = query.ToArray();
        }
    }
}
