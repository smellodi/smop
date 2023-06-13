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
}


public record class ChannelProps(int Slot, string Gas);

public record class Printer(ChannelProps[] Channels);
public record class Config(string Source, Printer Printer);

public record class ChannelRecipe(int Slot, float Flow, float Duration, float Temperature);
public record class Recipe(string Name, ChannelRecipe[]? Channels, float? Dilution = null);

// DMS measurement is defined in DmsMeasurement.cs

public record class SntMeasurement(string Source, float[] Resistors, float Temperature, float Humidity);
