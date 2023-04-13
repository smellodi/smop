using Smop.ML;
using System.Text.Json;

Cout.Init();

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing Machine Learning module (Smop.ML)...\n");

bool isSimulating = true;

Console.Write("Should the app enter the simulation mode (y/N)?   ");

do
{
    var resp = Console.ReadKey();
    if (resp.Key == ConsoleKey.Y || resp.Key == ConsoleKey.N || resp.Key == ConsoleKey.Enter)
    {
        isSimulating = resp.Key == ConsoleKey.Y;
        break;
    }

    Console.CursorLeft--;
} while (true);

Console.Write("\n");

var ml = new Communicator(isSimulating);
ml.Parameter = Smop.IonVision.SimulatedData.ParameterDefinition;
ml.RecipeReceived += (s, e) => Print(e);

var commands = new Dictionary<string, (string, Action?)>()
{
    { "check", ("checks the connection status", () => Print(ml.IsConnected)) },
    { "data", ("send data to ML", () => ml.Publish(Smop.IonVision.SimulatedData.ScanResult) ) },
    { "help", ("displays available commands", null) },
    { "exit", ("exists the app", null) },
};


PrintHelp();

while (true)
{
    Console.Write("\nCommand: ");
    var cmd = Console.ReadLine() ?? "";
    if (!commands.TryGetValue(cmd, out var requestDesc))
    {
        Console.WriteLine("Unknown command");
        continue;
    }

    var request = requestDesc.Item2;
    if (request == null)
    {
        if (cmd == "help")
        {
            PrintHelp();
            continue;
        }
        else
        {
            break;
        }
    }

    requestDesc.Item2!();
    Console.Write("\n");
}

Console.WriteLine("\nTesting finished.");

void PrintHelp()
{
    Console.WriteLine("\nAvailable commands:");
    foreach (var cmd in commands)
    {
        if (!string.IsNullOrEmpty(cmd.Key))
        {
            Console.WriteLine($"    {cmd.Key,-8} - {cmd.Value.Item1}");
        }
    }
}

const int MAX_CHARS_TO_PRINT = 700;

void Print<T>(T response)
{
    if (response is bool boolValue)
    {
        Console.WriteLine(boolValue);
    }
    else if (response is Request request)
    {
        var text = JsonSerializer.Serialize(request.Content, new JsonSerializerOptions()
        {
            WriteIndented = true,
        });
        Console.WriteLine(text.Length < MAX_CHARS_TO_PRINT ? text : $"{text[..MAX_CHARS_TO_PRINT]}... and {text.Length - MAX_CHARS_TO_PRINT} chars more.");
    }
    else
    {
        Console.WriteLine(response);
    }
}
