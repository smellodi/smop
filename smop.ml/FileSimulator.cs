using System;
using System.IO;
using System.Threading.Tasks;

namespace Smop.ML;

internal class FileSimulator : Simulator
{
    public FileSimulator()
    {
        if (!Directory.Exists(FileServer.Folder))
        {
            Directory.CreateDirectory(FileServer.Folder);
        }

        _input = Path.Combine(FileServer.Folder, FileServer.MLInput);
        if (!File.Exists(_input))
        {
            _inputStream = File.Create(_input);
        }

        _output = Path.Combine(FileServer.Folder, FileServer.MLOutput);
        if (!File.Exists(_output))
        {
            _outputStream = File.Create(_output);
        }

        _watcher = new FileSystemWatcher(FileServer.Folder, FileServer.MLInput);
        _watcher.Changed += OnInput_Changed;
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.EnableRaisingEvents = true;
    }

    public override void Dispose()
    {
        _inputStream?.Dispose();
        _outputStream?.Dispose();
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }


    // Internal

    const long DEBOUNCE_INTERVAL = 500_0000; // 500 ms
    const int READING_DELAY = 200; // ms; this is need to avoid a crash if the file is not yet closed by the counterpart

    readonly FileSystemWatcher _watcher;
    readonly string _input;
    readonly string _output;

    readonly FileStream? _inputStream;
    readonly FileStream? _outputStream;

    long _lastChangeTimestamp = 0;

    protected override async Task SendData(string data)
    {
        using var output = new StreamWriter(_output);
        await output.WriteAsync(data);
    }

    private async void OnInput_Changed(object sender, FileSystemEventArgs e)
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
                    using var input = new StreamReader(_input);
                    data = input.ReadToEnd();
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
