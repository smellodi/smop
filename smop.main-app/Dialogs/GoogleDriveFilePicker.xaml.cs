using Smop.MainApp.Controllers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

namespace Smop.MainApp.Dialogs;

public partial class GoogleDriveFilePicker : Window
{
    public ObservableCollection<Google.Apis.Drive.v3.Data.File> Files { get; set; } = new();
    public Google.Apis.Drive.v3.Data.File? SelectedFile { get; set; } = null;

    public GoogleDriveFilePicker(IList<Google.Apis.Drive.v3.Data.File> files)
    {
        InitializeComponent();

        foreach (var file in files)
        {
            if (file.Trashed == true)
                continue;
            Files.Add(file);
        }
    }

    // Internal

    readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.BindScaleToZoomLevel(sctScale);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.UnbindScaleToZoomLevel(sctScale);
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private async void Files_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        lblDmsInfo.Content = string.Empty;

        if (lsvFiles.SelectedItem is Google.Apis.Drive.v3.Data.File dmsFile)
        {
            var content = await GoogleDriveService.Instance.ReadFile(dmsFile.Id);
            try
            {
                var dms = JsonSerializer.Deserialize<IonVision.Defs.ScanResult>(content, _jsonOptions);
                lblDmsInfo.Content = dms?.Info ?? string.Empty;
            }
            catch
            {
                lblDmsInfo.Content = content;
            }
        }
    }
}
