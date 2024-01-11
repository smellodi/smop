using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
        _proc = new Reproducer.Procedure(ml);
        _proc.MlComputationStarted += (s, e) => Dispatcher.Invoke(() => SetActiveElement(ActiveElement.ML));
        _proc.ENoseStarted += (s, e) => Dispatcher.Invoke(() => SetActiveElement(ActiveElement.OdorDisplay | ActiveElement.ENose));
        _proc.ENoseProgressChanged += (s, e) => Dispatcher.Invoke(() => prbDmsProgress.Value = e);

        ml.RecipeReceived += HandleRecipe;

        //imgDms.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;
        //imgSnt.Visibility = SmellInsp.CommPort.Instance.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        imgDms.Visibility = App.IonVision != null ? Visibility.Visible : Visibility.Collapsed;
        imgSnt.Visibility = App.IonVision == null && SmellInsp.CommPort.Instance.IsOpen ? Visibility.Visible : Visibility.Collapsed;

        tblRecipeName.Text = "";
        tblRMSQ.Text = "";

        grdChannels1.Children.Clear();

        grdChannels1.RowDefinitions.Clear();
        for (int i = 0; i < Enum.GetNames(typeof(OdorDisplay.Device.ID)).Length; i++)
        {
            grdChannels1.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
        }

        DisplayRecipeInfo(new ML.Recipe("", 0, 0, _proc.Gases.Select(gas => new ML.ChannelRecipe((int)gas.ChannelID, -1, -1)).ToArray()));

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
        ENose = 4
    }

    Reproducer.Procedure? _proc;

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

        if (_activeElement.HasFlag(ActiveElement.ENose))
        {
            tblRecipeState.Text = "Sniffing the produced odor with eNose";

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

        tblRMSQ.Visibility = _activeElement == ActiveElement.OdorDisplay || _activeElement == ActiveElement.None ? Visibility.Visible : Visibility.Hidden;
    }

    private void HandleRecipe(object? sender, ML.Recipe recipe)
    {
        Dispatcher.Invoke(() =>
        {
            DisplayRecipeInfo(recipe);
            _proc?.ExecuteRecipe(recipe);

            SetActiveElement(recipe.Finished ? ActiveElement.None : ActiveElement.OdorDisplay);
        });
    }

    private void DisplayRecipeInfo(ML.Recipe recipe)
    {
        if (!string.IsNullOrEmpty(recipe.Name))
        {
            tblRecipeName.Text = recipe.Name;
        }
        tblRecipeState.Text = recipe.Finished ? $"Finished in {_proc?.CurrentStep} steps" : $"In progress (step #{_proc?.CurrentStep})";
        tblRMSQ.Text = $"r = {recipe.MinRMSE:N4}";

        grdChannels1.Children.Clear();
        if (recipe.Channels != null)
        {
            int rowIndex = 0;
            foreach (var channel in recipe.Channels)
            {
                var id = (OdorDisplay.Device.ID)channel.Id;
                var str = _proc?.Gases.FirstOrDefault(gas => gas.ChannelID == id)?.Name ?? id.ToString();
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

        btnQuit.Content = recipe.Finished ? "Return" : "Interrupt";
    }

    private void CleanUp()
    {
        _proc?.ShutDownFlows();

        if (App.ML != null)
        {
            App.ML.RecipeReceived -= HandleRecipe;
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
