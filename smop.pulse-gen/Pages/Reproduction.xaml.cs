using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Smop.PulseGen.Pages;

public partial class Reproduction : Page, IPage<Navigation>
{
    public event EventHandler<Navigation>? Next;

    public Reproduction()
    {
        InitializeComponent();

        Application.Current.Exit += (s, e) => CleanUp();
    }

    public void Start(ML.Communicator ml)
    {
        _step = 0;

        _ml = ml;
        ml.RecipeReceived += HandleRecipe;

        _ionVision = App.IonVision!;

        tblRecipeName.Text = "Recipe";
        tblRecipeState.Text = "in preparation";
        tblRMSQ.Text = "";

        grdChannels.Children.Clear();

        tblDmsStatus.Text = "-";

        btnQuit.Content = "Interrupt";
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Reproduction) + "Page");

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    ML.Communicator? _ml;
    IonVision.Communicator? _ionVision;

    int _step = 0;

    private void HandleRecipe(object? sender, ML.Recipe recipe)
    {
        Dispatcher.Invoke(() =>
        {
            _step++;
            _nlog.Info(recipe.ToString());

            DisplayRecipeInfo(recipe);

            // send command to OD
            if (recipe.Channels != null)
            {
                var actuators = new List<Actuator>();
                foreach (var channel in recipe.Channels)
                {
                    var valveCap = channel.Duration switch
                    {
                        >0 => KeyValuePair.Create(OdorDisplay.Device.Controller.OdorantValve, channel.Duration * 1000),
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

                HandleOdorDisplayError(SendOdorDisplayRequest(new SetActuators(actuators.ToArray())), "send channel command");
            }

            // schedule new scan
            if (!recipe.Finished)
            {
                DispatchOnce.Do(3, SendScanToML);
            }
        });
    }

    private void SendScanToML()
    {
        Task.Run(async () =>
        {
            var scan = await Scan();
            if (scan != null)
            {
                _ml?.Publish(scan);
            }
        });
    }

    private void DisplayRecipeInfo(ML.Recipe recipe)
    {
        if (!string.IsNullOrEmpty(recipe.Name))
        {
            tblRecipeName.Text = recipe.Name;
        }
        tblRecipeState.Text = recipe.Finished ? $"finished in {_step} steps" : $"in progress (step #{_step})";
        tblRMSQ.Text = $"r = {recipe.MinRMSE:N4}";

        grdChannels.Children.Clear();
        if (recipe.Channels != null)
        {
            int i = 0;
            foreach (var channel in recipe.Channels)
            {
                var str = $"Channel {channel.Id}: {channel.Flow} ml/min";
                if (channel.Duration > 0)
                {
                    str += $" for {channel.Duration:N2} seconds";
                }
                if (channel.Temperature != null)
                {
                    str += $" ({channel.Temperature}°C)";
                }

                var tbl = new TextBlock()
                {
                    Text = str,
                    Style = FindResource("Channel") as Style,
                };
                Grid.SetRow(tbl, i);
                grdChannels.Children.Add(tbl);

                i++;
            }
        }

        btnQuit.Content = recipe.Finished ? "Continue" : "Interrupt";
    }

    private async Task<IonVision.ScanResult?> Scan()
    {
        if (_ionVision == null)
            return null;

        var resp = HandleIonVisionError(await _ionVision.StartScan(), "StartScan");
        if (!resp.Success)
        {
            DisplayDmsStatus("Failed to start scan.");
            return null;
        }

        DisplayDmsStatus("Scanning...");

        var waitForScanProgress = true;

        do
        {
            await Task.Delay(1000);
            var progress = HandleIonVisionError(await _ionVision.GetScanProgress(), "GetScanProgress");
            var value = progress?.Value?.Progress ?? -1;

            if (value >= 0)
            {
                waitForScanProgress = false;
                DisplayDmsStatus($"Scanning... {value} %");
            }
            else if (waitForScanProgress)
            {
                continue;
            }
            else
            {
                DisplayDmsStatus($"Scanning finished.");
                break;
            }

        } while (true);

        await Task.Delay(300);
        var scan = HandleIonVisionError(await _ionVision.GetScanResult(), "GetScanResult").Value;
        if (scan == null)
        {
            DisplayDmsStatus("Failed to retrieve the scanning result.");
        }

        return scan;
    }

    private void DisplayDmsStatus(string line)
    {
        Dispatcher.Invoke(() => tblDmsStatus.Text = line);
    }

    private OdorDisplay.Result SendOdorDisplayRequest(Request request)
    {
        _nlog.Info($"Sent: {request}");

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);

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
            Dialogs.MsgBox.Error(Title, $"Odor Display: Cannot {action}:\n{odorDisplayResult.Reason}");
        }
    }

    private static IonVision.API.Response<T> HandleIonVisionError<T>(IonVision.API.Response<T> response, string action)
    {
        var error = !response.Success ? response.Error : "OK";
        _nlog.Error($"{action}: {error}");
        return response;
    }

    private void CleanUp()
    {
        if (_ml != null)
        {
            _ml.RecipeReceived -= HandleRecipe;
        }
    }

    // UI

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .BindScaleToZoomLevel(sctScale)
            .BindContentToZoomLevel(lblZoom)
            .BindVisibilityToDebug(lblDebug);

        if (Focusable)
        {
            Focus();
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance
            .UnbindScaleToZoomLevel(sctScale)
            .UnbindContentToZoomLevel(lblZoom)
            .UnbindVisibilityToDebug(lblDebug);
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        CleanUp();
        Next?.Invoke(this, Storage.Instance.SetupPage);
    }
}
