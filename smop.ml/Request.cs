using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;

namespace Smop.ML;

internal static class PacketType
{
    public static string Recipe => "recipe";
    public static string Measurement => "measurement";
    public static string Config => "config";
}

internal record class Packet(string Type, object Content);

// Here come definitions of the packet's Content
public enum Algorithm
{
    Euclidean
}

public static class Source
{
    public static string DMS => "dms";
    public static string SNT => "snt";
    public static string PID => "pid";
}


public record class ChannelProps(int Id, string Odor, Dictionary<string, object> Props);

public record class ChannelRecipe(int Id, float Flow, float Duration, float? Temperature = null);
public record class Recipe(string Name, bool IsFinal, float Distance, ChannelRecipe[]? Channels)
{
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

internal record class Printer(ChannelProps[] Channels);
internal record class Config(string[] Sources, Printer Printer, int MaxIterationNumber, float Threshold, string Algorithm);

internal record class Content(string Source);

// DMS measurement is defined in DmsMeasurement.cs

internal record class SntMeasurement(SmellInsp.Data Data) : Content(ML.Source.SNT)
{
    public static SntMeasurement From(SmellInsp.Data data) => new(data);
}

internal record class PIDMeasurement(float Data) : Content(ML.Source.PID)
{
    public static PIDMeasurement From(float data) => new(data);
}
