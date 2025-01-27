using Smop.MainApp.Dialogs;
using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Smop.MainApp.Logging;

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

public interface ILog
{
    public string Name { get; }
    public string Extension { get; }

    public bool Save(string filename);
    public void Clear();
}

public static class Logger
{
    /// <summary>
    /// Save the log files into the same folder
    /// </summary>
    /// <param name="logs">The logs to save</param>
    /// <returns>Saving result and the folder where the log files were save</returns>
    public static (SavingResult, string?) Save(ILog[] logs)
    {
        if (logs.Length == 0)
        {
            return (SavingResult.None, null);
        }

        var logLocation = new LogLocation();
        var result = logLocation.PromptToSave();
        string? folder = null;

        if (result == SavingResult.Save)
        {
            logLocation.EnsureLocationExists();
            folder = logLocation.Folder;

            var failedToSave = new List<string>();

            foreach (var log in logs)
            {
                var filename = logLocation.GetFileName(log.Name, log.Extension);
                if (!log.Save(filename))
                {
                    failedToSave.Add(filename);
                }
            }

            if (failedToSave.Count > 0)
            {
                var listOfFiles = string.Join('\n', failedToSave);
                if (MsgBox.Ask(
                    $"{App.Name} - Logger",
                    $"Failed to save the following files\\n'{listOfFiles}':\n\nRetry?",
                    MsgBox.Button.Yes, MsgBox.Button.No) == MsgBox.Button.Yes)
                {
                    (result, folder) = Save(logs);
                }
            }
            else
            {
                MsgBox.Notify(
                    $"{App.Name} - Logger",
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

        return (result, folder);
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

public class LogLocation
{
    //public static LogLocation Instance => _instance ??= new();

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
        var title = $"{App.Name} - Logger";
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

    public string GetFileName(string logName, string extension)
    {
        return Path.Combine(Folder, $"{logName}.{extension}");
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
