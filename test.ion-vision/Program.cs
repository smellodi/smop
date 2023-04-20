using Smop.IonVision;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing IonVision module (SMOP.IonVision)...\n");

Console.Write("Should the app enter the simulation mode (y/N)?   ");

bool isSimulating = true;

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

var version = await ionVision.GetSystemInfo();

if (!version.Success)
{
    Console.WriteLine("The device is offline");
    return;
}
else if (version.Value!.CurrentVersion != "1.5")
{
    Console.WriteLine();
    Console.WriteLine("=================== WARNING! ===================");
    Console.WriteLine(" This module works with IonVision API v1.5");
    Console.WriteLine(" The device API version that is connected now is");
    Console.WriteLine($"                    v{version.Value!.CurrentVersion}");
    Console.WriteLine(" Be prepared to experience errors and exceptions");
    Console.WriteLine("================================================");
}

var commands = new Dictionary<string, (string, Func<Task>?)>()
{
{ "sys", ("retrieves system params", async () => Print(await ionVision.GetSystemStatus())) },
{ "info", ("retrieves system info", async () => Print(await ionVision.GetSystemInfo())) },
{ "gclock", ("retrieves clock", async () => Print(await ionVision.GetClock())) },
{ "sclock", ("sets clock", async () => Print(await ionVision.SetClock())) },
{ "user", ("retrieves user", async () => Print(await ionVision.GetUser())) },
{ "projs", ("retrieves projects", async () => Print(await ionVision.GetProjects())) },
{ "params", ("retrieves parameters", async () => Print(await ionVision.GetParameters())) },
{ "gcpj", ("retrieves the current project", async () => Print(await ionVision.GetProject())) },
{ "scpj", ("sets the current project", async () => Print(await ionVision.SetProjectAndWait())) },
{ "gcpm", ("retrieves the current parameter", async () => Print(await ionVision.GetParameter())) },
{ "gcpmd", ("retrieves the current parameter definition", async () => Print(await ionVision.GetParameterDefinition())) },
{ "scpm", ("sets the current parameter", async () => Print(await ionVision.SetParameterAndPreload())) },
{ "scom", ("sets a comment to be added to the next scan result", async () => Print(await ionVision.SetScanResultComment(new Comments() { Text = "my comment" }))) },
{ "scan", ("starts a new scan", async () => Print(await ionVision.StartScan())) },
{ "p", ("retrieves the scan progress", async () => Print(await ionVision.GetScanProgress())) },
{ "result", ("gets the latest scan result", async () => Print(await ionVision.GetScanResult())) },
{ "help", ("displays available commands", null) },
{ "exit", ("exists the app", null) },
};


PrintHelp();

while (true)
{
    Console.Write("Command: ");
    var cmd = Console.ReadLine();
    if (string.IsNullOrEmpty(cmd)) { continue; }

    if (!commands.TryGetValue(cmd, out var requestDesc))
    {
        Console.WriteLine("Unknown command\n");
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
        else if (cmd == "exit")
        {
            break;
        }
        else throw new Exception($"Unimplemented response to the command '{cmd}'");
    }

    await request();

    Console.WriteLine();
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
    Console.WriteLine();
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
