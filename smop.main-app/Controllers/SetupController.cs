using Smop.MainApp.Logging;
using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODPackets = Smop.OdorDisplay.Packets;
using IVDefs = Smop.IonVision.Defs;
using System.IO;
using System.Text.Json;
using Smop.MainApp.Dialogs;

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
    public event EventHandler<float>? MeasurementProgress;

    public IVDefs.ParameterDefinition? ParamDefinition { get; private set; } = null;
    public IVDefs.ScopeParameters? ScopeParameters { get; private set; } = null;
    public Common.IMeasurement? DmsScan { get; private set; } = null;
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
    }

    public bool SetHumidityLevel(float humidity)
    {
        System.Threading.Thread.Sleep(100);
        return COMHelper.ShowErrorIfAny(_odController.SetHumidity(humidity), "set humidity");
    }

    public async void CleanUpOdorPrinter(string filename, Action? finishedAction = null)
    {
        LogOD?.Invoke(this, new LogHandlerArgs("Cleaning up: started."));

        var setupFilename = _storage.Simulating.HasFlag(SimulationTarget.OdorDisplay) ? "debug.txt" : $"{filename}.txt";
        var setup = PulseSetup.Load(Path.Combine("Assets", "cleanup", setupFilename));
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

        LogIO.Add(await ionVision.GetProject(), "GetProject", out IVDefs.ProjectAsName? responseGetProject);
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

        LogIO.Add(await ionVision.GetParameter(), "GetParameter", out IVDefs.ParameterAsNameAndId? getParameterResponse);

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
            LogIO.Add(await ionVision.GetParameterDefinition(), "GetParameterDefinition", out IVDefs.ParameterDefinition? paramDefinition);
            ParamDefinition = paramDefinition;

            await Task.Delay(300);

            LogIO.Add(await ionVision.GetScopeParameters(), "GetScopeParameters", out IVDefs.ScopeParameters? scopeParameters);
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
        await Task.Delay(150);

        if (!COMHelper.ShowErrorIfAny(_odController.OpenChannels(_odorChannels), "release odors"))
            return;

        App.IonVision?.SetScanResultComment(new { Pulse = _odorChannels.ToDmsComment() });

        if (!Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay))
            _odorDisplay.Data += OdorDisplay_Data;

        var flows = _odorChannels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name) && odorChannel.Name != odorChannel.ID.ToString())
            .Select(odorChannel => odorChannel.Flow);

        var saturationDuration = OdorDisplayController.CalcSaturationDuration(flows);
        LogOD?.Invoke(this, new LogHandlerArgs($"Odors were released, waiting {saturationDuration:F1}s for the mixture to stabilize..."));
        await Task.Delay((int)(saturationDuration * 1000));
        LogOD?.Invoke(this, new LogHandlerArgs("The odor is ready."));

        _pidSamples.Clear();
        _tempSamples.Clear();

        _canCollectOdorDisplayData = true;

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

        _canCollectOdorDisplayData = false;

        COMHelper.ShowErrorIfAny(_odController.CloseChannels(_odorChannels), "stop odors");

        var cleanupDuration = OdorDisplayController.CalcCleanupDuration(flows);
        LogOD?.Invoke(this, new LogHandlerArgs($"Odors stopped, cleaning up {cleanupDuration:F1}s..."));
        await Task.Delay((int)(cleanupDuration * 1000));
        LogOD?.Invoke(this, new LogHandlerArgs("Done."));

        _odorDisplay.Data -= OdorDisplay_Data;
    }

    public async Task ConfigureML()
    {
        if (App.ML == null)
            return;

        var settings = Properties.Settings.Default;

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
            App.ML.ScopeParameters = ScopeParameters with { Usv = settings.Reproduction_DmsSingleSV };
        }

        _nlog.Info(LogIO.Text(Timestamp.Ms, "ML", "Config", settings.Reproduction_ML_MaxIterations, settings.Reproduction_ML_Threshold,
            settings.Reproduction_ML_Algorithm));

        await App.ML.Config(dataSources.ToArray(), _odorChannels
                .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
                .Select(odorChannel => new ML.ChannelProps((int)odorChannel.ID, odorChannel.Name, odorChannel.Properties.ToDict())).ToArray(),
            settings.Reproduction_ML_MaxIterations,
            settings.Reproduction_ML_Threshold,
            settings.Reproduction_ML_Algorithm
        );

        await Task.Delay(300);

        _nlog.Info(LogIO.Text(Timestamp.Ms, "ML", "InitialMeasurements"));

        if (DmsScan is IVDefs.ScanResult fullScan)
        {
            await App.ML.Publish(fullScan);
        }
        else if (DmsScan is IVDefs.ScopeResult scopeScan)
        {
            await App.ML.Publish(scopeScan);
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

    public Common.IMeasurement? LoadDms(string filename)
    {
        Common.IMeasurement? result = null;

        if (File.Exists(filename) && ParamDefinition != null)
        {
            bool isDmsSingleVS = Properties.Settings.Default.Reproduction_DmsSingleSV > 0;

            int expectedDataSize = isDmsSingleVS ? IVDefs.ScopeParameters.DATA_SIZE : 
                ParamDefinition.MeasurementParameters.SteppingControl.Usv.Steps * ParamDefinition.MeasurementParameters.SteppingControl.Ucv.Steps;

            using StreamReader reader = new(filename);
            var json = reader.ReadToEnd();

            int? loadedDataSize = null;
            if (isDmsSingleVS)
            {
                try
                {
                    var dms = JsonSerializer.Deserialize<IVDefs.ScopeResult>(json);
                    loadedDataSize = dms?.IntensityTop?.Length;
                    result = dms;
                }
                catch { }
            }
            else
            {
                try
                {
                    var dms = JsonSerializer.Deserialize<IVDefs.ScanResult>(json);
                    loadedDataSize = dms?.MeasurementData?.IntensityTop.Length;
                    result = dms;
                }
                catch { }
            }

            if (loadedDataSize != expectedDataSize)
            {
                result = null;
                MsgBox.Error(App.Current.MainWindow.Title, "The DMS data size does not match the size defined in the current parameter");
            }

            DmsScan = result;
        }

        return result;
    }

    public Common.IMeasurement? LoadSnt(string filename)
    {
        Common.IMeasurement? result = null;

        if (File.Exists(filename))
        {
            using StreamReader reader = new(filename);
            var txt = reader.ReadToEnd();
            var lines = txt.Split('\n');

            List<SmellInsp.Data> data = new();
            foreach (var line in lines)
            {
                if (line.StartsWith("start"))
                {
                    var p = line.Split(';');
                    if (p.Length != 67)
                    {
                        continue;
                    }

                    try
                    {
                        data.Add(new SmellInsp.Data(
                            p[1..65].Select(float.Parse).ToArray(),
                            float.Parse(p[65]),
                            float.Parse(p[66])
                        ));
                    }
                    catch { }
                }
            }

            if (data.Count > 0)
            {
                float[] resistances = new float[64];
                float humidity = 0;
                float temperature = 0;
                for (int i = 0; i < data.Count; i++)
                {
                    var record = data[i];
                    for (int j = 0; j < record.Resistances.Length; j++)
                    {
                        resistances[j] += record.Resistances[j];
                    }
                    humidity += record.Humidity;
                    temperature += record.Temperature;
                }

                for (int j = 0; j < resistances.Length; j++)
                {
                    resistances[j] /= data.Count;
                }
                humidity /= data.Count;
                temperature /= data.Count;

                SntSample = new SmellInsp.Data(resistances, temperature, humidity);
                result = SntSample;
            }
        }

        return result;
    }

    public async Task<ChemicalLevel[]?> CheckChemicalLevels()
    {
        var result = new List<ChemicalLevel>();

        _odorDisplay.Data += OdorDisplay_Data;

        var enabledChannels = _odorChannels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name) && odorChannel.Name != odorChannel.ID.ToString());

        float count = 0;
        foreach (var channel in enabledChannels)
        {
            var odorChannels = OdorChannels.From(enabledChannels);
            foreach (var ch in odorChannels)
            {
                ch.Flow = ch.ID == channel.ID ? ChemicalLevel.TestFlow : 0;
            }

            COMHelper.ShowErrorIfAny(_odController.OpenChannels(odorChannels), "release odors");

            var saturationDuration = OdorDisplayController.CalcSaturationDuration(new float[] { 0 });   // use maximum duration
            LogOD?.Invoke(this, new LogHandlerArgs($"{channel.Name} was released, waiting {saturationDuration:F1}s for the mixture to stabilize..."));
            await Task.Delay((int)(saturationDuration * 1000));
            LogOD?.Invoke(this, new LogHandlerArgs("The odor is ready."));

            _pidSamples.Clear();
            _tempSamples.Clear();

            var progress = 100f * (3 * count + 1) / (3 * enabledChannels.Count());
            MeasurementProgress?.Invoke(this, (float)Math.Round(progress));

            _canCollectOdorDisplayData = true;

            await Task.Delay(2000);

            _canCollectOdorDisplayData = false;

            COMHelper.ShowErrorIfAny(_odController.CloseChannels(_odorChannels), "stop the odor");

            progress = 100f * (3 * count + 2) / (3 * enabledChannels.Count());
            MeasurementProgress?.Invoke(this, (float)Math.Round(progress));

            var cleanupDuration = OdorDisplayController.CalcCleanupDuration(new float[] { 0 });     // use the maximum duration
            LogOD?.Invoke(this, new LogHandlerArgs($"The odor was stopped, cleaning up {cleanupDuration:F1}s..."));
            await Task.Delay((int)(cleanupDuration * 1000));
            LogOD?.Invoke(this, new LogHandlerArgs("Done."));

            progress = 100f * (3 * count + 3) / (3 * enabledChannels.Count());
            MeasurementProgress?.Invoke(this, (float)Math.Round(progress));

            var pid = _pidSamples.Average();
            var temp = _tempSamples.Average();
            var pli = new PidLevelInspector();
            
            var expectedPID = pli.ComputePidLevel(channel.Properties.PidCheckLevel, pid, temp);

            result.Add(new ChemicalLevel(channel.Name, expectedPID));

            count += 1;
        }

        _odorDisplay.Data -= OdorDisplay_Data;

        return result.ToArray();
    }

    public string[] GetOdorsWithFlowOutOfRange()
    {
        var enabledChannels = _odorChannels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name) && odorChannel.Name != odorChannel.ID.ToString());

        var knownOdors = new KnownOdors();
        return enabledChannels
            .Select(odorChannel => new ChannelAndProps(odorChannel, knownOdors.GetProps(odorChannel.Name)))
            .Where(chp => chp.Props != null && (chp.Channel.Flow < chp.Props.MinFlow || chp.Channel.Flow > chp.Props.MaxFlow))
            .Select(chp => chp.Channel.Name)
            .ToArray();
    }

    // Internal

    record class ChannelAndProps(OdorChannel Channel, OdorChannelProperties? Props);

    const int PID_MAX_DATA_COUNT = 30;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(SetupController));

    readonly Storage _storage = Storage.Instance;
    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly SmellInsp.DataCollector _sntDataCollector = new();

    readonly OdorDisplayController _odController = new();

    readonly List<float> _pidSamples = new();
    readonly List<float> _tempSamples = new();

    readonly DmsCache _dmsCache = new();

    OdorChannels _odorChannels = new();

    bool _canCollectOdorDisplayData = false;

    private async Task MakeDmsScan(IonVision.Communicator ionVision)
    {
        DmsScan = _dmsCache.Find(_odorChannels, out string? filename);

        if (DmsScan != null)
        {
            _nlog.Info(LogIO.Text(Timestamp.Ms, "Cache", "Read", filename));
        }
        else
        {
            var settings = Properties.Settings.Default;

            if (settings.Reproduction_DmsSingleSV > 0)
            {
                DmsScan = await MeasureDmsSingleSV(ionVision, settings.Reproduction_DmsSingleSV);
            }
            else
            {
                DmsScan = await MeasureDmsFull(ionVision);
            }
        }

        if (DmsScan == null)
            LogDms?.Invoke(this, new LogHandlerArgs("Failed to retrieve the scanning result"));
    }

    private async Task<Common.IMeasurement?> MeasureDmsFull(IonVision.Communicator ionVision)
    {
        if (!LogIO.Add(await ionVision.StartScan(), "StartScan"))
        {
            LogDms?.Invoke(this, new LogHandlerArgs("Failed to start sample scan."));
            return null;
        }

        LogDms?.Invoke(this, new LogHandlerArgs("Scanning..."));

        await ionVision.WaitScanToFinish(progress =>
        {
            LogDms?.Invoke(this, new LogHandlerArgs(
                progress < 100 ?
                $"Scanning... {progress} %" :
                "Scanning finished.", true
            ));
            MeasurementProgress?.Invoke(this, progress);
        });

        await Task.Delay(300);

        LogIO.Add(await ionVision.GetScanResult(), "GetScanResult", out IVDefs.ScanResult? scan);

        if (scan != null)
        {
            var filename = _dmsCache.Save(_odorChannels, scan);
            if (filename != null)
            {
                _nlog.Info(LogIO.Text(Timestamp.Ms, "Cache", "Write", filename));
            }
        }

        return scan;
    }

    private async Task<Common.IMeasurement?> MeasureDmsSingleSV(IonVision.Communicator ionVision, float usv)
    {
        if (!LogIO.Add(await ionVision.GetScopeParameters(), "GetScopeParameters", out IVDefs.ScopeParameters? scopeParams) || scopeParams == null)
        {
            LogDms?.Invoke(this, new LogHandlerArgs("Failed to get scope parameters."));
            return null;
        }

        await Task.Delay(150);
        if (!LogIO.Add(await ionVision.SetScopeParameters(scopeParams with { Usv = usv }), "SetScopeParameters"))
        {
            LogDms?.Invoke(this, new LogHandlerArgs("Failed to set scope parameters."));
            return null;
        }

        await Task.Delay(150);
        if (!LogIO.Add(await ionVision.EnableScopeMode(), "EnableScopeMode"))
        {
            LogDms?.Invoke(this, new LogHandlerArgs("Failed to enabled scope mode."));
            return null;
        }

        LogDms?.Invoke(this, new LogHandlerArgs("Scanning..."));
        await Task.Delay(150);
        var scan = await ionVision.WaitScopeScanToFinish(progress =>
        {
            LogDms?.Invoke(this, new LogHandlerArgs(
                progress < 100 ?
                $"Scanning... {progress} %" :
                "Scanning finished.", true
            ));
            MeasurementProgress?.Invoke(this, progress);
        });

        await Task.Delay(150);
        LogIO.Add(await ionVision.DisableScopeMode(), "DisableScopeMode");

        return scan;
    }

    private async Task GetSntSample()
    {
        LogSnt?.Invoke(this, new LogHandlerArgs($"Collecting SNT samples..."));

        var settings = Properties.Settings.Default;
        _sntDataCollector.SampleCount = settings.Reproduction_SntSampleCount;

        SntSample = await _sntDataCollector.Collect((count, progress) =>
        {
            LogSnt?.Invoke(this, new LogHandlerArgs($"Collected {count} samples...", true));
            MeasurementProgress?.Invoke(this, progress);
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
            _odorDisplayLogger.Add(data);

            if (_canCollectOdorDisplayData && _pidSamples.Count < PID_MAX_DATA_COUNT)
            {
                foreach (var measurement in data.Measurements)
                {
                    if (measurement.Device == OdorDisplay.Device.ID.Base)
                    {
                        if (measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) is ODPackets.Sensor.PID pid)
                            _pidSamples.Add(pid.Volts);
                        if (measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.InputAirHumiditySensor) is ODPackets.Sensor.Humidity humidity)
                            _tempSamples.Add(humidity.Celsius);
                    }
                }
            }
        });
    }
}
