// Based on Test.OdorDisplay

//#define SHOW_PORT_DEBUG

using Smop.SmellInsp;
using System.Windows.Threading;

Console.Title = "Smellody Odor Printer (SMOP)";
Console.WriteLine("Testing Smell Inspector communication module (SMOP.SmellInsp)...\n");

#if !SHOW_PORT_DEBUG
int linesToScrollUp = 0;
#endif

var commands = new Dictionary<string, (string, Command?)>()
{
    { "0", ("fan 0", Command.FAN0) },
    { "1", ("fan 1", Command.FAN1) },
    { "2", ("fan 2", Command.FAN2) },
    { "3", ("fan 3", Command.FAN3) },
    { "i", ("retrieves info", Command.GET_INFO) },
    { "h", ("displays available commands", null) },
    { "e", ("exists the app", null) }
};


// COM port listener

Smop.OdorDisplay.COMUtils _com = new();
_com.Inserted += (s, e) => Dispatcher.CurrentDispatcher.Invoke(() =>
{
    Console.WriteLine($"[COM] port '{e.Name}' is available now ({e.Description}, by {e.Manufacturer})");
});

Console.WriteLine("Available ports:");
var ports = Smop.OdorDisplay.COMUtils.Ports;
if (ports.Length == 0)
{
    Console.WriteLine("  none");
}
else foreach (var port in ports)
    {
        Console.WriteLine($"  {port.Name} ({port.Description}; {port.Manufacturer})");
    }
Console.WriteLine("");


// Open a COM port or start a simulator`

var _port = new CommPort();
_port.Opened += (s, e) => Console.WriteLine("[PORT] opened");
_port.Closed += (s, e) => Console.WriteLine("[PORT] closed");
_port.Data += async (s, e) => await Task.Run(() => PrintData(e));
_port.DeviceInfo += async (s, e) => await Task.Run(() => PrintData(e));
_port.COMError += (s, e) => Console.WriteLine($"[PORT] {e}");

#if SHOW_PORT_DEBUG
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

    if (openResult.Error == Smop.OdorDisplay.Error.Success)
        break;

} while (true);


// Execute commands

PrintHelp();

while (true)
{
    Console.Write("\nCommand: ");
    var cmd = Console.ReadLine() ?? "";
    if (!HandleCommand(cmd))
    {
        break;
    }
}


// Exit

_port.Close();

Console.WriteLine("\nTesting finished.");


bool HandleCommand(string cmd)
{
    if (!commands.TryGetValue(cmd, out var requestDesc))
    {
        Console.WriteLine("Unknown command");
        return true;
    }

    var request = requestDesc.Item2;
    if (request == null)
    {
        if (cmd == "help")
        {
            PrintHelp();
            return true;
        }
        else
        {
            return false;
        }
    }

    var result = _port.Send((Command)request);

    Console.WriteLine($"Sent:     {request}");
    Console.WriteLine("  " + result.Reason);

#if !SHOW_PORT_DEBUG
    linesToScrollUp = 0;
#endif

    return true;
}

void PrintData(object e)
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