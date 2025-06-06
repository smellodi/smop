﻿using System;
using System.IO;
using System.Threading.Tasks;
using static Smop.Common.COMUtils;

namespace Smop.ML;

internal class FileServer : Server
{
    public static string Folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\smop";
    public static string MLInput = "input.txt";
    public static string MLOutput = "output.txt";

    public override bool IsConnected => File.Exists(_input) && File.Exists(_output);
    public override string DisplayName => $"files {MLInput}/{MLOutput}";

    public FileServer()
    {
        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
        }

        _input = Path.Combine(Folder, MLInput);
        if (!File.Exists(_input))
        {
            _inputStream = File.Create(_input);
        }

        _output = Path.Combine(Folder, MLOutput);
        if (!File.Exists(_output))
        {
            _outputStream = File.Create(_output);
        }

        _watcher = new FileSystemWatcher(Folder, MLOutput);
        _watcher.Changed += OnOutput_Changed;
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
