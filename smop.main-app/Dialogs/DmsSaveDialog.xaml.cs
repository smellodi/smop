using Smop.MainApp.Utils.Extensions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace Smop.MainApp.Dialogs;

public partial class DmsSaveDialog : Window
{
    public class DmsData(string name, IonVision.Defs.ScanResult data, bool isEnabled)
    {
        public string Name { get; set; } = name;
        public bool IsEnabled { get; set; } = isEnabled;
        public IonVision.Defs.ScanResult Data => data;
    }

    public ObservableCollection<DmsData> Items { get; set; } = new();

    public DmsSaveDialog(IReadOnlyList<IonVision.Defs.ScanResult> dmses)
    {
        InitializeComponent();

        foreach (var dms in dmses)
        {
            Items.Add(new DmsData(dms.StartTime.ToPath(), dms, true));
        }
    }

    // Internal

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.BindScaleToZoomLevel(sctScale);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.UnbindScaleToZoomLevel(sctScale);
    }


    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
