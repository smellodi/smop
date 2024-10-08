using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.OdorDisplay.Packets;

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
    public bool Fans => _payload![0] != 0;
    public bool PIDs => _payload![1] != 0;
    public SetSystem(bool fans, bool pids) : base(Type.SetSystem, new byte[] {
        (byte)(fans ? 1 : 0),
        (byte)(pids ? 1 : 0)
    })
    { }
    public override string ToString() => $"{_type}:" +
        $"\n    Fans {Fans.AsFlag()}" +
        $"\n    PIDs {PIDs.AsFlag()}";
    internal SetSystem(byte[] buffer) : base(buffer) { }
}

public class SetMeasurements : Request
{
    public enum Command { Stop = 0, Start = 1, Once = 2 }
    public Command Mode => (Command)_payload![0];
    public SetMeasurements(Command command) : base(Type.SetMeasurements, new byte[] { (byte)command }) { }
    public override string ToString() => $"{_type} {Mode}";
    internal SetMeasurements(byte[] buffer) : base(buffer) { }
}

/// <summary>
/// Debugging only, takes around 1500ms, does not send an ACKNOWLEDGE packet
/// </summary>
public class Reset : Request
{
    public Reset() : base(Type.Reset) { }
}

/// <summary>
/// Type definition of actuator capabilities
/// </summary>
public class ActuatorCapabilities : Dictionary<Device.Controller, float>
{
    public static float ValveOnPermanently => -1;
    public static float ValveOff => 0;

    public static KeyValuePair<Device.Controller, float> OdorantValveOpenPermanently => KeyValuePair.Create(Device.Controller.OdorantValve, ValveOnPermanently);
    public static KeyValuePair<Device.Controller, float> OdorantValveClose => KeyValuePair.Create(Device.Controller.OdorantValve, ValveOff);

    public static KeyValuePair<Device.Controller, float> OutputValveOpenPermanently => KeyValuePair.Create(Device.Controller.OutputValve, ValveOnPermanently);
    public static KeyValuePair<Device.Controller, float> OutputValveClose => KeyValuePair.Create(Device.Controller.OutputValve, ValveOff);

    public ActuatorCapabilities() : base() { }
    public ActuatorCapabilities(params KeyValuePair<Device.Controller, float>[] caps) : base()
    {
        foreach (var cap in caps)
        {
            Add(cap.Key, cap.Value);
        }
    }
}

/// <summary>
/// Helping class for <see cref="SetActuators"/>
/// </summary>
public class Actuator
{
    public byte[] Query { get; }
    public Device.ID DeviceID { get; }
    public ActuatorCapabilities Capabilities { get; }

    public override string ToString() => $"{DeviceID}: {string.Join(", ", Capabilities.Select(c => $"{c.Key}={c.Value}"))}";

    public Actuator(Device.ID id, ActuatorCapabilities caps)
    {
        if (caps.Count == 0)
        {
            throw new ArgumentException("The list cannot be empty", nameof(caps));
        }

        DeviceID = id;
        Capabilities = caps;

        var address = (byte)((byte)id | Packet.DEVICE_MASK);
        var maxFlowRate = id == Device.ID.Base || id == Device.ID.DilutionAir ? Device.MaxBaseAirFlowRate : Device.MaxOdoredAirFlowRate;

        var query = new List<byte> { address };
        foreach (var (ctrl, value) in caps)
        {
            query.Add((byte)ctrl);
            FourBytes controllerValue = ctrl switch
            {
                // value must be 0..1 (float)
                Device.Controller.OdorantFlow or Device.Controller.DilutionAirFlow =>
                    new FourBytes(Math.Max(Math.Min(value / maxFlowRate, 1), 0)),

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
                _ => throw new Exception($"Unknown controller '{(int)ctrl}'")
            };
            query.AddRange(controllerValue.ToArray());
        }

        Query = query.ToArray();
    }

    internal static bool TryFrom(byte[] payload, ref int index, out Actuator? actuator)
    {
        actuator = null;

        Device.ID? device = null;
        var caps = new ActuatorCapabilities();

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

            if ((index + 3) >= payload.Length)
            {
                break;
            }

            Device.Controller ctrl = (Device.Controller)deviceOrType;
            if (ctrl == Device.Controller.OutputValve || ctrl == Device.Controller.OdorantValve)
            {
                caps.Add(ctrl, FourBytes.ToInt(payload[index..(index += 4)]));
            }
            else
            {
                var value = FourBytes.ToFloat(payload[index..(index += 4)]);
                if (ctrl == Device.Controller.OdorantFlow || ctrl == Device.Controller.DilutionAirFlow)
                {
                    // The base MFC returns normalized value, but Odor MFC returns l/min
                    var maxFlowRate = device == Device.ID.Base ? Device.MaxBaseAirFlowRate : 1;
                    value *= maxFlowRate;
                }
                caps.Add(ctrl, value);
            }
        }

        if (device != null && caps.Count > 0)
        {
            actuator = new Actuator((Device.ID)device, caps);
        }

        return actuator != null;
    }
}
