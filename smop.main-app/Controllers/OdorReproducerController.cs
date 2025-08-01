﻿using Smop.Common;
using Smop.MainApp.Dialogs;
using Smop.MainApp.Logging;
using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IV = Smop.IonVision;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp.Controllers;

public class OdorReproducerController
{
    public record class OdorChannelConfig(
        OdorDisplay.Device.ID ID,
        float Flow
    );

    public record class Config(
        ML.Communicator MLComm,
        OdorChannelConfig[] TargetFlows,
        IMeasurement? TargetMeasurement,
        System.Windows.Size DataSize
    )
    {
        public int TrialsPerIteration => 1 + (int)Math.Pow(2, TargetFlows.Length);  // check the smop-ml Matlab code 
    }

    public OdorChannel[] OdorChannels => _odorChannels.ToArray();
    public int CurrentStep { get; private set; } = 0;
    public float BestDistance { get; private set; } = 10000;
    public float[] BestFlows { get; private set; }
    public float[] RecipeFlows { get; private set; }

    public event EventHandler<IV.Defs.ScanResult>? ScanFinished;
    public event EventHandler<IV.Defs.ScopeResult>? ScopeScanFinished;
    public event EventHandler<SmellInsp.Data>? SntCollected;
    public event EventHandler? MlComputationStarted;
    public event EventHandler? ENoseStarted;
    public event EventHandler<double>? ENoseProgressChanged;
    public event EventHandler<ODPackets.Data>? OdorDisplayData;

    public OdorReproducerController(Config config)
    {
        _ml = config.MLComm;

        _dmsCache.SetSubfolder((int)config.DataSize.Height, (int)config.DataSize.Width);

        var targetText = config.TargetFlows.Select(flow => $"{_odorChannels.NameFromID(flow.ID)} {flow.Flow}");
        _nlog.Info(LogIO.Text(Timestamp.Ms, "Target", string.Join(" ", targetText)));

        var settings = Properties.Settings.Default;
        _sntDataCollector.SampleCount = settings.Reproduction_SntSampleCount;

        _odorDisplay.Data += OdorDisplay_Data;

        BestFlows = config.TargetFlows.Select(flow => 0f).ToArray();
        RecipeFlows = config.TargetFlows.Select(flow => 0f).ToArray();

        _canLogOdorDisplayData = !Storage.Instance.Simulating.HasFlag(SimulationTarget.OdorDisplay);
    }

    public void ShutDownFlows()
    {
        if (_odorDisplay.IsOpen)
        {
            System.Threading.Thread.Sleep(100);
            LogIO.Add(_odController.ShutdownChannels(_odorChannels), "StopOdors", LogSource.OD);
        }
    }

    public void CleanUp()
    {
        _odorDisplay.Data -= OdorDisplay_Data;
    }

    public void ExecuteRecipe(ML.Recipe recipe)
    {
        CurrentStep++;
        _nlog.Info(LogIO.Text(Timestamp.Ms.ToString(), RecipeToString(recipe)));

        var cachedDmsScan = _dmsCache.Find(recipe, out string? dmsFilename);

        // send command to OD
        if (cachedDmsScan == null && !recipe.IsFinal)
        {
            var actuators = recipe.ToOdorPrinterActuators();
            if (actuators.Length > 0)
                COMHelper.ShowErrorIfAny(_odController.SetFlowsAndValves(actuators.ToArray()), "release odors");
        }

        // update the solution
        if (recipe.Distance < BestDistance)
        {
            BestDistance = recipe.Distance;
            BestFlows = RecipeFlows.Select(f => f).ToArray();
        }

        // schedule new scan
        if (!recipe.IsFinal)
        {
            if (cachedDmsScan != null)
            {
                _nlog.Info(LogIO.Text(Timestamp.Ms, "Cache", "Read", dmsFilename));

                var cleanupDurationMs = _pauseEstimator.GetCleanupDuration(recipe.Channels?.Select(ch => ch.Flow));
                var cleanupDuration = (int)(cleanupDurationMs * 1000);
                _ = SendMeasurementToML(cachedDmsScan, _pauseEstimator.UseCleanupPidLevel, _pauseEstimator.CleanupPidLevel, cleanupDuration);
            }
            else
            {
                CollectDataAndSendToML(recipe, useDelays: !IV.Simulator.IsFast);
            }
        }
        else
        {
            BestFlows = recipe.Channels?.Select(c => c.Flow).ToArray() ?? RecipeFlows;
            System.Media.SystemSounds.Beep.Play();

            _canLogOdorDisplayData = false;

            if (_odorDisplayLogger.HasRecords)
            {
                var (result, folder) = Logger.Save(new ILog[] { _odorDisplayLogger, _ionVisionLogger });
                if (result == SavingResult.None)
                {
                    MsgBox.Warn(App.Name, "No data to save", MsgBox.Button.OK);
                }
                else if (result == SavingResult.Save && App.LogFileName is string logFilename)
                {
                    File.Copy(logFilename, Path.Combine(folder ?? "", "events.txt"));
                }
                _odorDisplayLogger.Clear();
            }
        }

        RecipeFlows = recipe.Channels?.Select(c => c.Flow).ToArray() ?? RecipeFlows;
    }

    public void CollectDataAndSendToML(ML.Recipe recipe, bool useDelays)
    {
        Task.Run(async () =>
        {
            App.IonVision?.SetScanResultComment(new { Pulse = _odorChannels.ToDmsComment(recipe) });

            if (useDelays)
            {
                var saturationDurationSec = _pauseEstimator.GetSaturationDuration(recipe.Channels?.Select(ch => ch.Flow));
                await Task.Delay((int)(saturationDurationSec * 1000));
            }

            var settings = Properties.Settings.Default;
            var measurement = await CollectData(settings.Reproduction_DmsSingleSV, useDelays); // recipe.Usv

            if (measurement != null)
            {
                int cleanupDuration = 100;
                double cleanupPidLevel = 1;

                if (useDelays)
                {
                    var cleanupDurationSec = _pauseEstimator.GetCleanupDuration(recipe.Channels?.Select(ch => ch.Flow));
                    cleanupDuration = (int)(cleanupDurationSec * 1000);
                    cleanupPidLevel = _pauseEstimator.CleanupPidLevel;
                }

                await SendMeasurementToML(measurement, _pauseEstimator.UseCleanupPidLevel, _pauseEstimator.CleanupPidLevel, cleanupDuration);
                if (measurement is IV.Defs.ScanResult fullScan)
                {
                    var filename = _dmsCache.Save(recipe, fullScan);
                    _nlog.Info(LogIO.Text(Timestamp.Ms, "Cache", "Write", filename));
                }
            }
        });
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger("Reproducer");

    readonly OdorChannels _odorChannels = new();
    readonly DmsCache _dmsCache = new();

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.DataCollector _sntDataCollector = new();
    readonly OdorDisplayLogger _odorDisplayLogger = OdorDisplayLogger.Instance;
    readonly IonVisionLogger _ionVisionLogger = IonVisionLogger.Instance;

    readonly OdorDisplayController _odController = new();
    readonly PauseEstimator _pauseEstimator = new();

    readonly ML.Communicator _ml;

    bool _canSendFrequentData = false;
    bool _canLogOdorDisplayData;

    double _pidLastValue = 0;

    private string RecipeToString(ML.Recipe recipe)
    {
        var name = recipe.Name.Replace(' ', '_');
        var fields = new List<string>() { "Received", name, recipe.IsFinal ? "Final" : "Continues", recipe.Distance.ToString("0.####") };
        if (recipe.Channels != null)
        {
            fields.AddRange(recipe.Channels.Select(ch => $"{_odorChannels.NameFromID((OdorDisplay.Device.ID)ch.Id)} {ch.Flow}"));
        }
        return LogIO.Text("Recipe", string.Join(" ", fields));
    }

    private async Task<IMeasurement?> CollectData(float usv, bool useDelays = true)
    {
        _canSendFrequentData = true;

        IMeasurement? result = null;

        if (App.IonVision != null)
        {
            result = await MakeDmsScan(App.IonVision, usv);
        }
        else
        {
            ENoseStarted?.Invoke(this, EventArgs.Empty);
            result = await _sntDataCollector.Collect((count, progress) =>
                ENoseProgressChanged?.Invoke(this, progress));

            if (useDelays)
                await Task.Delay(300);
        }

        _canSendFrequentData = false;

        ShutDownFlows();

        return result;
    }

    private async Task SendMeasurementToML(IMeasurement measurement, bool useCleanupPidLevel, double cleanupPidLevel, int cleanupDuration)
    {
        MlComputationStarted?.Invoke(this, EventArgs.Empty);

        if (measurement is IV.Defs.ScopeResult dmsScopeScan)
        {
            ScopeScanFinished?.Invoke(this, dmsScopeScan);
            await WaitUntilCleanedUp(useCleanupPidLevel, cleanupPidLevel, cleanupDuration);
            _ = _ml.Publish(dmsScopeScan);
        }
        else if (measurement is IV.Defs.ScanResult dmsFullScan)
        {
            ScanFinished?.Invoke(this, dmsFullScan);
            await WaitUntilCleanedUp(useCleanupPidLevel, cleanupPidLevel, cleanupDuration);
            _ = _ml.Publish(dmsFullScan);
        }
        else if (measurement is SmellInsp.Data snt)
        {
            SntCollected?.Invoke(this, snt);
            await WaitUntilCleanedUp(useCleanupPidLevel, cleanupPidLevel, cleanupDuration);
            _ = _ml.Publish(snt.AsFeatures());
        }
    }

    private async Task WaitUntilCleanedUp(bool useCleanupPidLevel, double cleanupPidLevel, int cleanupDuration)
    {
        if (useCleanupPidLevel)
        {
            while (_pidLastValue > cleanupPidLevel)
            {
                await Task.Delay(100);
            }
        }
        else
        {
            await Task.Delay(cleanupDuration);
        }
    }

    private async Task<IMeasurement?> MakeDmsScan(IV.Communicator ionVision, float usv, bool useDelays = true)
    {
        ENoseStarted?.Invoke(this, EventArgs.Empty);

        if (usv > 0)
        {
            if (!LogIO.Add(await ionVision.GetScopeParameters(), "GetScopeParameters", out IV.Defs.ScopeParameters? scopeParams) || scopeParams == null)
                return null;

            if (useDelays)
                await Task.Delay(150);
            LogIO.Add(await ionVision.SetScopeParameters(scopeParams with { Usv = usv }), "SetScopeParameters");

            if (useDelays)
                await Task.Delay(150);
            LogIO.Add(await ionVision.EnableScopeMode(), "EnableScopeMode");

            var scan = await ionVision.WaitScopeScanToFinish(progress => ENoseProgressChanged?.Invoke(this, progress));

            if (useDelays)
                await Task.Delay(150);
            LogIO.Add(await ionVision.DisableScopeMode(), "DisableScopeMode");

            return scan;
        }
        else
        {
            if (!LogIO.Add(await ionVision.StartScan(), "StartScan"))
                return null;

            await ionVision.WaitScanToFinish(progress => ENoseProgressChanged?.Invoke(this, progress));

            if (useDelays)
                await Task.Delay(300);
            LogIO.Add(await ionVision.GetScanResult(), "GetScanResult", out IV.Defs.ScanResult? scan);

            if (scan != null)
            {
                _ionVisionLogger.Add(scan);
            }

            return scan;
        }
    }

    private async void OdorDisplay_Data(object? sender, ODPackets.Data data)
    {
        await COMHelper.Do(() =>
        {
            if (_canLogOdorDisplayData)
            {
                _odorDisplayLogger.Add(data);
            }

            OdorDisplayData?.Invoke(this, data);

            foreach (var measurement in data.Measurements)
            {
                if (measurement.Device == OdorDisplay.Device.ID.Base &&
                    measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) is ODPackets.Sensor.PID pid)
                {
                    _pidLastValue = pid.Volts;

                    if (_canSendFrequentData && Properties.Settings.Default.Reproduction_UsePID)
                    {
                        _ = _ml.Publish(pid.Volts);
                    }
                    break;
                }
            }
        });
    }
}
