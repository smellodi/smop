using Smop.MainApp.Logging;
using Smop.MainApp.Reproducer;
using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp;

public class SetupProcedure
{
    public class LogHandlerArgs(string text, bool replaceLast = false) : EventArgs
    {
        public string Text { get; } = text;
        public bool ReplaceLast { get; } = replaceLast;
    }

    public event EventHandler<LogHandlerArgs>? Log;
    public event EventHandler<LogHandlerArgs>? LogDms;
    public event EventHandler<LogHandlerArgs>? LogSnt;
    public event EventHandler<float>? ScanProgress;

    public IonVision.ParameterDefinition? ParamDefinition { get; private set; } = null;
    public IonVision.ScanResult? DmsScan { get; private set; } = null;

    public bool IsSntScanComplete => _sntSamples.Count >= SNT_MAX_DATA_COUNT;

    public SetupProcedure()
    {
        IonVision.DataPlot.UseLogarithmicScaleInBlandAltman = false;
    }

    public void AcquireGasInfo()
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

        _gases = new Gases(channelIDs.ToArray());
    }

    public void EnumGases(Action<Gas> callback)
    {
        foreach (var gas in _gases.Items)
        {
            callback(gas);
        }
    }

    public void SaveSetup()
    {
        _gases.Save();
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
        var cleanupFLow = 10f;
        var cleanupDuration = 10_000;

        var actuators = _gases.Items.Select(gas =>
            new ODPackets.Actuator(gas.ChannelID, new ODPackets.ActuatorCapabilities(
                ODPackets.ActuatorCapabilities.OdorantValveOpenPermanently,
                KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, cleanupFLow)
            ))
        );

        System.Threading.Thread.Sleep(100);
        COMHelper.ShowErrorIfAny(_odController.ReleaseGases(actuators.ToArray()), "start cleaning up");

        await Task.Delay(cleanupDuration);

        actuators = _gases.Items.Select(gas =>
            new ODPackets.Actuator(gas.ChannelID, new ODPackets.ActuatorCapabilities(
                ODPackets.ActuatorCapabilities.OdorantValveClose,
                KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, 0f)
            ))
        );

        System.Threading.Thread.Sleep(100);
        COMHelper.ShowErrorIfAny(_odController.ReleaseGases(actuators.ToArray()), "stop cleaning up");

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

        LogIO.Add(await ionVision.GetProject(), "GetProject", out IonVision.ProjectAsName? responseGetProject);
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

        LogIO.Add(await ionVision.GetParameter(), "GetParameter", out IonVision.ParameterAsNameAndId? getParameterResponse);

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
            LogIO.Add(await ionVision.GetParameterDefinition(), "GetParameterDefinition", out IonVision.ParameterDefinition? paramDefinition);
            ParamDefinition = paramDefinition;

            if (paramDefinition != null)
            {
                var sc = paramDefinition.MeasurementParameters.SteppingControl;
                _dmsCache.SetSubfolder((int)sc.Usv.Steps, (int)sc.Ucv.Steps);
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

        if (!COMHelper.ShowErrorIfAny(_odController.ReleaseGases(_gases), "release odors"))
            return;

        Log?.Invoke(this, new LogHandlerArgs("Odors were released, waiting for the mixture to stabilize..."));
        await Task.Delay((int)(1000 * Properties.Settings.Default.Reproduction_SniffingDelay));
        Log?.Invoke(this, new LogHandlerArgs("Odor mixturing process has finished.", true));

        _pidSamples.Clear();

        _odorDisplay.Data += OdorDisplay_Data;
        _smellInsp.Data += SmellInsp_Data;

        List<Task> scans = new();
        if (App.IonVision != null)
        {
            scans.Add(MakeDmsScan(App.IonVision));
        }
        else if (_smellInsp.IsOpen)     // remove "else" to measure from BOTH ENOSES
        {
            scans.Add(CollectSntSamples());
        }

        await Task.WhenAll(scans);

        _odorDisplay.Data -= OdorDisplay_Data;
        _smellInsp.Data -= SmellInsp_Data;

        if (!COMHelper.ShowErrorIfAny(_odController.StopGases(_gases), "stop odors"))
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
        else if (_smellInsp.IsOpen)
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

        var settings = Properties.Settings.Default;
        _nlog.Info(LogIO.Text("ML", "Config", settings.Reproduction_MaxIterations, settings.Reproduction_Threshold));

        await App.ML.Config(dataSources.ToArray(),
            _gases.Items
                .Where(gas => !string.IsNullOrWhiteSpace(gas.Name))
                .Select(gas => new ML.ChannelProps((int)gas.ChannelID, gas.Name, gas.Propeties)).ToArray(),
            settings.Reproduction_MaxIterations,
            settings.Reproduction_Threshold
        );

        await Task.Delay(300);

        _nlog.Info(LogIO.Text("ML", "InitialMeasurements"));

        if (DmsScan != null)
        {
            await App.ML.Publish(DmsScan);
        }
        else if (_sntSamples.Count > 0)
        {
            foreach (var sample in _sntSamples)
            {
                await App.ML.Publish(sample);
            }
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

    const int SNT_MAX_DATA_COUNT = 10;
    const int PID_MAX_DATA_COUNT = 30;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(SetupProcedure));

    readonly Storage _storage = Storage.Instance;
    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;
 
    readonly OdorDisplayController _odController = new();

    readonly List<SmellInsp.Data> _sntSamples = new();
    readonly List<float> _pidSamples = new();

    readonly DmsCache _dmsCache = new();

    Gases _gases = new();

    private async Task MakeDmsScan(IonVision.Communicator ionVision)
    {
        DmsScan = _dmsCache.Find(_gases, out string? filename);

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
            LogIO.Add(await ionVision.GetScanResult(), "GetScanResult", out IonVision.ScanResult? scan);
            DmsScan = scan;

            if (scan != null)
            {
                filename = _dmsCache.Save(_gases, scan);
                if (filename != null)
                {
                    _nlog.Info(LogIO.Text("Cache", "Write", filename));
                }
            }
        }

        LogDms?.Invoke(this, new LogHandlerArgs(DmsScan != null ? "Ready to start." : "Failed to retrieve the scanning result"));
    }

    private async Task CollectSntSamples()
    {
        _sntSamples.Clear();

        LogSnt?.Invoke(this, new LogHandlerArgs($"Collecting SNT samples..."));

        while (_sntSamples.Count < SNT_MAX_DATA_COUNT)
        {
            await Task.Delay(1000);
            LogSnt?.Invoke(this, new LogHandlerArgs($"Collected {_sntSamples.Count}/{SNT_MAX_DATA_COUNT} samples...", true));
            ScanProgress?.Invoke(this, 100 * _sntSamples.Count/SNT_MAX_DATA_COUNT);
        }

        LogSnt?.Invoke(this, new LogHandlerArgs($"Samples collected.", true));
        LogSnt?.Invoke(this, new LogHandlerArgs($"Ready to start."));
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

    private async void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        await COMHelper.Do(() =>
        {
            if (_sntSamples.Count < SNT_MAX_DATA_COUNT)
            {
                _sntSamples.Add(data);
            }
        });
    }
}
