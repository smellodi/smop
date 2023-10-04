using Smop.IonVision;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Test.IonVision;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing IonVision module (SMOP.IonVision)...\n");

// tests the Bland-Altman plot
/*
int sizeX = 150;
int sizeY = 100;
int length = sizeX * sizeY;
float[] data1 = new float[length];
float[] data2 = new float[length];
var rnd = new Random();
for (int i = 0; i < sizeY; i++)
    for (int j = 0; j < sizeX; j++)
    {
        data1[i * sizeX + j] = (float)(12f - 0.1*j - 0.1*i + 0 * rnd.NextDouble());
        data2[i * sizeX + j] = (float)(12 * Math.Exp(-0.0001 * (Math.Pow(i - sizeY / 2, 2) + Math.Pow(j - sizeX / 2, 2))));
    }
DataPlot.Show(sizeX, sizeY, data1, data2);
*/

// Initialization

bool isSimulating = GetMode();
bool isRunning = true;

List<MeasurementData> _data = new();
ParameterDefinition? _paramDefinition = isSimulating ? SimulatedData.ParameterDefinition : null;

var ionVision = new Communicator(null, isSimulating);

if (!await Connect(ionVision))
    return;

await Task.Delay(1);
EventSink events = new(isSimulating ? "127.0.0.1" : ionVision.Settings.IP);
events.ScanStarted += (s, e) => PrintEvent(e.Type, "scan started");
events.ScanProgressChanged += (s, e) => PrintEvent(e.Type, $"scan progress = {e.Data.Progress} %");
events.ScanFinished += (s, e) => PrintEvent(e.Type, "scan finished");
events.ScanResultsProcessed += (s, e) => PrintEvent(e.Type, "scan results are available now");
events.CurrentProjectChanged += (s, e) => PrintEvent(e.Type, $"project = {e.Data.NewProject}");
events.CurrentParameterChanged += (s, e) => PrintEvent(e.Type, $"param = {e.Data.NewParameter.Name}");

(string, string)[] listOfCommands = Array.Empty<(string, string)>();
var commands = new Dictionary<string, (string, Func<Task>)>()
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
    { "scom", ("sets a comment to be added to the next scan result", async () => Print(await ionVision.SetScanResultComment(new { _quickcomment = new string[] { "my comment" } } ))) },
    { "scan", ("starts a new scan", async () => Print(await ionVision.StartScan())) },
    { "p", ("retrieves the scan progress", async () => Print(await ionVision.GetScanProgress())) },
    { "result", ("gets the latest scan result", async () => Print(await ionVision.GetScanResult())) },
    { "help", ("displays available commands", async () => { PrintHelp(listOfCommands); await Task.CompletedTask; }) },
    { "exit", ("exists the app", async () => { isRunning = false; await Task.CompletedTask; }) },
};

listOfCommands = commands.Select(c => (c.Key, c.Value.Item1)).ToArray();
PrintHelp(listOfCommands);
Console.WriteLine();

while (isRunning)
{
    Console.Write("Command: ");
    var cmd = Console.ReadLine();
    if (string.IsNullOrEmpty(cmd)) { continue; }

    if (!commands.TryGetValue(cmd, out var requestDesc))
    {
        Console.WriteLine("Unknown command\n");
        continue;
    }

    await requestDesc.Item2();

    Console.WriteLine();
}

Console.WriteLine("Testing finished.");


// Routines

static bool GetMode()
{
    Console.Write("Should the app enter the simulation mode (y/N)?  ");

    do
    {
        var resp = Console.ReadKey();
        if (resp.Key == ConsoleKey.Y || resp.Key == ConsoleKey.N || resp.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return resp.Key == ConsoleKey.Y;
        }

        Console.CursorLeft--;
    } while (true);
}

static async Task<bool> Connect(Communicator ionVision)
{
    var version = await ionVision.GetSystemInfo();
    if (!version.Success)
    {
        Console.WriteLine("The device is offline");
        return false;
    }
    else if (version.Value!.CurrentVersion != ionVision.SupportedVersion)
    {
        Console.WriteLine();
        Console.WriteLine("=================== WARNING! ===================");
        Console.WriteLine($" This module works with IonVision API v{ionVision.SupportedVersion}");
        Console.WriteLine(" The device API version that is connected now is");
        Console.WriteLine($"                    v{version.Value!.CurrentVersion}");
        Console.WriteLine(" Be prepared to experience errors and exceptions");
        Console.WriteLine("================================================");
    }

    return true;
}

static void PrintHelp((string, string)[] help)
{
    Console.WriteLine("\nAvailable commands:");
    foreach (var cmd in help)
    {
        if (!string.IsNullOrEmpty(cmd.Item1))
        {
            Console.WriteLine($"    {cmd.Item1,-8} - {cmd.Item2}");
        }
    }
}

static void PrintEvent(string type, string msg)
{
    Console.CursorLeft = 0;
    Console.Write($"[EVT]: {type} : {msg}\nCommand: ");
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
        Console.WriteLine(text.Length < MAX_CHARS_TO_PRINT ? text : $"{text[..MAX_CHARS_TO_PRINT]}...\nand {text.Length - MAX_CHARS_TO_PRINT} chars more.");

        if (response.Value is ParameterDefinition paramDefinition)
        {
            _paramDefinition = paramDefinition;
            Console.WriteLine($"COLS x ROWS = {paramDefinition.MeasurementParameters.SteppingControl.Usv.Steps} x {paramDefinition.MeasurementParameters.SteppingControl.Ucv.Steps}");
        }
        else if (response.Value is ScanResult result)
        {
            _data.Add(result.MeasurementData);
            Console.WriteLine($"{result.MeasurementData.DataPoints} data points");

            if (_paramDefinition is not null)
            {
                DataPlot.Show(
                        (int)_paramDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                        (int)_paramDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                        result.MeasurementData.IntensityTop,
                        _data.Count > 1 ? _data[^2].IntensityTop : null
                    );
            }
        }
    }
    else
    {
        Console.WriteLine(response.Error);
    }
}
