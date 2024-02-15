using Smop.IonVision;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing IonVision module (SMOP.IonVision)...\n");

const int MAX_CHARS_TO_PRINT = 700;

KeyValuePair<double, Color>[] PLOT_THEME = new Dictionary<double, Color>()
{
    { 0, Colors.White },
    { 0.1, Colors.Cyan },
    { 0.2, Colors.Green },
    { 0.5, Colors.Orange },
    { 0.75, Colors.Red },
    { 1, Colors.Fuchsia },
}.ToArray();

// tests the Bland-Altman plot
/*
int sizeX = 150;
int sizeY = 100;
int length = sizeX * sizeY;
float[] data1 = new float[length];
float[] data2 = new float[length];
var rnd = new Random();
for (int y = 0; y < sizeY; y++)
    for (int x = 0; x < sizeX; x++)
    {
        data1[y * sizeX + x] = (float)(12f - 0.03 * x - 0.031 * y + 0.05 * rnd.NextDouble());
        data2[y * sizeX + x] = (float)(12f - 0.031 * x - 0.03 * y + 0.05 * rnd.NextDouble());
        //data2[y * sizeX + x] = (float)(12 * Math.Exp(-0.0001 * (Math.Pow(y - sizeY / 2, 2) + Math.Pow(x - sizeX / 2, 2))));
    }
//DataPlot.OperationWith2Sets = DataPlot.ComparisonOperation.Difference;
//DataPlot.Show(sizeX, sizeY, data1);
//DataPlot.Show(sizeX, sizeY, data2);
DataPlot.Show(sizeX, sizeY, data1, data2);
*/

// Initialization

bool isSimulating = GetMode();
bool isRunning = true;
bool isOutputCutEnabled = true;

List<MeasurementData> scanDataList = new();
ParameterDefinition? scanParamDefinition = isSimulating ? SimulatedData.ParameterDefinition : null;
List<ScopeResult> scopeDataList = new();
ScopeParameters? scopeParamDefinition = isSimulating ? SimulatedData.ScopeParameters : null;

var ionVision = new Communicator("IonVision-Tietotalo.json", isSimulating);
ionVision.ScanProgress += (s, e) => Print(new API.Response<ScanProgress>(new(e, new()), null));
ionVision.ScopeResult += (s, e) => scopeDataList.Add(e);

if (!await Connect(ionVision))
    return;

/*
await Task.Delay(1);
EventSink events = new(isSimulating ? "127.0.0.1" : ionVision.Settings.IP);
events.ScanStarted += (s, e) => PrintEvent(e.Type, "scan started");
events.ScanProgressChanged += (s, e) => PrintEvent(e.Type, $"scan progress = {e.Data.Progress} %");
events.ScanFinished += (s, e) => PrintEvent(e.Type, "scan finished");
events.ScanResultsProcessed += (s, e) => PrintEvent(e.Type, "scan results are available now");
events.CurrentProjectChanged += (s, e) => PrintEvent(e.Type, $"project = {e.Data.NewProject}");
events.CurrentParameterChanged += (s, e) => PrintEvent(e.Type, $"param = {e.Data.NewParameter.Name}");
*/

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
    { "gpj", ("retrieves the current project", async () => Print(await ionVision.GetProject())) },
    { "spj", ("sets the current project", async () => Print(await ionVision.SetProjectAndWait())) },
    { "gpm", ("retrieves the current parameter", async () => Print(await ionVision.GetParameter())) },
    { "gpmd", ("retrieves the current parameter definition", async () => Print(await ionVision.GetParameterDefinition())) },
    { "spm", ("sets the current parameter", async () => Print(await ionVision.SetParameterAndPreload())) },
    { "com", ("sets a comment to be added to the next scan result", async () => 
        Print(await ionVision.SetScanResultComment(new { _quickcomment = new string[] { "my comment" } } ))) },
    { "scan", ("starts a new scan", async () => Print(await ionVision.StartScan())) },
    { "p", ("retrieves the scan progress", async () => Print(await ionVision.GetScanProgress())) },
    { "result", ("gets the latest scan result", async () => Print(await ionVision.GetScanResult())) },
    { "plot", ("shows the last result as a plot", async () => {
        ShowPlot(Plot.ComparisonOperation.None);
        await Task.CompletedTask;
    }) },
    { "plotd", ("shows the difference plot for the two last scans", async () => {
        ShowPlot(Plot.ComparisonOperation.Difference);
        await Task.CompletedTask;
    }) },
    { "plotba", ("shows the plot Bland-Altman for the two last scans", async () => {
        ShowPlot(Plot.ComparisonOperation.BlandAltman);
        await Task.CompletedTask;
    }) },
    { "all", ("a combnation of scpj, scpm, gcpmd, scan, result and plot", async () => await GetNewScan()) },
    { "sc", ("gets scope mode", async () => Print(await ionVision.CheckScopeMode())) },
    { "scon", ("enables scope mode", async () => Print(await ionVision.EnableScopeMode())) },
    { "sq", ("disabled scope mode", async () => Print(await ionVision.DisableScopeMode())) },
    { "scr", ("gets the latest scope result", async () => Print(await ionVision.GetScopeResult())) },
    { "scgp", ("retrieves scope parameters", async () => Print(await ionVision.GetScopeParameters())) },
    { "scsp", ("sets scope parameters", async () => 
        Print(await ionVision.SetScopeParameters(SimulatedData.ScopeParameters with { Usv = 500 }))) },
    { "scplot", ("shows the last scope result as a plot", async () => {
        ShowScopePlot(Plot.ComparisonOperation.None);
        await Task.CompletedTask;
    }) },
    { "scplotd", ("shows the difference plot for the two last scope data", async () => {
        ShowScopePlot(Plot.ComparisonOperation.Difference);
        await Task.CompletedTask;
    }) },
    { "scall", ("a combnation of scsp, scon, scoff, and scplot", async () => await GetNewScopeScan()) },
    { "help", ("displays available commands", async () => { PrintHelp(listOfCommands); await Task.CompletedTask; }) },
    { "tout", ($"toggle output cut to {MAX_CHARS_TO_PRINT} chars", async () => { ToggleOutputCut(); await Task.CompletedTask; }) },
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
    else if (!version.Value?.CurrentVersion.StartsWith(ionVision.SupportedVersion) ?? false)
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

async Task GetNewScan()
{
    await ionVision.SetProjectAndWait();
    await Task.Delay(150);
    await ionVision.SetParameterAndPreload();
    await Task.Delay(150);
    await ionVision.GetParameterDefinition();
    await Task.Delay(150);
    await ionVision.StartScan();

    API.Response<ScanProgress> progress;
    do
    {
        await Task.Delay(1000);
        progress = await ionVision.GetScanProgress();
    } while (progress.Success);

    await Task.Delay(150);
    var response = await ionVision.GetScanResult();
    if (response.Value != null)
    {
        scanDataList.Add(response.Value.MeasurementData);
        ShowPlot(Plot.ComparisonOperation.None);
    }
}

async Task GetNewScopeScan()
{
    await ionVision.SetScopeParameters(SimulatedData.ScopeParameters with { Usv = 500 });

    await Task.Delay(150);
    var result = await ionVision.EnableScopeMode();
    if (!result.Success)
        return;

    scopeDataList.Clear();

    while (scopeDataList.Count < 1)
    {
        await Task.Delay(100);
    }

    await ionVision.DisableScopeMode();

    ShowScopePlot(Plot.ComparisonOperation.None);
}

void ShowPlot(Plot.ComparisonOperation operation)
{
    if (scanParamDefinition == null)
    {
        Console.Write($"Parameter definition is uknown (use 'gpmd' command to retrieve it)");
        return;
    }

    switch (operation)
    {
        case Plot.ComparisonOperation.None:
            if (scanDataList.Count > 0)
               new Plot().Show(
                    (int)scanParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                    (int)scanParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                    scanDataList[^1].IntensityTop,
                    theme: PLOT_THEME
                );
            else
                Console.Write($"No scan results retrieved yet (use 'result' command to retrieve it)");
            break;
        case Plot.ComparisonOperation.Difference:
        case Plot.ComparisonOperation.BlandAltman:
            if (scanDataList.Count > 1)
            {
                var plot = new Plot() { UseLogarithmicScaleInBlandAltman = false };
                plot.Show(
                    (int)scanParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps,
                    (int)scanParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps,
                    scanDataList[^1].IntensityTop,
                    scanDataList[^2].IntensityTop,
                    operation
                );
            }
            else
                Console.Write($"At least 2 scan results must be retrieved (use 'scan' and 'result' commands)");
            break;
    }
}

void ShowScopePlot(Plot.ComparisonOperation operation)
{
    if (operation == Plot.ComparisonOperation.None)
    {
        if (scopeDataList.Count > 0)
            new Plot().Show(1,
                scopeDataList[^1].IntensityTop.Length,
                scopeDataList[^1].IntensityTop,
                theme: PLOT_THEME
            );
        else
            Console.Write("No scope results retrieved yet");
    }
    else if (operation == Plot.ComparisonOperation.Difference)
    {
        if (scopeDataList.Count > 1)
            new Plot().Show(1,
                scopeDataList[^1].IntensityTop.Length,
                scopeDataList[^1].IntensityTop,
                scopeDataList[^2].IntensityTop,
                theme: PLOT_THEME
            );
        else
            Console.Write("At least 2 scope scan must be completed");
    }
    else
    {
        Console.Write($"Operation '{operation}' not supported for plotting scope data");
    }
}

void ToggleOutputCut()
{
    isOutputCutEnabled = !isOutputCutEnabled;
    Console.Write("Output cut: " + (isOutputCutEnabled ? $"Enabled (max {MAX_CHARS_TO_PRINT} chars)" : "Disabled") + "\n");
}

/*static void PrintEvent(string type, string msg)
{
    Console.CursorLeft = 0;
    Console.Write($"[EVT]: {type} : {msg}\nCommand: ");
}*/

void Print<T>(API.Response<T> response)
{
    if (response.Success)
    {
        var text = JsonSerializer.Serialize(response.Value!, new JsonSerializerOptions()
        {
            WriteIndented = true,
        });

        if (isOutputCutEnabled)
            Console.WriteLine(text.Length < MAX_CHARS_TO_PRINT ? text : $"{text[..MAX_CHARS_TO_PRINT]}...\nand {text.Length - MAX_CHARS_TO_PRINT} chars more.");
        else
            Console.WriteLine(text);

        if (response.Value is ParameterDefinition paramDefinition)
        {
            scanParamDefinition = paramDefinition;
            Console.WriteLine($"COLS x ROWS = {paramDefinition.MeasurementParameters.SteppingControl.Usv.Steps} x {paramDefinition.MeasurementParameters.SteppingControl.Ucv.Steps}");
        }
        else if (response.Value is ScanResult result)
        {
            scanDataList.Add(result.MeasurementData);
            Console.WriteLine($"{result.MeasurementData.DataPoints} data points");
        }
        else if (response.Value is ScopeParameters scopeParameters)
        {
            scopeParamDefinition = scopeParameters;
        }
    }
    else
    {
        Console.WriteLine(response.Error);
    }
}
