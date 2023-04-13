namespace Smop.ML;

public enum RequestType
{
    Recipe
}

public record class Request(RequestType Type, object Content);
