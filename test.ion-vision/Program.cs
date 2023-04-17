using Smop.IonVision;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing IonVision module (SMOP.IonVision)...\n");

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

Console.WriteLine();

var ionVision = new Communicator(null, isSimulating);

var isConnected = await ionVision.CheckConnection();

if (!isConnected)
{
    Console.WriteLine("The device is offline");
    return;
}

var commands = new Dictionary<string, (string, Func<Task>?)>()
{
{ "sys", ("retrieves system params", async () => Print(await ionVision.GetSystemStatus())) },
{ "gclock", ("retrieves clock", async () => Print(await ionVision.GetSettingsClock())) },
{ "sclock", ("sets clock", async () => Print(await ionVision.SetSettingsClock())) },
{ "user", ("retrieves user", async () => Print(await ionVision.GetUser())) },
{ "projs", ("retrieves projects", async () => Print(await ionVision.GetProjects())) },
{ "params", ("retrieves parameters", async () => Print(await ionVision.GetParameters())) },
{ "gcpj", ("retrieves the current project", async () => Print(await ionVision.GetProject())) },
{ "scpj", ("sets the current project", async () => Print(await ionVision.SetProject())) },
{ "gcpm", ("retrieves the current parameter", async () => Print(await ionVision.GetParameter())) },
{ "gcpmd", ("retrieves the current parameter definition", async () => Print(await ionVision.GetParameterDefinition())) },
{ "scpm", ("sets the current parameter", async () => Print(await ionVision.SetParameter())) },
{ "scom", ("sets a comment to be added to the next scan result", async () => Print(await ionVision.SetScanResultComment(new Comment("my comment")))) },
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

    await requestDesc.Item2!();
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

void Print<T>(API.Response<T> response)
{
    if (response.Success)
    {
        var text = JsonSerializer.Serialize(response.Value!, new JsonSerializerOptions()
        {
            WriteIndented = true,
        });
        Console.WriteLine(text.Length < MAX_CHARS_TO_PRINT ? text : $"{text[..MAX_CHARS_TO_PRINT]}... and {text.Length - MAX_CHARS_TO_PRINT} chars more.");

        if (response.Value is ScanResult result)
        {
            Console.WriteLine($"{result.MeasurementData.DataPoints} data points");
        }
    }
    else
    {
        Console.WriteLine(response.Error);
    }
}
