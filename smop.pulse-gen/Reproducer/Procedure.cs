using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smop.PulseGen.Reproducer;

public class Procedure
{
    public Gas[] Gases => _gases.Items;
    public int CurrentStep => _step;

    public event EventHandler? MlComputationStarted;
    public event EventHandler? ENoseStarted;
    public event EventHandler<double>? ENoseProgressChanged;

    public Procedure(ML.Communicator ml)
    {
        _ml = ml;

        var settings = Properties.Settings.Default;
        _scanDelay = settings.Reproduction_SniffingDelay;

        if (settings.Reproduction_UsePID)
        {
            _odorDisplay.Data += OdorDisplay_Data;
        }

        if (App.IonVision == null && _smellInsp.IsOpen)
        {
            _smellInsp.Data += SmellInsp_Data;
        }
    }

    public void ShutDownFlows()
    {
        var actuators = Gases.Select(gas => new Actuator(gas.ChannelID, new ActuatorCapabilities(
            ActuatorCapabilities.OdorantValveClose,
            KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, 0.0f)
        )));
        SendOdorDisplayRequest(new SetActuators(actuators.ToArray()));

        if (Properties.Settings.Default.Reproduction_UsePID)
        {
            _odorDisplay.Data -= OdorDisplay_Data;
        }

        if (App.IonVision == null && _smellInsp.IsOpen)
        {
            _smellInsp.Data -= SmellInsp_Data;
        }
    }

    public void ExecuteRecipe(ML.Recipe recipe)
    {
        _step++;
        _nlog.Info(recipe.ToString());

        // send command to OD
        if (recipe.Channels != null)
        {
            var actuators = new List<Actuator>();
            foreach (var channel in recipe.Channels)
            {
                var valveCap = channel.Duration switch
                {
                    > 0 => KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantValve, channel.Duration * 1000),
                    0 => ActuatorCapabilities.OdorantValveClose,
                    _ => ActuatorCapabilities.OutputValveOpenPermanently,
                };
                var caps = new ActuatorCapabilities(
                    valveCap,
                    KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, channel.Flow)
                );
                if (channel.Temperature != null)
                {
                    caps.Add(OdorDisplay.Device.Controller.ChassisTemperature, (float)channel.Temperature);
                }

                var actuator = new Actuator((OdorDisplay.Device.ID)channel.Id, caps);
                actuators.Add(actuator);
            }

            SendOdorDisplayRequest(new SetActuators(actuators.ToArray()));
        }

        // schedule new scan
        if (!recipe.Finished)
        {
            DispatchOnce.Do(_scanDelay, ScanAndSendToML);
        }
    }

    // Internal

    const int SCAN_PROGRESS_INTERVAL = 1000;    // ms
    const int SNT_MAX_DATA_COUNT = 10;

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Procedure));

    readonly Gases _gases = new();

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    readonly SmellInsp.CommPort _smellInsp = SmellInsp.CommPort.Instance;

    readonly ML.Communicator _ml;

    readonly float _scanDelay = 3;  // seconds

    int _step = 0;

    bool _canSendFrequentData = false;
    int _sntSamplesCount = 0;

    private void ScanAndSendToML()
    {
        Task.Run(async () =>
        {
            _canSendFrequentData = true;
            
            if (App.IonVision != null)
            {
                var scan = await ScanGas(App.IonVision);
                if (scan != null)
                {
                    _ = _ml.Publish(scan);
                    MlComputationStarted?.Invoke(this, EventArgs.Empty);
                }
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
            }

            _canSendFrequentData = false;
            _sntSamplesCount = 0;
        });
    }

    private async Task<IonVision.ScanResult?> ScanGas(IonVision.Communicator ionVision)
    {
        if (ionVision == null)
            return null;

        ENoseStarted?.Invoke(this, EventArgs.Empty);

        var resp = HandleIonVisionError(await ionVision.StartScan(), "StartScan");
        if (!resp.Success)
        {
            DisplayDmsStatus("Failed to start scan.");
            return null;
        }

        DisplayDmsStatus("Scanning...");

        var waitForScanProgress = true;

        do
        {
            await Task.Delay(SCAN_PROGRESS_INTERVAL);
            var progress = HandleIonVisionError(await ionVision.GetScanProgress(), "GetScanProgress");
            var value = progress?.Value?.Progress ?? -1;

            if (value >= 0)
            {
                waitForScanProgress = false;
                DisplayDmsStatus($"Scanning... {value} %");
                ENoseProgressChanged?.Invoke(this, value);
            }
            else if (waitForScanProgress)
            {
                continue;
            }
            else
            {
                DisplayDmsStatus($"Scanning finished.");
                ENoseProgressChanged?.Invoke(this, 100);
                break;
            }

        } while (true);

        await Task.Delay(300);
        var scan = HandleIonVisionError(await ionVision.GetScanResult(), "GetScanResult").Value;
        if (scan == null)
        {
            DisplayDmsStatus("Failed to retrieve the scanning result.");
        }

        return scan;
    }

    private OdorDisplay.Result SendOdorDisplayRequest(Request request)
    {
        _nlog.Info($"Sent: {request}");

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);
        HandleOdorDisplayError(result, $"send the '{request.Type}' request");

        if (ack != null)
            _nlog.Info($"Received: {ack}");
        if (result.Error == OdorDisplay.Error.Success && response != null)
            _nlog.Info($"Received: {response}");

        return result;
    }

    private void HandleOdorDisplayError(OdorDisplay.Result odorDisplayResult, string action)
    {
        if (odorDisplayResult.Error != OdorDisplay.Error.Success)
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

    private void DisplayDmsStatus(string line)
    {
        //Dispatcher.Invoke(() => tblDmsStatus.Text = line);
    }

    private void OdorDisplay_Data(object? sender, Data e)
    {
        if (!_canSendFrequentData)
            return;

        foreach (var measurement in e.Measurements)
        {
            if (measurement.Device == OdorDisplay.Device.ID.Base)
            {
                var pid = measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) as PIDValue;
                if (pid != null)
                {
                    _ = _ml.Publish(pid.Volts);
                    break;
                }
            }
        }
    }

    private void SmellInsp_Data(object? sender, SmellInsp.Data e)
    {
        if (_canSendFrequentData)
        {
            _sntSamplesCount++;
            _ = _ml.Publish(e);
        }
    }
}
