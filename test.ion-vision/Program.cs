using Smop.IonVision;
using System.Text.Json;

const bool IS_DEBUGGING = true;

var ionVision = new Communicator(IS_DEBUGGING);

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing IonVision module (SMOP.IonVision)...\n");

var commands = new Dictionary<string, (string, Action?)> ()
{
    { "sys", ("retrieves system params", async () => Print(await ionVision.GetSystemStatus())) },
    { "user", ("retrieves user", async () => Print(await ionVision.GetUser())) },
    { "projs", ("retrieves projects", async () => Print(await ionVision.GetProjects())) },
    { "params", ("retrieves parameters", async () => Print(await ionVision.GetParameters())) },
    { "cproj", ("set the current project", async () => Print(await ionVision.SetProject())) },
    { "cparam", ("sets the current parameter", async () => Print(await ionVision.SetParameter())) },
    { "scan", ("starts a new scan", async () => Print(await ionVision.StartScan())) },
    { "p", ("retrieves the scan progress", async () => Print(await ionVision.GetScanProgress())) },
    { "result", ("gets the latest scan result", async () => Print(await ionVision.GetScanResult())) },
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
}

Console.WriteLine("\nTesting finished.");

void PrintHelp()
{
    Console.WriteLine("Available commands:");
    foreach (var cmd in commands)
    {
        if (!string.IsNullOrEmpty(cmd.Key))
        {
            Console.WriteLine($"    {cmd.Key,-8} - {cmd.Value.Item1}");
        }
    }
}

void Print<T>(API.Response<T> response)
{
    if (response.Success)
    {
        Console.WriteLine(JsonSerializer.Serialize(response.Value!, new JsonSerializerOptions()
        {
            WriteIndented = true,
        }));
    }
    else
    {
        Console.WriteLine(response.Error);
    }
}

/*
var projects = await ionVision.GetProjects();

Console.WriteLine("Projects:");
if (projects.Success)
{
    int i = 1;
    foreach (var project in projects.Value!)
    {
        Console.WriteLine($"{i++}:\n" + project);
    }
}
else
{
    Console.WriteLine(projects.Error);
}

string AsDict(object obj)
{
    return string.Join("\n", obj.GetType().GetProperties().Select(p => $"  {p.Name} = {p.GetValue(obj)}"));
}
*/