using SMOP.Comm;
using SMOP.Comm.Packets;
using System.IO.Ports;

int linesToScrollUp = 0;
var commands = new Dictionary<string, Request?>()
{
    { "ver", new QueryVersion() },
    { "devs", new QueryDevices() },
    { "capsb", new QueryCapabilities(Device.ID.Base) },
    { "caps1", new QueryCapabilities(Device.ID.Odor1) },
    { "seta", new SetActuators(new Actuator[]
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
        }) },
    { "sets", new SetSystem(true, false) },
    { "setm", new SetMeasurements(SetMeasurements.Command.Start) },
    { "setmo", new SetMeasurements(SetMeasurements.Command.Once) },
    { "exit", null },
    { "", new SetMeasurements(SetMeasurements.Command.Stop) },
};

// Port opening

Console.Title = "Smellody Odor Presenter (SMOP)";
Console.WriteLine("Testing Smellody Odor Presenter (SMOP)...\n");
Console.Write("Available ports: ");
var ports = string.Join(", ", SerialPort.GetPortNames());
Console.WriteLine(string.IsNullOrEmpty(ports) ? "none" : ports);
Console.WriteLine("");

CommPort _port = new CommPort();
_port.Opened += (s, e) => Console.WriteLine("[PORT] opened");
_port.Closed += (s, e) => Console.WriteLine("[PORT] closed");
_port.Data += async (s, e) => await Task.Run(() => HandleData(e));
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

// Executing commands

Console.WriteLine("Available commands:");
foreach (var cmd in commands)
{
    Console.WriteLine("    " + cmd.Key);
}
Console.WriteLine("Type a command, or press ENTER to stop data flow (if started), or type 'exit' to atop the app:");

while (true)
{
    Console.Write("\nCommand: ");
    var cmd = Console.ReadLine() ?? "";
    if (!commands.TryGetValue(cmd, out var request))
    {
        Console.WriteLine("Unknown command");
        continue;
    }

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