﻿using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System.Diagnostics;
using System.Windows.Threading;

const bool SHOW_PORT_DEBUG = true;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing Multichannel Odor Display communication module (SMOP.Comm)...\n");

int linesToScrollUp = 0;
var commands = new Dictionary<string, (string, Request?)>()
{
    { "ver", ("retrieves version", new QueryVersion()) },
    { "devs", ("retrieves attached modules", new QueryDevices()) },
    { "capsb", ("retrieves Base module capabilities", new QueryCapabilities(Device.ID.Base)) },
    { "caps1", ("retrieves Odor1 module capabilities", new QueryCapabilities(Device.ID.Odor1)) },
    { "seta", ("sets Base odorant flow = 5 l/min, chassis T = 25C, output valve ON, Odor1 flow = 0.1 l/min", new SetActuators(new Actuator[]
        {
            new Actuator(Device.ID.Base, new Dictionary<Device.Controller, float>()
            {
                {Device.Controller.OutputValve, -1 },
                {Device.Controller.OdorantFlow, 5.0f },
                {Device.Controller.ChassisTemperature, 25f },
            }),
            new Actuator(Device.ID.Odor1, new Dictionary<Device.Controller, float>()
            {
                {Device.Controller.OdorantFlow, 0.1f }
            })
        })) },
    { "sets", ("start the fan, disabled PID", new SetSystem(true, false)) },
    { "setm", ("start measurements; press ENTRER to stop it", new SetMeasurements(SetMeasurements.Command.Start)) },
    { "setmo", ("retrieves a measurement once", new SetMeasurements(SetMeasurements.Command.Once)) },
    { "help", ("displays available commands", null) },
    { "exit", ("exists the app", null) },
    { "", ("", new SetMeasurements(SetMeasurements.Command.Stop)) },
};


// COM port listener

COMUtils _com = new();
_com.Inserted += (s, e) => Dispatcher.CurrentDispatcher.Invoke(() =>
{
    Console.WriteLine($"[COM] port '{e.Name}' is available now ({e.Description}, by {e.Manufacturer})");
});

Console.WriteLine("Available ports:");
var ports = COMUtils.Ports;
if (ports.Length == 0)
{
    Console.WriteLine("  none");
}
else foreach (var port in ports)
{
    Console.WriteLine($"  {port.Name} ({port.Description}; {port.Manufacturer})");
}
Console.WriteLine("");

COMUtils.Port? smopCOMPort = COMUtils.SMOPPort;
if (smopCOMPort != null)
{
    Console.WriteLine($"Looks like {smopCOMPort.Name} is the port you should use\n");
}


// Open a COM port or start a simulator`

var _port = new CommPort();
_port.Opened += (s, e) => Console.WriteLine("[PORT] opened");
_port.Closed += (s, e) => Console.WriteLine("[PORT] closed");
_port.Data += async (s, e) => await Task.Run(() => PrintData(e));
_port.COMError += (s, e) => Console.WriteLine($"[PORT] {e}");

if (SHOW_PORT_DEBUG)
    //_port.Debug += async (s, e) => await Task.Run(() => PrintDebug(e));
    _port.Debug += (s, e) => PrintDebug(e);

do
{
    Console.WriteLine("Enter COM port number, or leave it blank to start simulation:");
    Console.Write("  COM");

    var com = Console.ReadLine() ?? "";
    if (!string.IsNullOrEmpty(com))
        com = "COM" + com;

    var openResult = _port.Open(com);

    Console.WriteLine($"Result: {openResult}\n");

    if (openResult.Error == Error.Success)
        break;

} while (true);


// Execute commands

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

    var result = _port.Request(request, out Ack? ack, out Response? response);

    Console.WriteLine($"Sent:     {request}");
    Console.WriteLine($"Result:   {result}");
    if (ack != null)
        Console.WriteLine($"Received: {ack}");
    if (result.Error == Error.Success && response != null)
        Console.WriteLine("  " + response);

    linesToScrollUp = 0;
}


// Exit

_port.Close();

Console.WriteLine("\nTesting finished.");


void PrintData(Data e)
{
    var line = Console.CursorTop;
    if (Console.CursorLeft > 0)
        Console.WriteLine("\n");
    if (!SHOW_PORT_DEBUG && linesToScrollUp > 0)
    {
        Console.CursorTop -= linesToScrollUp;
    }
    Console.WriteLine("  " + e);
    Console.Write("\nCommand: ");
    if (!SHOW_PORT_DEBUG && linesToScrollUp == 0)
    {
        linesToScrollUp = Console.CursorTop - line;
        Debug.WriteLine(linesToScrollUp);
    }
}

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

void PrintDebug(string str)
{
    if (Console.CursorLeft > 0)
        Console.WriteLine("");
    Console.WriteLine($"[PORT] debug: {str}");
}