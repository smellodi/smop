using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;

namespace Smop.ML;

public static class PacketType
{
    public static string Recipe => "recipe";
    public static string Measurement => "measurement";
    public static string Config => "config";
}

public record class Packet(string Type, object Content);

// Here come definitions of the packet's Content

public static class Source
{
    public static string DMS => "dms";
    public static string SNT => "snt";
    public static string PID => "pid";
}


public record class ChannelProps(int Id, string Odor, Dictionary<string, string> Props);

public record class Printer(ChannelProps[] Channels);
public record class Config(string[] Sources, Printer Printer, int MaxIterationNumber, float Threshold);

public record class ChannelRecipe(int Id, float Flow, float Duration, float? Temperature = null);
public record class Recipe(string Name, int IsFinal, float MinRMSE, float Usv, ChannelRecipe[]? Channels)
{
    public bool Finished => IsFinal != 0;

    public Actuator[] ToOdorPrinterActuators()
    {
        var actuators = new List<Actuator>();
        foreach (var channel in Channels ?? Array.Empty<ChannelRecipe>())
        {
            var valveCap = channel.Duration switch
            {
                > 0 => KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantValve, channel.Duration * 1000),
                0 => ActuatorCapabilities.OdorantValveClose,
                _ => ActuatorCapabilities.OdorantValveOpenPermanently,
            };
            var caps = new ActuatorCapabilities(
                valveCap,
                KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, channel.Flow)
            );
            if (channel.Temperature != null)
            {
                caps.Add(OdorDisplay.Device.Controller.ChassisTemperature, (float)channel.Temperature);
            }

            var actuator = new Actuator((OdorDisplay.Device.ID)channel.Id, caps);
            actuators.Add(actuator);
        }

        return actuators.ToArray();
    }
}

public record class Content(string Source);

// DMS measurement is defined in DmsMeasurement.cs

public record class SntMeasurement(SmellInsp.Data Data) : Content(ML.Source.SNT)
{
    public static SntMeasurement From(SmellInsp.Data data) => new(data);
}

public record class PIDMeasurement(float Data) : Content(ML.Source.PID)
{
    public static PIDMeasurement From(float data) => new(data);
}
