using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Smop.ML;

public enum Status { Deactivated, Activated }

public class Communicator : IDisposable
{
    public static bool IsDemo => false;
    public static bool CanLaunchML => System.IO.File.Exists(ML_EXECUTABLE);

    public enum Type
    {
        Tcp,
        File,
        Local
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

    public IonVision.Defs.ParameterDefinition? DmsParameter { get; set; } = null;
    public IonVision.Defs.ScopeParameters? DmsScopeParameters { get; set; } = null;

    public event EventHandler<Status>? StatusChanged;
    public event EventHandler<Recipe>? RecipeReceived;
    public event EventHandler<ErrorEventHandlerArgs>? Error;

    public string CmdParams { get; private set; } = "";
    public bool IsConnected => _server.IsConnected;
    public string ConnectionMean => _server.DisplayName;

    public Communicator(Type type, bool isSimulating)
    {
        _server = type switch
        {
            Type.Tcp => new TcpServer(),
            Type.File => new FileServer(),
            Type.Local => new LocalServer(),
            _ => throw new NotImplementedException()
        };
        _server.RecipeReceived += (s, e) =>
        {
            lock (_lock)
            {
                if (_hasStarted)
                    RecipeReceived?.Invoke(this, e);
                else
                    _firstRecipe = e;
            }
        };
        _server.Error += (s, e) => Error?.Invoke(this, new ErrorEventHandlerArgs(_lastAction, e));

        if (_server is TcpServer tcpServer)
        {
            tcpServer.StatusChanged += (s, e) => StatusChanged?.Invoke(this, e);
        }
        else if (_server is LocalServer localServer)
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                StatusChanged?.Invoke(this, Status.Activated);
            });
        }

        if (isSimulating)
        {
            Common.DispatchOnce.Do(1, () =>
            {
                _simulator = type switch
                {
                    Type.Tcp => new TcpSimulator(),
                    Type.File => new FileSimulator(),
                    _ => null
                };
            });
        }
    }

    public void LaunchMlExe(string cmdParams)
    {
        if (_simulator != null || !CanLaunchML)
        {
            return;
        }

        if (_mlExe != null)
        {
            _mlExe.Kill();
            _mlExe.WaitForExit();
        }

        try
        {
            _mlExe = new Process
            {
                EnableRaisingEvents = true
            };
            _mlExe.StartInfo.FileName = ML_EXECUTABLE;
            _mlExe.StartInfo.Arguments = cmdParams;
            _mlExe.Exited += (s, e) => _mlExe = null;
            _mlExe.Start();

            CmdParams = cmdParams;
        }
        catch (Exception ex)
        {
            Common.ScreenLogger.Print($"[ML] Cannot start ML executable: {ex.Message}");
            _mlExe = null;
        }
    }

    public async Task Config(string[] sources, ChannelProps[] channels, int maxInteractions = 0, float threshold = 0, string algorithm = "")
    {
        _lastAction = "Config";
        _hasStarted = false;

        if (!IsDemo)
        {
            var config = new Config(sources, new Printer(channels), maxInteractions, threshold, algorithm);
            var packet = new Packet(PacketType.Config, config);
            await _server.SendAsync(packet);
        }
        else if (_simulator != null)
        {
            await _server.SendAsync(Array.Empty<float>());
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            _hasStarted = true;
            if (_firstRecipe != null)
            {
                RecipeReceived?.Invoke(this, _firstRecipe);
                _firstRecipe = null;
            }
        }
    }

    public async Task Publish(IonVision.Defs.ScanResult scan)
    {
        if (DmsParameter == null)
        {
            throw new Exception("Parameter is not set");
        }

        _lastAction = "PublishDMS";

        if (IsDemo)
        {
            var packet = new System.Collections.Generic.List<float>()
            {
                DmsParameter.MeasurementParameters.SteppingControl.Usv.Steps,
                DmsParameter.MeasurementParameters.SteppingControl.Ucv.Steps,
            };
            packet.AddRange(scan.MeasurementData.IntensityTop);
            await _server.SendAsync(packet.ToArray());
        }
        else
        {
            var packet = new Packet(PacketType.Measurement, DmsMeasurement.From(scan, DmsParameter));
            await _server.SendAsync(packet);
        }
    }

    public async Task Publish(IonVision.Defs.ScopeResult scan)
    {
        if (DmsScopeParameters == null)
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
            var packet = new Packet(PacketType.Measurement, DmsMeasurementScope.From(scan, DmsScopeParameters));
            await _server.SendAsync(packet);
        }
    }

    public async Task Publish(SmellInsp.FeatureData data)
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

    public void CleanUp()
    {
        _server.CleanUp();
    }

    public void Dispose()
    {
        _lastAction = "Dispose";

        _mlExe?.Dispose();
        _server.Dispose();
        _simulator?.Dispose();

        GC.SuppressFinalize(this);
    }

    // Internal

    const string ML_EXECUTABLE = "smop_ml.exe";

    readonly Server _server;
    readonly System.Threading.Mutex _lock = new();

    Simulator? _simulator = null;
    string _lastAction = "Init";

    Process? _mlExe = null;

    bool _hasStarted = false;
    Recipe? _firstRecipe = null;
}
