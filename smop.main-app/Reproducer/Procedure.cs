using Smop.MainApp.Logging;
using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp.Reproducer;

public class Procedure
{
    public record class GasFlow(
        OdorDisplay.Device.ID ID,
        float Flow
    );
    public record class Config(
        ML.Communicator MLComm,
        GasFlow[] TargetFlows,
        System.Windows.Size DataSize
    );

    public Gas[] Gases => _gases.Items;
    public int CurrentStep => _step;

    public event EventHandler<IonVision.ScanResult>? ScanFinished;
    public event EventHandler? MlComputationStarted;
    public event EventHandler? ENoseStarted;
    public event EventHandler<double>? ENoseProgressChanged;
    public event EventHandler<ODPackets.Data>? OdorDisplayData;

    public Procedure(Config config)
    {
        _ml = config.MLComm;

        _dmsCache.SetSubfolder((int)config.DataSize.Height, (int)config.DataSize.Width);

        var targetText = config.TargetFlows.Select(flow => $"{_gases.NameFromID(flow.ID)} {flow.Flow}");
        _nlog.Info(LogIO.Text("Target", string.Join(" ", targetText)));

        var settings = Properties.Settings.Default;

        _odorDisplay.Data += OdorDisplay_Data;

        if (App.IonVision == null && _smellInsp.IsOpen)
        {
            _smellInsp.Data += SmellInsp_Data;
        }
    }

    public void ShutDownFlows()
    {
        if (_odorDisplay.IsOpen)
        {
            System.Threading.Thread.Sleep(100);
            LogIO.Add(_odController.StopGases(_gases), "StopOdors", LogSource.OD);
        }
    }

    public void CleanUp()
    {
        _odorDisplay.Data -= OdorDisplay_Data;

        if (App.IonVision == null && _smellInsp.IsOpen)
        {
            _smellInsp.Data -= SmellInsp_Data;
        }
    }

    public void ExecuteRecipe(ML.Recipe recipe)
    {
        _step++;
        _nlog.Info(RecipeToString(recipe));

        var cachedDmsScan = _dmsCache.Find(recipe, out string? dmsFilename);

        // send command to OD
        if (cachedDmsScan == null)
        {
            var actuators = recipe.ToOdorPrinterActuators();
            if (actuators.Length > 0)
                COMHelper.ShowErrorIfAny(_odController.ReleaseGases(actuators.ToArray()), "release odors");
        }

        // schedule new scan
        if (!recipe.Finished)
        {
            if (cachedDmsScan != null)
            {
                _nlog.Info(LogIO.Text("Cache", "Read", dmsFilename));
                DispatchOnce.Do(2, () => SendDmsScanToML(cachedDmsScan));
            }
            else
            {
                Task.Run(async () =>
                {
                    var waitingTime = OdorDisplayController.CalcWaitingTime(recipe.Channels?.Select(ch => ch.Flow));
                    await Task.Delay((int)(waitingTime * 1000));

                    var dmsScan = await CollectData();
                    if (dmsScan != null)
                    {
                        SendDmsScanToML(dmsScan);
                        var filename = _dmsCache.Save(recipe, dmsScan);
                        _nlog.Info(LogIO.Text("Cache", "Write", filename));
                    }
                });
            }
        }
        else
        {
            System.Media.SystemSounds.Beep.Play();
        }
    }

    // Internal

    const int SNT_MAX_DATA_COUNT = 10;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger("Reproducer");

    readonly Gases _gases = new();
    readonly DmsCache _dmsCache = new();

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly OdorDisplayController _odController = new();

    readonly ML.Communicator _ml;

    int _step = 0;

    bool _canSendFrequentData = false;
    int _sntSamplesCount = 0;

    public string RecipeToString(ML.Recipe recipe)
    {
        var fields = new List<string>() { "Received", recipe.Name, recipe.Finished ? "Final" : "Continues", recipe.MinRMSE.ToString("0.####") };
        if (recipe.Channels != null)
        {
            fields.AddRange(recipe.Channels.Select(ch => $"{_gases.NameFromID((OdorDisplay.Device.ID)ch.Id)} {ch.Flow}"));
        }
        return LogIO.Text("Recipe", string.Join(" ", fields));
    }

    private async Task<IonVision.ScanResult?> CollectData()
    {
        _canSendFrequentData = true;

        IonVision.ScanResult? dmsScan = null;

        if (App.IonVision != null)
        {
            dmsScan = await MakeDmsScan(App.IonVision);
        }
        else
        {
            ENoseStarted?.Invoke(this, EventArgs.Empty);
            while (_sntSamplesCount < SNT_MAX_DATA_COUNT)
            {
                await Task.Delay(1000);
                ENoseProgressChanged?.Invoke(this, 100 * _sntSamplesCount / SNT_MAX_DATA_COUNT);
            }
            await Task.Delay(300);
            MlComputationStarted?.Invoke(this, EventArgs.Empty);

            _sntSamplesCount = 0;
        }

        _canSendFrequentData = false;

        ShutDownFlows();

        return dmsScan;
    }

    private void SendDmsScanToML(IonVision.ScanResult dmsScan)
    {
        _ = _ml.Publish(dmsScan);
        ScanFinished?.Invoke(this, dmsScan);
        MlComputationStarted?.Invoke(this, EventArgs.Empty);
    }

    private async Task<IonVision.ScanResult?> MakeDmsScan(IonVision.Communicator ionVision)
    {
        if (ionVision == null)
            return null;

        ENoseStarted?.Invoke(this, EventArgs.Empty);

        if (!LogIO.Add(await ionVision.StartScan(), "StartScan"))
            return null;

        await ionVision.WaitScanToFinish(progress => ENoseProgressChanged?.Invoke(this, progress));
        
        await Task.Delay(300);
        LogIO.Add(await ionVision.GetScanResult(), "GetScanResult", out IonVision.ScanResult? scan);

        return scan;
    }

    private async void OdorDisplay_Data(object? sender, ODPackets.Data e)
    {
        await COMHelper.Do(() =>
        {
            OdorDisplayData?.Invoke(this, e);

            if (!_canSendFrequentData || !Properties.Settings.Default.Reproduction_UsePID)
                return;

            foreach (var measurement in e.Measurements)
            {
                if (measurement.Device == OdorDisplay.Device.ID.Base &&
                    measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) is ODPackets.PIDValue pid)
                {
                    _ = _ml.Publish(pid.Volts);
                    break;
                }
            }
        });
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data e)
    {
        await COMHelper.Do(() =>
        {
            if (_canSendFrequentData)
            {
                _sntSamplesCount++;
                _ = _ml.Publish(e);
            }
        });
    }
}
