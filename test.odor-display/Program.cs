//#define SHOW_PORT_DEBUG

using Smop.Common;
using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System.Windows.Threading;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing Multichannel Odor Display communication module (SMOP.OdorDisplay)...\n");

#if !SHOW_PORT_DEBUG
int linesToScrollUp = 0;
#endif
var commands = new Dictionary<string, (string, Request?)>()
{
    { "ver", ("retrieves version", new QueryVersion()) },
    { "devs", ("retrieves attached modules", new QueryDevices()) },
    { "capsb", ("retrieves Base module capabilities", new QueryCapabilities(Device.ID.Base)) },
    { "capsd", ("retrieves Dilution module capabilities", new QueryCapabilities(Device.ID.DilutionAir)) },
    { "caps1", ("retrieves Odor1 module capabilities", new QueryCapabilities(Device.ID.Odor1)) },
    { "init", ("starts the fan, enables PID", new SetSystem(true, true)) },
    { "bon", ("turns Base ON [humid air = 1.5L/min, dry air = 8.5L/min, both valves ON]", new SetActuators(new Actuator[]
        {
            new(Device.ID.Base, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 1.5f),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, 8.5f),
                ActuatorCapabilities.OdorantValveOpenPermanently,
                ActuatorCapabilities.OutputValveOpenPermanently
            )),
        })) },
    { "boff", ("turns Base OFF", new SetActuators(new Actuator[]
        {
            new(Device.ID.Base, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0.0f),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, 0.0f),
                ActuatorCapabilities.OdorantValveClose,
                ActuatorCapabilities.OutputValveClose
            )),
        })) },
    { "oon1", ("turns Odor1 ON for 2 seconds [flow = 10 sccm]", new SetActuators(new Actuator[]
        {
            new(Device.ID.Odor1, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 10f),
                KeyValuePair.Create(Device.Controller.OdorantValve, 2000f)
            ))
        })) },
    { "oon2", ("turns Odor1 ON [flow = 40 sccm]", new SetActuators(new Actuator[]
        {
            new(Device.ID.Odor1, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 40f),
                ActuatorCapabilities.OdorantValveOpenPermanently
            ))
        })) },
    { "ooff2", ("turns Odor1 OFF", new SetActuators(new Actuator[]
        {
            new(Device.ID.Odor1, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OutputValveClose
            ))
        })) },
    { "oon3", ("turns Odor[1-3] ON [flow = 50 sccm]", new SetActuators(new Actuator[]
        {
            new(Device.ID.Odor1, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 50f),
                ActuatorCapabilities.OdorantValveOpenPermanently
            )),
            new(Device.ID.Odor2, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 50f),
                ActuatorCapabilities.OdorantValveOpenPermanently
            )),
            new(Device.ID.Odor3, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 50f),
                ActuatorCapabilities.OdorantValveOpenPermanently
            ))
        })) },
    { "ooff3", ("turns Odor[1-3] OFF", new SetActuators(new Actuator[]
        {
            new(Device.ID.Odor1, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            )),
            new(Device.ID.Odor2, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            )),
            new(Device.ID.Odor3, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            )),
        })) },
    { "dpon", ("turn the Dilution pump ON", new SetActuators(new Actuator[]
        {
            new(Device.ID.DilutionAir, new ActuatorCapabilities(
                ActuatorCapabilities.OutputValveOpenPermanently
            ))
        })) },
    { "dpoff", ("turn the Dilution pump OFF", new SetActuators(new Actuator[]
        {
            new(Device.ID.DilutionAir, new ActuatorCapabilities(
                ActuatorCapabilities.OutputValveClose
            ))
        })) },
    { "don", ("turns Dilution ON [10 L/min + 100 sccm]", new SetActuators(new Actuator[]
        {
            new(Device.ID.DilutionAir, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 10f),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, 100f),
                ActuatorCapabilities.OdorantValveOpenPermanently
            ))
        })) },
    { "doff", ("turns Dilution OFF", new SetActuators(new Actuator[]
        {
            new(Device.ID.DilutionAir, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            ))
        })) },
    { "start", ("start measurements; press ENTRER to stop it", new SetMeasurements(SetMeasurements.Command.Start)) },
    { "once", ("retrieves a measurement once", new SetMeasurements(SetMeasurements.Command.Once)) },
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

COMUtils.Port? smopCOMPort = COMUtils.OdorDisplayPort;
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

CommPort.SamplingInterval = 2000;

#if SHOW_PORT_DEBUG
    //_port.Debug += async (s, e) => await Task.Run(() => PrintDebug(e));
    _port.Debug += (s, e) => PrintDebug(e);
#endif

do
{
    Console.WriteLine("Enter COM port number, or leave it blank to start simulation:");
    Console.Write("  COM");

    var com = Console.ReadLine() ?? "";
    if (!string.IsNullOrEmpty(com))
        com = "COM" + com;

    var openResult = _port.Open(com);

    Console.WriteLine($"Result: {openResult}\n");

    if (openResult.Error == Smop.Comm.Error.Success)
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
    if (result.Error == Smop.Comm.Error.Success && response != null)
        Console.WriteLine("  " + response);

#if !SHOW_PORT_DEBUG
    linesToScrollUp = 0;
#endif
}


// Exit

_port.Close();
_com.Dispose();

Console.WriteLine("\nTesting finished.");


void PrintData(Data e)
{
    var line = Console.CursorTop;
    if (Console.CursorLeft > 0)
        Console.WriteLine("\n");
#if !SHOW_PORT_DEBUG
    if (linesToScrollUp > 0)
    {
        Console.CursorTop -= linesToScrollUp;
    }
#endif
    Console.WriteLine("  " + e);
    Console.Write("\nCommand: ");
#if !SHOW_PORT_DEBUG
    if (linesToScrollUp == 0)
    {
        linesToScrollUp = Console.CursorTop - line;
        System.Diagnostics.Debug.WriteLine(linesToScrollUp);
    }
#endif
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

#if SHOW_PORT_DEBUG
void PrintDebug(string str)
{
    if (Console.CursorLeft > 0)
        Console.WriteLine("");
    Console.WriteLine($"[PORT] debug: {str}");
}
#endif