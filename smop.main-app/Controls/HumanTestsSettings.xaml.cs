using Smop.MainApp.Controllers;
using System.Windows.Controls;

namespace Smop.MainApp.Controls;

public partial class HumanTestsSettings : UserControl
{
    public Controllers.HumanTests.Settings Settings { get; } = new();

    public HumanTestsSettings()
    {
        InitializeComponent();
        DataContext = Settings;
    }

    public void AddOdorChannel(OdorChannel odorChannel)
    {
        Settings.Channels.Add(odorChannel.ID, odorChannel.Name);
    }
}
