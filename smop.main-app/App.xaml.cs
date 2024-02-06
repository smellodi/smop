using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Smop.MainApp;

public partial class App : Application
{
    #region Global objects

    public static IonVision.Communicator? IonVision { get; set; } = null;
    public static ML.Communicator? ML { get; set; } = null;

    #endregion

    // Internal

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var settings = MainApp.Properties.Settings.Default;
        if (settings.CallUpgrade)
        {
            settings.Upgrade();
            settings.CallUpgrade = false;
            settings.Save();
        }

        // Configure the logger
        var config = new NLog.Config.LoggingConfiguration();

        var logfile = new NLog.Targets.FileTarget("File")
        { 
            FileName = "${basedir}/logs/logfile.txt",
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 5,
            Layout = Logging.LogIO.Text("${longdate}", "${logger}", "${callsite-linenumber}", "${message}", "${exception:format=ToString}")
            //Layout = "${longdate} ${callsite}:${callsite-linenumber} ${message}${exception:format=ToString}"
        };
        var logdebug = new NLog.Targets.DebugSystemTarget("logdebug")
        {
            Layout = $"[${{logger}}] ${{replace:${{message}}:searchFor={Logging.LogIO.LOG_DELIM}:replaceWith= }}"
        };

        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logdebug);
        config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logfile);

        NLog.LogManager.Configuration = config;

        // Set the US-culture across the application to avoid decimal point parsing/logging issues
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

        // Force all TextBox's to select its content upon focused
        EventManager.RegisterClassHandler(typeof(TextBox),
            UIElement.GotFocusEvent,
            new RoutedEventHandler(TextBox_GotFocus));
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        (sender as TextBox)?.SelectAll();
    }
}
