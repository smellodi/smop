using Smop.MainApp.Controllers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp.Dialogs;

public partial class KnownOdorsEditor : Window, INotifyPropertyChanged
{
    public ObservableCollection<OdorChannelProperties> Items { get; set; } = new(new KnownOdors());

    public PidLevelInspector PidLevelInspector { get; } = new PidLevelInspector();

    public KnownOdorsEditor()
    {
        InitializeComponent();

        DialogTools.HideWindowButtons(this);

        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Internal

    // UI

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.BindScaleToZoomLevel(sctScale);
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        Storage.Instance.UnbindScaleToZoomLevel(sctScale);
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var newKnwonOdors = new KnownOdors(Items);
        newKnwonOdors.Save();
        
        PidLevelInspector.Save();
        
        DialogResult = true;
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as Button)?.Tag;
        if (tag is string name)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].ShortKnownName == name)
                {
                    Items.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        Items.Add(new OdorChannelProperties(0, 50, 70, 1, "name", "LongName"));
    }
}
