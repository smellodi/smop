using Smop.OdorDisplay.Packets;
using Smop.PulseGen.Reproducer;
using Smop.PulseGen.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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

        tblRecipeName.Text = "";
        tblRMSQ.Text = "";

        grdChannels1.Children.Clear();

        grdChannels1.RowDefinitions.Clear();
        for (int i = 0; i < Enum.GetNames(typeof(OdorDisplay.Device.ID)).Length; i++)
        {
            grdChannels1.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
        }

        DisplayRecipeInfo(new ML.Recipe("", 0, 0, _gases.Items.Select(gas => new ML.ChannelRecipe((int)gas.ChannelID, -1, -1)).ToArray()));

        //tblDmsStatus.Text = "-";

        SetActiveElement(ActiveElement.ML);
    }

    // Internal

    [Flags]
    enum ActiveElement
    {
        None = 0,
        ML = 1,
        OdorDisplay = 2,
        DMS = 4
    }

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(Reproduction) + "Page");
    static readonly Gases _gases = new();

    readonly OdorDisplay.CommPort _odorDisplay = OdorDisplay.CommPort.Instance;
    ML.Communicator? _ml;
    IonVision.Communicator? _ionVision;

    int _step = 0;
    ActiveElement _activeElement = ActiveElement.None;

    private void SetActiveElement(ActiveElement el)
    {
        _activeElement = el;

        if (_activeElement.HasFlag(ActiveElement.ML))
        {
            tblRecipeState.Text = "Creating a recipe";

            brdML.Style = FindResource("ActiveElement") as Style;
            imgDmsActive.Visibility = Visibility.Visible;
            imgDmsPassive.Visibility = Visibility.Hidden;
        }
        else
        {
            brdML.Style = FindResource("Element") as Style;
            imgDmsActive.Visibility = Visibility.Hidden;
            imgDmsPassive.Visibility = Visibility.Visible;
        }

        if (_activeElement.HasFlag(ActiveElement.OdorDisplay))
        {
            tblRecipeState.Text = "Mixing the chemicals to produce the odor";

            brdOdorDisplay.Style = FindResource("ActiveElement") as Style;
            imgGas.Visibility = Visibility.Visible;
        }
        else
        {
            brdOdorDisplay.Style = FindResource("Element") as Style;
            imgGas.Visibility = Visibility.Hidden;
        }

        if (_activeElement.HasFlag(ActiveElement.DMS))
        {
            tblRecipeState.Text = "Scanning the produced odor";

            prbDmsProgress.Visibility = Visibility.Visible;
        }
        else
        {
            prbDmsProgress.Visibility = Visibility.Hidden;
            prbDmsProgress.Value = 0;
        }

        if (_activeElement == ActiveElement.None)
        {
            tblRecipeState.Text = "Finished";
            tblRecipeName.Text = "";
            imgGas.Visibility = Visibility.Visible;
        }

        tblRMSQ.Visibility = _activeElement == ActiveElement.OdorDisplay ? Visibility.Hidden : Visibility.Visible;
    }

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

            SetActiveElement(ActiveElement.OdorDisplay);

            // schedule new scan
            if (!recipe.Finished)
            {
                DispatchOnce.Do(3, ScanAndSendToML);
            }
            else
            {
                SetActiveElement(ActiveElement.None);
            }
        });
    }

    private void ScanAndSendToML()
    {
        Task.Run(async () =>
        {
            var scan = await ScanGas();
            if (scan != null)
            {
                _ml?.Publish(scan);
                Dispatcher.Invoke(() => SetActiveElement(ActiveElement.ML));
            }
        });
    }

    private void DisplayRecipeInfo(ML.Recipe recipe)
    {
        if (!string.IsNullOrEmpty(recipe.Name))
        {
            tblRecipeName.Text = recipe.Name;
        }
        tblRecipeState.Text = recipe.Finished ? $"Finished in {_step} steps" : $"In progress (step #{_step})";
        tblRMSQ.Text = $"r = {recipe.MinRMSE:N4}";

        grdChannels1.Children.Clear();
        if (recipe.Channels != null)
        {
            int rowIndex = 0;
            foreach (var channel in recipe.Channels)
            {
                var id = (OdorDisplay.Device.ID)channel.Id;
                var str = _gases.Get(id)?.Name ?? id.ToString();
                if (channel.Flow >= 0)
                {
                    str += $", {channel.Flow} ml/min";
                }
                if (channel.Duration > 0)
                {
                    str += $", {channel.Duration:N2} sec.";
                }
                if (channel.Temperature != null)
                {
                    str += $", {channel.Temperature:N1}°";
                }

                var tbl = new TextBlock()
                {
                    Text = str,
                    Style = FindResource("Channel") as Style,
                };
                Grid.SetRow(tbl, rowIndex);
                grdChannels1.Children.Add(tbl);

                rowIndex++;
            }
        }

        btnQuit.Content = recipe.Finished ? "Continue" : "Interrupt";
    }

    private async Task<IonVision.ScanResult?> ScanGas()
    {
        if (_ionVision == null)
            return null;

        Dispatcher.Invoke(() => SetActiveElement(ActiveElement.OdorDisplay | ActiveElement.DMS));

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
                Dispatcher.Invoke(() => prbDmsProgress.Value = value);
            }
            else if (waitForScanProgress)
            {
                continue;
            }
            else
            {
                Dispatcher.Invoke(() => prbDmsProgress.Value = 100);
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
        //Dispatcher.Invoke(() => tblDmsStatus.Text = line);
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
