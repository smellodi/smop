namespace Smop.ML;

public static class PacketType
{
    public static string Recipe => "recipe";
    public static string Measurement => "measurement";
    public static string Config => "config";
}

public record class Packet(string Type, object Content);

// Here come definitions of the packet's Content

public record class ChannelProps(int Slot, string Gas);
public record class Config(ChannelProps[] Channels);

public record class ChannelRecipe(int Slot, float Flow, float Duration, float Temperature);
public record class Recipe(string Name, ChannelRecipe[]? Channels, float? Dilution = null);

// Measurement is define in Measurement.cs