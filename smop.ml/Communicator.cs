using Smop.IonVision;
using System;

namespace Smop.ML;

public class Communicator : IDisposable
{
    public enum Type
    {
        Tcp,
        File
    }

    public ParameterDefinition? Parameter { get; set; } = null;

    public event EventHandler<Recipe>? RecipeReceived;

    public bool IsConnected => _server.IsClientConnected;


    public Communicator(Type type, bool isSimulating)
    {
        _server = type == Type.Tcp ? new TcpServer() : new FileServer();
        _server.RecipeReceived += Server_RecipeReceived;

        if (isSimulating)
        {
            _simulator = type == Type.Tcp ? new TcpSimulator() : new FileSimulator();
        }
    }

    public async void Config(string source, ChannelProps[] channels)
    {
        await _server.SendAsync(new Packet(PacketType.Config, new Config(source, new Printer(channels))));
    }

    public async void Publish(ScanResult scan)
    {
        if (Parameter == null)
        {
            throw new Exception("Parameter is not set");
        }

        var packet = new Packet(PacketType.Measurement, DmsMeasurement.From(scan, Parameter));
        await _server.SendAsync(packet);
    }

    public void Dispose()
    {
        _simulator?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Simulator? _simulator = null;
    readonly Server _server;

    private void Server_RecipeReceived(object? sender, Recipe e)
    {
        RecipeReceived?.Invoke(this, e);
    }
}
