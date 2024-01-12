﻿using Smop.OdorDisplay.Packets;
using Smop.MainApp.Generator;
using Smop.MainApp.Reproducer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.MainApp;

public class SetupProcedure
{
    public class LogHanddlerArgs : EventArgs
    {
        public string Text { get; }
        public bool ReplaceLast { get; }
        public LogHanddlerArgs(string text, bool replaceLast = false)
        {
            Text = text;
            ReplaceLast = replaceLast;
        }
    }

    public event EventHandler<LogHanddlerArgs>? Log;
    public event EventHandler<LogHanddlerArgs>? LogDms;
    public event EventHandler<LogHanddlerArgs>? LogSnt;

    public IonVision.ParameterDefinition? ParamDefinition { get; private set; } = null;
    public IonVision.ScanResult? SampleScan { get; private set; } = null;

    public bool IsSntScanComplete => _sntSamples.Count >= SNT_MAX_DATA_COUNT;

    public void EnumGases(Action<Gas> callback)
    {
        var channelIDs = GetAvailableChannelIDs();
        _gases = new Gases(channelIDs);

        foreach (var gas in _gases.Items)
        {
            callback(gas);
        }
    }

    public void Finalize()
    {
        _gases.Save();
    }

    public void ShutDown()
    {
        if (_odorDisplay.IsOpen)
        {
            var queryMeasurements = new SetMeasurements(SetMeasurements.Command.Stop);
            SendOdorDisplayRequest(queryMeasurements);
        }
    }

    public void InitializeOdorPrinter()
    {
        var odController = new OdorDisplayController();
        HandleOdorDisplayError(odController.Init(), "initialize");

        System.Threading.Thread.Sleep(100);
        HandleOdorDisplayError(odController.Start(), "start measurements");
    }

    public async Task<bool> InitializeIonVision(IonVision.Communicator ionVision)
    {
        HandleIonVisionError(await ionVision.SetClock(), "SetClock");

        await Task.Delay(300);
        LogDms?.Invoke(this, new LogHanddlerArgs("Current clock is set."));
        LogDms?.Invoke(this, new LogHanddlerArgs($"Loading '{ionVision.Settings.Project}' project..."));

        var response = HandleIonVisionError(await ionVision.GetProject(), "GetProject");
        if (response.Value?.Project != ionVision.Settings.Project)
        {
            await Task.Delay(300);
            HandleIonVisionError(await ionVision.SetProjectAndWait(), "SetProjectAndWait");

            bool isProjectLoaded = false;
            while (!isProjectLoaded)
            {
                await Task.Delay(1000);
                response = await ionVision.GetProject();
                if (response.Success)
                {
                    isProjectLoaded = true;
                }
                else if (response.Error?.StartsWith("Request failed") ?? false)
                {
                    _nlog.Info("loading a project...");
                }
                else
                {
                    // impossible if the project exists
                }
            }
        }

        LogDms?.Invoke(this, new LogHanddlerArgs($"Project '{ionVision.Settings.Project}' is loaded.", true));

        await Task.Delay(500);

        LogDms?.Invoke(this, new LogHanddlerArgs($"Loading '{ionVision.Settings.ParameterName}' parameter..."));

        var getParameterResponse = await ionVision.GetParameter();
        bool hasParameterLoaded = getParameterResponse.Value?.Parameter.Id == ionVision.Settings.ParameterId;
        if (!hasParameterLoaded)
        {
            await Task.Delay(300);
            HandleIonVisionError(await ionVision.SetParameterAndPreload(), "SetParameterAndPreload");

            LogDms?.Invoke(this, new LogHanddlerArgs($"Parameter '{ionVision.Settings.ParameterName}' is set. Preloading...", true));
            await Task.Delay(1000);
        }

        LogDms?.Invoke(this, new LogHanddlerArgs($"Parameter '{ionVision.Settings.ParameterName}' is set and preloaded.", true));

        if (App.ML != null)
        {
            LogDms?.Invoke(this, new LogHanddlerArgs($"Retrieving the parameter..."));
            var paramDefinition = HandleIonVisionError(await ionVision.GetParameterDefinition(), "GetParameterDefinition");
            ParamDefinition = paramDefinition.Value;

            await Task.Delay(500);
            LogDms?.Invoke(this, new LogHanddlerArgs("Ready for scanning the target odor.", true));
        }

        return true;
    }

    public async Task MeasureSample()
    {
        var actuators = _gases.Items
            .Select(gas => new Actuator(gas.ChannelID, new ActuatorCapabilities(
                KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, gas.Flow),
                gas.Flow > 0 ? ActuatorCapabilities.OdorantValveOpenPermanently : ActuatorCapabilities.OdorantValveClose
            )));
        SendOdorDisplayRequest(new SetActuators(actuators.ToArray()));

        Log?.Invoke(this, new LogHanddlerArgs("Odors were released, waiting for the mixture to stabilize..."));
        await Task.Delay((int)(1000 * Properties.Settings.Default.Reproduction_SniffingDelay));
        Log?.Invoke(this, new LogHanddlerArgs("Odor mixturing process has finished", true));

        _pidSamples.Clear();

        _odorDisplay.Data += OdorDisplay_Data;
        _smellInsp.Data += SmellInsp_Data;

        List<Task> scans = new();
        if (App.IonVision != null)
        {
            scans.Add(MakeSampleScan(App.IonVision));
        }
        else if (_smellInsp.IsOpen)     // remove "else" to measure from BOTH ENOSES
        {
            scans.Add(WaitForSntSamples());
        }

        await Task.WhenAll(scans);

        _odorDisplay.Data -= OdorDisplay_Data;
        _smellInsp.Data -= SmellInsp_Data;

        actuators = _gases.Items
            .Select(gas => new Actuator(gas.ChannelID, new ActuatorCapabilities(
                KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            )));
        SendOdorDisplayRequest(new SetActuators(actuators.ToArray()));

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
        await App.ML.Config(dataSources.ToArray(),
            _gases.Items.Select(gas => new ML.ChannelProps((int)gas.ChannelID, gas.Name, gas.Propeties)).ToArray(),
            settings.Reproduction_MaxIterations,
            settings.Reproduction_Threshold
        );

        await Task.Delay(500);

        if (SampleScan != null)
        {
            await App.ML.Publish(SampleScan);
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

    Gases _gases = new Gases();
    List<SmellInsp.Data> _sntSamples = new();
    List<float> _pidSamples = new();

    private OdorDisplay.Device.ID[] GetAvailableChannelIDs()
    {
        var ids = new List<OdorDisplay.Device.ID>();
        var response = SendOdorDisplayRequest(new QueryDevices());

        if (response != null)
        {
            var devices = Devices.From(response);
            if (devices != null)
            {
                for (int i = 0; i < Devices.MaxOdorModuleCount; i++)
                {
                    if (devices.HasOdorModule(i))
                    {
                        ids.Add((OdorDisplay.Device.ID)(i + 1));
                    }
                }
            }
        }

        return ids.ToArray();
    }

    private Response? SendOdorDisplayRequest(Request request)
    {
        _nlog.Info($"Sent: {request}");

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);
        HandleOdorDisplayError(result, $"send the '{request.Type}' request");

        if (ack != null)
            _nlog.Info($"Received: {ack}");
        if (result.Error == Comm.Error.Success && response != null)
            _nlog.Info($"Received: {response}");

        return response;
    }

    private async Task MakeSampleScan(IonVision.Communicator ionVision)
    {
        SampleScan = null;

        var resp = HandleIonVisionError(await ionVision.StartScan(), "StartScan");
        if (!resp.Success)
        {
            LogDms?.Invoke(this, new LogHanddlerArgs("Failed to start sample scan."));
            return;
        }

        LogDms?.Invoke(this, new LogHanddlerArgs("Scanning..."));

        var waitForScanProgress = true;

        do
        {
            await Task.Delay(1000);
            var progress = HandleIonVisionError(await ionVision.GetScanProgress(), "GetScanProgress");
            var value = progress?.Value?.Progress ?? -1;

            if (value >= 0)
            {
                waitForScanProgress = false;
                LogDms?.Invoke(this, new LogHanddlerArgs($"Scanning... {value} %", true));
            }
            else if (waitForScanProgress)
            {
                continue;
            }
            else
            {
                LogDms?.Invoke(this, new LogHanddlerArgs($"Scanning finished.", true));
                break;
            }

        } while (true);

        await Task.Delay(300);
        SampleScan = HandleIonVisionError(await ionVision.GetScanResult(), "GetScanResult").Value;

        LogDms?.Invoke(this, new LogHanddlerArgs(SampleScan != null ? "Ready to start." : "Failed to retrieve the scanning result"));
    }

    private async Task WaitForSntSamples()
    {
        _sntSamples.Clear();

        LogSnt?.Invoke(this, new LogHanddlerArgs($"Collecting SNT samples..."));

        while (_sntSamples.Count < SNT_MAX_DATA_COUNT)
        {
            await Task.Delay(1000);
            LogSnt?.Invoke(this, new LogHanddlerArgs($"Collected {_sntSamples.Count}/{SNT_MAX_DATA_COUNT} samples...", true));
        }

        LogSnt?.Invoke(this, new LogHanddlerArgs($"Samples collected.", true));
        LogSnt?.Invoke(this, new LogHanddlerArgs($"Ready to start."));
    }

    private void HandleOdorDisplayError(Comm.Result odorDisplayResult, string action)
    {
        if (odorDisplayResult.Error != Comm.Error.Success)
        {
            Dialogs.MsgBox.Error("Odor Display", $"Cannot {action}:\n{odorDisplayResult.Reason}");
        }
    }

    private static IonVision.API.Response<T> HandleIonVisionError<T>(IonVision.API.Response<T> response, string action)
    {
        var error = !response.Success ? response.Error : "OK";
        _nlog.Error($"{action}: {error}");
        return response;
    }
    private void OdorDisplay_Data(object? sender, OdorDisplay.Packets.Data data)
    {
        if (_pidSamples.Count < PID_MAX_DATA_COUNT)
        {
            foreach (var measurement in data.Measurements)
            {
                if (measurement.Device == OdorDisplay.Device.ID.Base)
                {
                    var pid = measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) as PIDValue;
                    if (pid != null)
                    {
                        _pidSamples.Add(pid.Volts);
                        break;
                    }
                }
            }
        }
    }

    private void SmellInsp_Data(object? sender, SmellInsp.Data data)
    {
        if (_sntSamples.Count < SNT_MAX_DATA_COUNT)
        {
            _sntSamples.Add(data);
        }
    }
}
