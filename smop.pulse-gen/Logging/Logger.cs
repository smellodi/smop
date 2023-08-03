using Smop.PulseGen.Utils;
using Smop.PulseGen.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace Smop.PulseGen.Logging;

public enum SavingResult
{
    None,
    Save,
    Discard,
    Cancel,
}

public abstract class RecordBase
{
    public long Timestamp { get; }

    public RecordBase() : base()
    {
        Timestamp = Utils.Timestamp.Ms;
    }

    protected static string Delim => "\t";
}

public class LogLocation
{
    public string Folder => Path.Combine(_rootFolder, _folderName);

    public LogLocation()
    {
        var settings = Properties.Settings.Default;
        _rootFolder = settings.Logger_Folder;

        if (string.IsNullOrEmpty(_rootFolder) || !Directory.Exists(_rootFolder))
        {
            _rootFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        _folderName = $"{DateTime.Now:u}".ToPath();
    }

    public SavingResult PromptToSave()
    {
        var title = $"{Application.Current.MainWindow.Title} - Logging";
        var message = $"{SaveInto}\n'{Folder}'?\n\n{PressChange}\n{PressDiscard}\n{PressCancel}";

        MsgBox.Result answer = MsgBox.Ask(title, message, Array.Empty<string>(),
            MsgBox.Button.Save, MsgBox.Button.Change, MsgBox.Button.Discard, MsgBox.Button.Cancel);

        if (answer.Button == MsgBox.Button.Discard)
        {
            if (MsgBox.Warn(title, "The data will be lost. Continue?", MsgBox.Button.Yes, MsgBox.Button.No) == MsgBox.Button.Yes)
            {
                return SavingResult.Discard;
            }
        }
        else if (answer.Button == MsgBox.Button.Change)
        {
            if (AskFolderName())
            {
                return SavingResult.Save;
            }
        }
        else if (answer.Button == MsgBox.Button.Save)
        {
            return SavingResult.Save;
        }

        return SavingResult.Cancel;
    }

    public string GetFileName(string logName)
    {
        return Path.Combine(Folder, logName + ".txt");
    }

    public void EnsureLocationExists()
    {
        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
        }
    }

    // Internal

    readonly string SaveInto = "Save data into";
    readonly string PressChange = "Press 'Change' to set another destination file";
    readonly string PressDiscard = "Press 'Discard' to discard data";
    readonly string PressCancel = "Press 'Cancel' to cancel the action";

    string _rootFolder;
    string _folderName;

    private bool AskFolderName()
    {
        var savePicker = new Microsoft.Win32.SaveFileDialog()
        {
            InitialDirectory = _rootFolder,
            FileName = _folderName,
        };

        if (savePicker.ShowDialog() ?? false)
        {
            _rootFolder = Path.GetDirectoryName(savePicker.FileName) ?? _rootFolder;
            Properties.Settings.Default.Logger_Folder = _rootFolder;
            Properties.Settings.Default.Save();

            _folderName = savePicker.FileName;

            return true;
        }

        return false;
    }
}

public interface ILog
{
    public string Name { get; }

    public bool Save(string filename);
    public void Clear();
}

public static class Logger
{
    public static SavingResult Save(ILog[] logs)
    {
        if (logs.Length == 0)
        {
            return SavingResult.None;
        }

        var logLocation = new LogLocation();
        var result = logLocation.PromptToSave();

        if (result == SavingResult.Save)
        {
            logLocation.EnsureLocationExists();
            var failsFailedToSave = new List<string>();

            foreach (var log in logs)
            {
                var filename = logLocation.GetFileName(log.Name);
                if (!log.Save(filename))
                {
                    failsFailedToSave.Add(filename);
                }
            }

            if (failsFailedToSave.Count > 0)
            {
                var listOfFiles = string.Join('\n', failsFailedToSave);
                if (MsgBox.Ask(
                    $"{Application.Current.MainWindow.Title} - Logger",
                    $"Failed to save the following files\\n'{listOfFiles}':\n\nRetry?",
                    MsgBox.Button.Yes, MsgBox.Button.No) == MsgBox.Button.Yes)
                {
                    result = Save(logs);
                }
            }
            else
            {
                MsgBox.Notify(
                    $"{Application.Current.MainWindow.Title} - Logger",
                    $"Data saved into\n'{logLocation.Folder}'",
                    MsgBox.Button.OK);
            }
        }

        if (result == SavingResult.Save || result == SavingResult.Discard)
        {
            foreach (var log in logs)
            {
                log.Clear();
            }
        }

        return result;
    }
}

public abstract class Logger<T> where T : RecordBase
{
    public bool HasRecords => _records.Count > 0;

    public bool IsEnabled { get; set; } = true;

    public bool Save(string filename)
    {
        using var writer = new StreamWriter(filename);
        try
        {
            writer.Write(RecordsToText());
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    public void Clear()
    {
        _records.Clear();
    }


    // Internal

    protected readonly List<T> _records = new();

    protected string Header { get; set; } = "";

    protected Logger() { }

    protected virtual string RecordsToText()
    {
        var stringBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(Header))
        {
            stringBuilder.AppendLine(Header);
        }

        foreach (var record in _records)
        {
            stringBuilder.AppendLine(record.ToString());
        }

        return stringBuilder.ToString();
    }
}
