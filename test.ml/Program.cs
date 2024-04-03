using Smop.Common;
using Smop.ML;

Cout.Init();

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing Machine Learning module (Smop.ML)...\n");

// Get debug type

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

// Get server type

Communicator.Type commType = Communicator.Type.Tcp;

Console.Write("\n");
Console.Write("Should the app use File server rahter than TCP server (Y/n)?   ");

do
{
    var resp = Console.ReadKey();
    if (resp.Key == ConsoleKey.Y || resp.Key == ConsoleKey.N || resp.Key == ConsoleKey.Enter)
    {
        commType = resp.Key == ConsoleKey.N ? Communicator.Type.Tcp : Communicator.Type.File;
        break;
    }

    Console.CursorLeft--;
} while (true);


Console.Write("\n");

var ml = new Communicator(commType, isSimulating);
ml.Parameter = Smop.IonVision.SimulatedData.ParameterDefinition;
ml.RecipeReceived += (s, e) => Print(e);

await Task.Delay(300);  
await ml.Config(new string[] { Source.DMS }, new ChannelProps[]
{
    new(0, "nButanol", new() { { "maxFlow", 50 }, { "criticalFlow", 70 } }),
    new(1, "IPA", new() { { "maxFlow", 50 }, { "criticalFlow", 55 } }),
});
await Task.Delay(300);

var commands = new Dictionary<string, (string, Action?)>()
{
    { "check", ("checks the connection status", () => Print(ml.IsConnected)) },
    { "dms", ("send DMS data to ML", () => _ = ml.Publish(Smop.IonVision.SimulatedData.ScanResult) ) },
    { "snt", ("send SNT data to ML", () => _ = ml.Publish(Smop.SmellInsp.SimulatedData.Generate() ) ) },
    { "pid", ("send PID data to ML", () => _ = ml.Publish(68.2f) ) },
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

ml.Dispose();
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

void Print<T>(T response)
{
    if (response is bool boolValue)
    {
        Console.WriteLine(boolValue);
    }
    else if (response is Recipe recipe)
    {
        Console.WriteLine($"Got {recipe.Name}");
    }
    else
    {
        Console.WriteLine($"Unhandled response: {response}");
    }
}
