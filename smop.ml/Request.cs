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


public record class ChannelProps(int Id, string Gas, Dictionary<string, string> Props);

public record class Printer(ChannelProps[] Channels);
public record class Config(string[] Sources, Printer Printer, int MaxIterationNumber, float Threshold);

public record class ChannelRecipe(int Id, float Flow, float Duration, float? Temperature = null);
public record class Recipe(string Name, int IsFinal, float MinRMSE, ChannelRecipe[]? Channels)
{
    public bool Finished => IsFinal != 0;
}

// DMS measurement is defined in DmsMeasurement.cs

public record class SntMeasurement(string Source, SmellInsp.Data Data)
{
    public static SntMeasurement From(SmellInsp.Data data) => new(ML.Source.SNT, data);
}

public record class PIDMeasurement(string Source, float Data)
{
    public static PIDMeasurement From(float data) => new(ML.Source.PID, data);
}
