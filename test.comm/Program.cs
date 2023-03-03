using SMOP.Comm;
using SMOP.Comm.Packets;
using System.Windows.Threading;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing Smellody Odor Printer (SMOP)...\n");

int linesToScrollUp = 0;
var commands = new Dictionary<string, (string, Request?)>()
{
    { "ver", ("retrieves version", new QueryVersion()) },
    { "devs", ("retrieves attached modules", new QueryDevices()) },
    { "capsb", ("retrieves Base module capabilities", new QueryCapabilities(Device.ID.Base)) },
    { "caps1", ("retrieves Odor1 module capabilities", new QueryCapabilities(Device.ID.Odor1)) },
    { "seta", ("sets odorant flow for Base to 5 l/min, and for Odor1 to 0.1 l/min; opens Base output valve", new SetActuators(new Actuator[]
        {
            new Actuator(Device.ID.Base, new Dictionary<Device.Controller, float>()
            {
                {Device.Controller.OutputValve, 1 },
                {Device.Controller.OdorantFlow, 5.0f }
            }),
            new Actuator(Device.ID.Odor1, new Dictionary<Device.Controller, float>()
            {
                {Device.Controller.OdorantFlow, 0.1f }
            })
        })) },
    { "sets", ("start the fan, disabled PID", new SetSystem(true, false)) },
    { "setm", ("start measurements; press ENTRER to stop it", new SetMeasurements(SetMeasurements.Command.Start)) },
    { "setmo", ("retrieves a measurement once", new SetMeasurements(SetMeasurements.Command.Once)) },
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
    Console.WriteLine($"  {port.Name} ({port.Description}, by {port.Manufacturer})");
}
Console.WriteLine("");

var smopCOMPort = COMUtils.SMOPPort;
if (smopCOMPort != null)
{
    Console.WriteLine($"Looks like {smopCOMPort.Name} is the one you should use");
}


// Open a COM port or start a simulator

//Console.Write("Available ports: ");
//var ports = string.Join(", ", SerialPort.GetPortNames());
//Console.WriteLine(string.IsNullOrEmpty(ports) ? "none" : ports);
//Console.WriteLine("");

CommPort _port = new CommPort();
_port.Opened += (s, e) => Console.WriteLine("[PORT] opened");
_port.Closed += (s, e) => Console.WriteLine("[PORT] closed");
_port.Data += async (s, e) => await Task.Run(() => HandleData(e));
_port.COMError += (s, e) => Console.WriteLine($"[PORT] {e}");
//_port.Debug += async (s, e) => await Task.Run(() => Console.WriteLine($"[PORT] debug: {e}"));

do
{
    Console.WriteLine("Enter COM port number, or leave it blank to start simulation:");
    Console.Write("  COM");

    var com = Console.ReadLine() ?? "";
    var openResult = _port.Open(com);

    Console.WriteLine($"Result: {openResult}\n");

    if (openResult.Error == Error.Success)
        break;

} while (true);


// Print a list of commands

Console.WriteLine("Available commands:");
foreach (var cmd in commands)
{
    if (!string.IsNullOrEmpty(cmd.Key))
    {
        Console.WriteLine($"    {cmd.Key,-8} - {cmd.Value.Item1}");
    }
}
Console.WriteLine("Type a command:");


// Execute commands

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
        break;

    var result = _port.Request(request, out Ack? ack, out Response? resonse);

    Console.WriteLine($"Sent:     {request}");
    Console.WriteLine($"Result:   {result}");
    if (ack != null)
        Console.WriteLine($"Received: {ack}");
    if (result.Error == Error.Success && resonse != null)
        Console.WriteLine("  " + resonse);

    linesToScrollUp = 0;
}


// Exit

_port.Close();

Console.WriteLine("\nTesting finished.");


void HandleData(Data e)
{
    if (Console.CursorLeft > 0)
        Console.WriteLine("\n");
    var line = Console.CursorTop;
    if (linesToScrollUp > 0)
        Console.CursorTop -= linesToScrollUp;
    Console.WriteLine("  " + e);
    if (linesToScrollUp == 0)
        linesToScrollUp = Console.CursorTop - line;
}