using Smop.MainApp.Logging;
using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp.Controllers;

public class SetupController
{
    public class LogHandlerArgs(string text, bool replaceLast = false) : EventArgs
    {
        public string Text { get; } = text;
        public bool ReplaceLast { get; } = replaceLast;
    }

    public event EventHandler<LogHandlerArgs>? LogDms;
    public event EventHandler<LogHandlerArgs>? LogSnt;
    public event EventHandler<LogHandlerArgs>? LogOD;
    public event EventHandler<float>? ScanProgress;

    public IonVision.Param.ParameterDefinition? ParamDefinition { get; private set; } = null;
    public IonVision.Defs.ScopeParameters? ScopeParameters { get; private set; } = null;
    public IonVision.Scan.ScanResult? DmsScan { get; private set; } = null;
    public SmellInsp.Data? SntSample { get; private set; } = null;

    public bool HasInitializationFinished { get; private set; } = false;

    public void AcquireOdorChannelsInfo()
    {
        var channelIDs = new List<OdorDisplay.Device.ID>();

        if (COMHelper.ShowErrorIfAny(_odController.QueryDevices(out var devices), "query the printer devices") && devices != null)
        {
            for (int i = 0; i < ODPackets.Devices.MaxOdorModuleCount; i++)
            {
                if (devices.HasOdorModule(i))
                {
                    channelIDs.Add((OdorDisplay.Device.ID)(i + 1));
                }
            }
        }

        _odorChannels = new OdorChannels(channelIDs.ToArray());
    }

    public void EnumOdorChannels(Action<OdorChannel> callback)
    {
        foreach (var odorChannel in _odorChannels)
        {
            callback(odorChannel);
        }
    }

    public void SaveSetup()
    {
        _odorChannels.Save();
    }

    public void ShutDown()
    {
        if (_odorDisplay.IsOpen)
        {
            System.Threading.Thread.Sleep(100);
            LogIO.Add(_odController.StopMeasurements(), "StopMeasurements", LogSource.OD);
        }
    }

    public void InitializeOdorPrinter()
    {
        COMHelper.ShowErrorIfAny(_odController.Init(), "initialize");

        System.Threading.Thread.Sleep(100);
        COMHelper.ShowErrorIfAny(_odController.StartMeasurements(), "start measurements");

        System.Threading.Thread.Sleep(100);
        COMHelper.ShowErrorIfAny(_odController.SetHumidity(Properties.Settings.Default.Reproduction_Humidity), "set default humidity");
    }

    public async void CleanUpOdorPrinter(Action? finishedAction = null)
    {
        LogOD?.Invoke(this, new LogHandlerArgs("Cleaning up: started."));

        var setupFilename = _storage.Simulating.HasFlag(SimulationTarget.OdorDisplay) ? "init-setup-debug.txt" : "init-setup.txt";
        var setup = PulseSetup.Load(System.IO.Path.Combine("Properties", setupFilename));
        if (setup != null)
        {
            using var controller = new PulseController(setup, null);
            controller.StageChanged += (s, e) =>
            {
                HasInitializationFinished = e.Stage.HasFlag(Stage.Finished);
                if (e.Stage.HasFlag(Stage.NewSession))
                    LogOD?.Invoke(this, new LogHandlerArgs($"Cleaning up: Session {e.SessionID}"));
                else if (e.Stage.HasFlag(Stage.Pulse))
                    LogOD?.Invoke(this, new LogHandlerArgs($"Cleaning up: Pulse {e.PulseID}"));
            };

            await Task.Delay(500);
            controller.Start();

            while (!HasInitializationFinished)
                await Task.Delay(200);

            EventLogger.Instance.Clear();
        }

        LogOD?.Invoke(this, new LogHandlerArgs("Cleaning up: finished."));
        var actuators = _odorChannels.Select(odorChannel =>
            new ODPackets.Actuator(odorChannel.ID, new ODPackets.ActuatorCapabilities(
                ODPackets.ActuatorCapabilities.OdorantValveClose,
                KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, 0f)
            ))
        );

        System.Threading.Thread.Sleep(100);
        COMHelper.ShowErrorIfAny(_odController.OpenChannels(actuators.ToArray()), "stop cleaning up");

        finishedAction?.Invoke();
    }

    public async Task<bool> InitializeIonVision(IonVision.Communicator ionVision)
    {
        if (LogIO.Add(await ionVision.SetClock(), "SetClock"))
            LogDms?.Invoke(this, new LogHandlerArgs("Current clock is set."));
        else
            LogDms?.Invoke(this, new LogHandlerArgs("The clock was not set."));

        await Task.Delay(300);

        var projectName = ionVision.Settings.Project;
        LogDms?.Invoke(this, new LogHandlerArgs($"Loading '{projectName}' project..."));

        LogIO.Add(await ionVision.GetProject(), "GetProject", out IonVision.Defs.ProjectAsName? responseGetProject);
        if (responseGetProject?.Project != projectName)
        {
            await Task.Delay(300);
            if (LogIO.Add(await ionVision.SetProjectAndWait(), "SetProjectAndWait"))
            {
                LogDms?.Invoke(this, new LogHandlerArgs($"Project '{projectName}' is loaded.", true));
            }
            else
            {
                LogDms?.Invoke(this, new LogHandlerArgs($"Project '{projectName}' was not loaded... Interrupted.", true));
                return false;
            }
        }
        else
        {
            LogDms?.Invoke(this, new LogHandlerArgs($"Project '{projectName}' is loaded already", true));
        }

        await Task.Delay(300);

        var paramName = ionVision.Settings.ParameterName;
        LogDms?.Invoke(this, new LogHandlerArgs($"Loading '{paramName}' parameter..."));

        LogIO.Add(await ionVision.GetParameter(), "GetParameter", out IonVision.Defs.ParameterAsNameAndId? getParameterResponse);

        bool hasParameterLoaded = getParameterResponse?.Parameter.Id == ionVision.Settings.ParameterId;
        if (!hasParameterLoaded)
        {
            await Task.Delay(300);
            if (LogIO.Add(await ionVision.SetParameterAndPreload(), "SetParameterAndPreload"))
            {
                LogDms?.Invoke(this, new LogHandlerArgs($"Parameter '{paramName}' is set and preloaded.", true));
            }
            else
            {
                LogDms?.Invoke(this, new LogHandlerArgs($"Parameter '{paramName}' was not set... Interrupted", true));
                return false;
            }
        }
        else
        {
            LogDms?.Invoke(this, new LogHandlerArgs($"Parameter '{paramName}' is loaded already.", true));
        }

        if (App.ML != null)
        {
            LogDms?.Invoke(this, new LogHandlerArgs($"Retrieving the parameter..."));
            LogIO.Add(await ionVision.GetParameterDefinition(), "GetParameterDefinition", out IonVision.Param.ParameterDefinition? paramDefinition);
            ParamDefinition = paramDefinition;

            await Task.Delay(300);

            LogIO.Add(await ionVision.GetScopeParameters(), "GetScopeParameters", out IonVision.Defs.ScopeParameters? scopeParameters);
            ScopeParameters = scopeParameters;

            if (paramDefinition != null)
            {
                var sc = paramDefinition.MeasurementParameters.SteppingControl;
                _dmsCache.SetSubfolder(sc.Usv.Steps, sc.Ucv.Steps);
            }

            await Task.Delay(300);
            LogDms?.Invoke(this, new LogHandlerArgs("Ready for scanning the target odor.", true));
        }

        return true;
    }

    public async Task MeasureSample()
    {
        if (!COMHelper.ShowErrorIfAny(_odController.SetHumidity(Properties.Settings.Default.Reproduction_Humidity), "set humidity"))
            return;

        await Task.Delay(150);

        if (!COMHelper.ShowErrorIfAny(_odController.OpenChannels(_odorChannels), "release odors"))
            return;

        var flows = _odorChannels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name) && odorChannel.Name != odorChannel.ID.ToString())
            .Select(odorChannel => odorChannel.Flow);
        var waitingTime = OdorDisplayController.CalcWaitingTime(flows);
        LogOD?.Invoke(this, new LogHandlerArgs($"Odors were released, waiting {waitingTime:F1}s for the mixture to stabilize..."));
        await Task.Delay((int)(waitingTime * 1000));
        LogOD?.Invoke(this, new LogHandlerArgs("The odor is ready."));

        _pidSamples.Clear();

        _odorDisplay.Data += OdorDisplay_Data;

        List<Task> scans = new();
        if (App.IonVision != null)
        {
            scans.Add(MakeDmsScan(App.IonVision));
        }
        else if (_sntDataCollector.IsEnabled)     // remove "else" to measure from BOTH ENOSES
        {
            scans.Add(GetSntSample());
        }

        await Task.WhenAll(scans);

        _odorDisplay.Data -= OdorDisplay_Data;

        if (!COMHelper.ShowErrorIfAny(_odController.CloseChannels(_odorChannels), "stop odors"))
            return;

        await Task.Delay(300);
    }

    public async Task ConfigureML()
    {
        if (App.ML == null)
            return;

        var dataSources = new List<string>();
        if (App.IonVision != null)
        {
            dataSources.Add(ML.Source.DMS);
        }
        else if (_sntDataCollector.IsEnabled)
        {
            dataSources.Add(ML.Source.SNT);
        }
        if (Properties.Settings.Default.Reproduction_UsePID)
        {
            dataSources.Add(ML.Source.PID);
        }

        if (ParamDefinition != null)
        {
            App.ML.Parameter = ParamDefinition;
        }
        if (ScopeParameters != null)
        {
            App.ML.ScopeParameters = ScopeParameters;
        }

        var settings = Properties.Settings.Default;
        _nlog.Info(LogIO.Text("ML", "Config", settings.Reproduction_ML_MaxIterations, settings.Reproduction_ML_Threshold,
            settings.Reproduction_ML_Algorithm));

        await App.ML.Config(dataSources.ToArray(), _odorChannels
                .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
                .Select(odorChannel => new ML.ChannelProps((int)odorChannel.ID, odorChannel.Name, odorChannel.Propeties)).ToArray(),
            settings.Reproduction_ML_MaxIterations,
            settings.Reproduction_ML_Threshold,
            settings.Reproduction_ML_Algorithm
        );

        await Task.Delay(300);

        _nlog.Info(LogIO.Text("ML", "InitialMeasurements"));

        if (DmsScan != null)
        {
            await App.ML.Publish(DmsScan);
        }
        else if (SntSample != null)
        {
            await App.ML.Publish(SntSample);
        }

        if (settings.Reproduction_UsePID)
        {
            foreach (var sample in _pidSamples)
            {
                await App.ML.Publish(sample);
            }
        }
    }

    // Internal

    const int PID_MAX_DATA_COUNT = 30;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(SetupController));

    readonly Storage _storage = Storage.Instance;
    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.DataCollector _sntDataCollector = new();

    readonly OdorDisplayController _odController = new();

    readonly List<float> _pidSamples = new();

    readonly DmsCache _dmsCache = new();

    OdorChannels _odorChannels = new();

    private async Task MakeDmsScan(IonVision.Communicator ionVision)
    {
        DmsScan = _dmsCache.Find(_odorChannels, out string? filename);

        if (DmsScan != null)
        {
            _nlog.Info(LogIO.Text("Cache", "Read", filename));
        }
        else
        {
            if (!LogIO.Add(await ionVision.StartScan(), "StartScan"))
            {
                LogDms?.Invoke(this, new LogHandlerArgs("Failed to start sample scan."));
                return;
            }

            LogDms?.Invoke(this, new LogHandlerArgs("Scanning..."));

            await ionVision.WaitScanToFinish(progress =>
            {
                LogDms?.Invoke(this, new LogHandlerArgs(
                    progress < 100 ?
                    $"Scanning... {progress} %" :
                    "Scanning finished.", true
                ));
                ScanProgress?.Invoke(this, progress);
            });

            await Task.Delay(300);
            LogIO.Add(await ionVision.GetScanResult(), "GetScanResult", out IonVision.Scan.ScanResult? scan);
            DmsScan = scan;

            if (scan != null)
            {
                filename = _dmsCache.Save(_odorChannels, scan);
                if (filename != null)
                {
                    _nlog.Info(LogIO.Text("Cache", "Write", filename));
                }
            }
        }

        if (DmsScan == null)
            LogDms?.Invoke(this, new LogHandlerArgs("Failed to retrieve the scanning result"));
    }

    private async Task GetSntSample()
    {
        LogSnt?.Invoke(this, new LogHandlerArgs($"Collecting SNT samples..."));

        SntSample = await _sntDataCollector.Collect((count, progress) =>
        {
            LogSnt?.Invoke(this, new LogHandlerArgs($"Collected {count} samples...", true));
            ScanProgress?.Invoke(this, progress);
        });

        if (SntSample != null)
        {
            LogSnt?.Invoke(this, new LogHandlerArgs($"Samples collected.", true));
        }
        else
        {
            LogSnt?.Invoke(this, new LogHandlerArgs($"Failed to collect SNT samples.", true));
        }
    }

    private async void OdorDisplay_Data(object? sender, ODPackets.Data data)
    {
        await COMHelper.Do(() =>
        {
            if (_pidSamples.Count < PID_MAX_DATA_COUNT)
            {
                foreach (var measurement in data.Measurements)
                {
                    if (measurement.Device == OdorDisplay.Device.ID.Base &&
                        measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) is ODPackets.PIDValue pid)
                    {
                        _pidSamples.Add(pid.Volts);
                        break;
                    }
                }
            }
        });
    }
}
