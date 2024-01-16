using Smop.MainApp.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ODPackets = Smop.OdorDisplay.Packets;

namespace Smop.MainApp.Reproducer;

public class Procedure
{
    public Gas[] Gases => _gases.Items;
    public int CurrentStep => _step;

    public event EventHandler? MlComputationStarted;
    public event EventHandler? ENoseStarted;
    public event EventHandler<double>? ENoseProgressChanged;
    public event EventHandler<ODPackets.Data>? OdorDisplayData;

    public Procedure(ML.Communicator ml)
    {
        _ml = ml;

        var settings = Properties.Settings.Default;
        _scanDelay = settings.Reproduction_SniffingDelay;

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
            var actuators = _gases.Items
                .Where(gas => !string.IsNullOrWhiteSpace(gas.Name))
                .Select(gas => new ODPackets.Actuator(gas.ChannelID, new ODPackets.ActuatorCapabilities(
                    ODPackets.ActuatorCapabilities.OdorantValveClose,
                    KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, 0.0f)
                )));
            SendOdorDisplayRequest(new ODPackets.SetActuators(actuators.ToArray()));
        }

        _odorDisplay.Data -= OdorDisplay_Data;

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
            var actuators = new List<ODPackets.Actuator>();
            foreach (var channel in recipe.Channels)
            {
                var valveCap = channel.Duration switch
                {
                    > 0 => KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantValve, channel.Duration * 1000),
                    0 => ODPackets.ActuatorCapabilities.OdorantValveClose,
                    _ => ODPackets.ActuatorCapabilities.OdorantValveOpenPermanently,
                };
                var caps = new ODPackets.ActuatorCapabilities(
                    valveCap,
                    KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantFlow, channel.Flow)
                );
                if (channel.Temperature != null)
                {
                    caps.Add(OdorDisplay.Device.Controller.ChassisTemperature, (float)channel.Temperature);
                }

                var actuator = new ODPackets.Actuator((OdorDisplay.Device.ID)channel.Id, caps);
                actuators.Add(actuator);
            }

            SendOdorDisplayRequest(new ODPackets.SetActuators(actuators.ToArray()));
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
            return null;
        }

        var waitForScanProgress = true;

        do
        {
            await Task.Delay(SCAN_PROGRESS_INTERVAL);
            var progress = HandleIonVisionError(await ionVision.GetScanProgress(), "GetScanProgress");
            var value = progress?.Value?.Progress ?? -1;

            if (value >= 0)
            {
                waitForScanProgress = false;
                ENoseProgressChanged?.Invoke(this, value);
            }
            else if (waitForScanProgress)
            {
                continue;
            }
            else
            {
                ENoseProgressChanged?.Invoke(this, 100);
                break;
            }

        } while (true);

        await Task.Delay(300);
        var scan = HandleIonVisionError(await ionVision.GetScanResult(), "GetScanResult").Value;

        return scan;
    }

    private Comm.Result SendOdorDisplayRequest(ODPackets.Request request)
    {
        _nlog.Info($"Sent: {request}");

        var result = _odorDisplay.Request(request, out ODPackets.Ack? ack, out ODPackets.Response? response);
        HandleOdorDisplayError(result, $"send the '{request.Type}' request");

        if (ack != null)
            _nlog.Info($"Received: {ack}");
        if (result.Error == Comm.Error.Success && response != null)
            _nlog.Info($"Received: {response}");

        return result;
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

    private async void OdorDisplay_Data(object? sender, ODPackets.Data e)
    {
        await CommPortEventHandler.Do(() =>
        {
            OdorDisplayData?.Invoke(this, e);

            if (!_canSendFrequentData || !Properties.Settings.Default.Reproduction_UsePID)
                return;

            foreach (var measurement in e.Measurements)
            {
                if (measurement.Device == OdorDisplay.Device.ID.Base)
                {
                    var pid = measurement.SensorValues.FirstOrDefault(value => value.Sensor == OdorDisplay.Device.Sensor.PID) as ODPackets.PIDValue;
                    if (pid != null)
                    {
                        _ = _ml.Publish(pid.Volts);
                        break;
                    }
                }
            }
        });
    }

    private async void SmellInsp_Data(object? sender, SmellInsp.Data e)
    {
        await CommPortEventHandler.Do(() =>
        {
            if (_canSendFrequentData)
            {
                _sntSamplesCount++;
                _ = _ml.Publish(e);
            }
        });
    }
}
