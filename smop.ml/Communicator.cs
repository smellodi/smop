using System;
using System.Threading.Tasks;

namespace Smop.ML;

public class Communicator : IDisposable
{
    public enum Type
    {
        Tcp,
        File
    }

    public IonVision.ParameterDefinition? Parameter { get; set; } = null;

    public event EventHandler<Status>? StatusChanged;
    public event EventHandler<Recipe>? RecipeReceived;

    public bool IsConnected => _server.IsClientConnected;
    public string ConnectionMean { get; }


    public Communicator(Type type, bool isSimulating)
    {
        _server = type == Type.Tcp ? new TcpServer() : new FileServer();
        _server.RecipeReceived += Server_RecipeReceived;

        ConnectionMean = type == Type.Tcp ? $"port {TcpServer.Port}" : $"files {FileServer.MLInput}/{FileServer.MLOutput}";

        if (_server is TcpServer tcpServer)
        {
            tcpServer.StatusChanged += (s, e) => StatusChanged?.Invoke(this, e);
        }

        if (isSimulating)
        {
            IonVision.DispatchOnce.Do(1, () =>
            {
                _simulator = type == Type.Tcp ? new TcpSimulator() : new FileSimulator();
            });
        }
    }

    public async Task Config(string[] sources, ChannelProps[] channels, int maxInteractions = 0, float threshold = 0)
    {
        await _server.SendAsync(new Packet(PacketType.Config, new Config(sources, new Printer(channels), maxInteractions, threshold)));
    }

    public async Task Publish(IonVision.ScanResult scan)
    {
        if (Parameter == null)
        {
            throw new Exception("Parameter is not set");
        }

        var packet = new Packet(PacketType.Measurement, DmsMeasurement.From(scan, Parameter));
        await _server.SendAsync(packet);
    }

    public async Task Publish(SmellInsp.Data data)
    {
        var packet = new Packet(PacketType.Measurement, SntMeasurement.From(data));
        await _server.SendAsync(packet);
    }

    public async Task Publish(float pid)
    {
        var packet = new Packet(PacketType.Measurement, PIDMeasurement.From(pid));
        await _server.SendAsync(packet);
    }

    public void Dispose()
    {
        _simulator?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Server _server;

    Simulator? _simulator = null;

    private void Server_RecipeReceived(object? sender, Recipe e)
    {
        RecipeReceived?.Invoke(this, e);
    }
}
