using System;
using System.Threading.Tasks;

namespace Smop.ML;

public class Communicator : IDisposable
{
    public static bool IsDemo => true;

    public enum Type
    {
        Tcp,
        File
    }

    public class ErrorEventHandlerArgs : EventArgs
    {
        public string Action { get; }
        public string Error { get; }
        public ErrorEventHandlerArgs(string action, string error)
        {
            Action = action;
            Error = error;
        }
    }

    public IonVision.ParameterDefinition? Parameter { get; set; } = null;
    public IonVision.ScopeParameters? ScopeParameters { get; set; } = null;

    public event EventHandler<Status>? StatusChanged;
    public event EventHandler<Recipe>? RecipeReceived;
    public event EventHandler<ErrorEventHandlerArgs>? Error;

    public bool IsConnected => _server.IsClientConnected;
    public string ConnectionMean { get; }

    public Communicator(Type type, bool isSimulating)
    {
        _server = type == Type.Tcp ? new TcpServer() : new FileServer();
        _server.RecipeReceived += (s, e) => RecipeReceived?.Invoke(this, e);
        _server.Error += (s, e) => Error?.Invoke(this, new ErrorEventHandlerArgs(_lastAction, e));

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
        _lastAction = "Config";

        if (!IsDemo)
        {
            await _server.SendAsync(new Packet(PacketType.Config, new Config(sources, new Printer(channels), maxInteractions, threshold)));
        }
        else if (_simulator != null)
        {
            await _server.SendAsync(Array.Empty<float>());
        }
    }

    public async Task Publish(IonVision.ScanResult scan)
    {
        if (Parameter == null)
        {
            throw new Exception("Parameter is not set");
        }

        _lastAction = "PublishDMS";

        if (IsDemo)
        {
            var packet = new System.Collections.Generic.List<float>()
            {
                Parameter.MeasurementParameters.SteppingControl.Usv.Steps,
                Parameter.MeasurementParameters.SteppingControl.Ucv.Steps,
            };
            packet.AddRange(scan.MeasurementData.IntensityTop);
            await _server.SendAsync(packet.ToArray());
        }
        else
        {
            var packet = new Packet(PacketType.Measurement, DmsMeasurement.From(scan, Parameter));
            await _server.SendAsync(packet);
        }
    }

    public async Task Publish(IonVision.ScopeResult scan)
    {
        if (ScopeParameters == null)
        {
            throw new Exception("Scope parameter is not set");
        }

        _lastAction = "PublishDMS";

        if (IsDemo)
        {
            var packet = new System.Collections.Generic.List<float>() { 1, scan.IntensityTop.Length };
            packet.AddRange(scan.IntensityTop);
            await _server.SendAsync(packet.ToArray());
        }
        else
        {
            var packet = new Packet(PacketType.Measurement, DmsMeasurementScope.From(scan, ScopeParameters));
            await _server.SendAsync(packet);
        }
    }

    public async Task Publish(SmellInsp.Data data)
    {
        _lastAction = "PublishSNT";

        if (!IsDemo)
        {
            var packet = new Packet(PacketType.Measurement, SntMeasurement.From(data));
            await _server.SendAsync(packet);
        }
    }

    public async Task Publish(float pid)
    {
        _lastAction = "PublishPID";

        if (!IsDemo)
        {
            var packet = new Packet(PacketType.Measurement, PIDMeasurement.From(pid));
            await _server.SendAsync(packet);
        }
    }

    public void Dispose()
    {
        _lastAction = "Dispose";

        _server.Dispose();
        _simulator?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Server _server;

    Simulator? _simulator = null;
    string _lastAction = "Init";
}
