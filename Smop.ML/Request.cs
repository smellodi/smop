namespace Smop.ML;

public static class RequestType
{
    public static string Recipe => "recipe";
}

public record class Request(string Type, object Content);

public record class Channel(int Slot, float Flow, float Duration, float Temperature);
public record class Recipe(string Name, Channel[]? Channels, float? dilution = null);
