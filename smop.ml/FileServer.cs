using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Smop.ML;

internal class FileServer : Server
{
    public static string Folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\smop";
    public static string MLInput = "input.txt";
    public static string MLOutput = "output.txt";

    public override bool IsClientConnected => File.Exists(_input) && File.Exists(_output);

    public FileServer()
    {
        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
        }

        _input = Path.Combine(Folder, MLInput);
        if (!File.Exists(_input))
        {
            File.Create(_input);
        }

        _output = Path.Combine(Folder, MLOutput);
        if (!File.Exists(_output))
        {
            File.Create(_output);
        }

        _watcher = new FileSystemWatcher(Folder, MLOutput);
        _watcher.Changed += OnOutput_Changed;
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.EnableRaisingEvents = true;
    }

    public override void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }


    // Internal

    long DEBOUNCE_INTERVAL = 500_0000; // 500 ms
    int READING_DELAY = 200; // ms; this is need to avoid a crash if the file is not yet closed by the counterpart

    FileSystemWatcher _watcher;
    string _input;
    string _output;

    long _lastChangeTimestamp = 0;

    protected override async Task SendTextAsync(string data)
    {
        using var input = new StreamWriter(_input);
        await input.WriteAsync(data);
    }

    private async void OnOutput_Changed(object sender, FileSystemEventArgs e)
    {
        var timestamp = DateTime.Now.Ticks;

        if (e.ChangeType == WatcherChangeTypes.Changed && timestamp > (_lastChangeTimestamp + DEBOUNCE_INTERVAL))
        {
            string data = "";
            int trialCountLeft = 5;

            while (trialCountLeft > 0)
            {
                _lastChangeTimestamp = DateTime.Now.Ticks;

                await Task.Delay(READING_DELAY);

                try
                {
                    using var output = new StreamReader(_output);
                    data = output.ReadToEnd();
                    trialCountLeft = 0;
                }
                catch (Exception)
                {
                    trialCountLeft--;
                }
            }

            ParseJson(data);
        }
    }
}
