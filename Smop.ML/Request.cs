namespace Smop.ML;

public static class PacketType
{
    public static string Recipe => "recipe";
    public static string Measurement => "measurement";
    public static string Config => "config";
}

public record class Packet(string Type, object Content);

public record class ChannelProps(int Slot, string Gas);
public record class Config(string Type, ChannelProps[] Channels);

public record class Channel(int Slot, float Flow, float Duration, float Temperature);
public record class Recipe(string Name, Channel[]? Channels, float? Dilution = null);
