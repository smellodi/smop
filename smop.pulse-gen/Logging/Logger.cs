using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Smop.PulseGen.Utils;

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
/*
public class LoggerStorage
{
	public string? Folder
	{
		get => _folder;
		set
		{
			_folder = value;
			Properties.Settings.Default.Logger_Folder = value;
			Properties.Settings.Default.Save();
		}
	}
	public string? Name { get; set; }
	public string? SubName { get; set; }

	public bool Exists => !string.IsNullOrEmpty(Name);

	public LoggerStorage()
	{
		Load();
	}

	public string To(string subname, string timestamp)
	{
		string filename;

		var p = Name?.Split(DELIM);

		if (p?.Length > 2 && p[^1] == SubName)
		{
			p[^2] = timestamp;
			p[^1] = subname;
			filename = string.Join(DELIM, p) + "." + FILE_EXT;
		}
		else
		{
			filename = Name + DELIM + timestamp + DELIM + subname + "." + FILE_EXT;
		}

		return Path.Combine(_folder ?? "", filename.ToPath());
	}

	public void SaveFilenameBase(bool save)
	{
		var settings = Properties.Settings.Default;
		settings.Logger_FilenameBase = save ? Name : null;
		settings.Save();
	}

	public void Load()
	{
		var settings = Properties.Settings.Default;
		_folder = settings.Logger_Folder;
		Name = settings.Logger_FilenameBase;

		if (string.IsNullOrEmpty(Folder))
		{
			_folder = DEFAULT_FOLDER;
		}
		else if (!Directory.Exists(_folder))
		{
			_folder = DEFAULT_FOLDER;
			Name = null;
		}

		if (!string.IsNullOrEmpty(Name))
		{
			var p = Name.Split(DELIM);
			SubName = p[^1];
		}
	}

	public const char DELIM = '_';
	public const string FILE_EXT = "txt";

	public static readonly string DEFAULT_FOLDER = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

	private string? _folder;
}
*/
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

	/*
    public SavingResult SaveTo(string subname, string timestamp, bool canCancel, LoggerStorage? reference)
	{
		var result = SavingResult.Save;
		bool? memorize = null;
		bool suppressConfirmation = false;

		string filename;

		if (reference == null)
		{
			File.Load();       // remove data left from a possible previous call of this method
			reference = File;
		}

		if (reference.Exists)
		{
			filename = reference.To(subname, timestamp);
			suppressConfirmation = true;
		}
		else
		{
			var ending = $"{LoggerStorage.DELIM}{timestamp}{LoggerStorage.DELIM}{subname}.{LoggerStorage.FILE_EXT}".ToPath();
			filename = $"{NAME}{ending}";
			(result, memorize) = PromptToSave(ref filename, canCancel);

			if (filename != null && !filename.EndsWith(ending)) // force to place the timestamp and subname at the end
			{
				filename = filename[..(filename.Length - 1 - LoggerStorage.FILE_EXT.Length)] + ending;
			}
		}

		if (result == SavingResult.Save)
		{
			result = Save(filename!, _records, Header, suppressConfirmation) ? SavingResult.Save : SavingResult.Cancel;
			File.Name = Path.GetFileNameWithoutExtension(filename!);
			File.SubName = subname;

			if (memorize != null)
			{
				File.SaveFilenameBase(memorize ?? false);
			}
		}

		if (result == SavingResult.Save || result == SavingResult.Discard)
		{
			_records.Clear();
		}

		return result;
	}
	*/
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

	/*
    readonly string SaveInto = "Save data into";
    readonly string PressChange = "Press 'Change' to set another destination file";
    readonly string PressDiscard = "Press 'Discard' to discard data";
    readonly string PressCancel = "Press 'Cancel' to cancel the action";
    readonly string EnableAutoSaving = "Enable auto-saving";
	*/
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

    /*
	protected (SavingResult, bool) PromptToSave(ref string filename, bool canCancel, string greeting = "")
	{
		if (!string.IsNullOrEmpty(greeting))
		{
			greeting += "\n";
		}

		var title = $"{Application.Current.MainWindow.Title} - Logger";
		var message = $"{greeting}{SaveInto}\n'{File.Folder}\\{filename}'?\n\n{PressChange}\n{PressDiscard}";

		MsgBox.Result answer;

		if (canCancel)
		{
			message += $"\n{PressCancel}";
			answer = MsgBox.Custom(title, message, MsgBox.MsgIcon.Question, EnableAutoSaving, null, 
				MsgBox.Button.Save, MsgBox.Button.Change, MsgBox.Button.Discard, MsgBox.Button.Cancel);
		}
		else
		{
			answer = MsgBox.Custom(title, message, MsgBox.MsgIcon.Question, EnableAutoSaving, null,
				MsgBox.Button.Save, MsgBox.Button.Change, MsgBox.Button.Discard);
		}

		if (answer.Button == MsgBox.Button.Discard)
		{
			if (MsgBox.Warn(title, "The data will be lost. Continue?", MsgBox.Button.Yes, MsgBox.Button.No) == MsgBox.Button.Yes)
			{
				return (SavingResult.Discard, false);
			}
		}
		else if (answer.Button == MsgBox.Button.Change)
		{
			filename = AskFileName(filename) ?? "";
			return (string.IsNullOrEmpty(filename) ? SavingResult.Cancel : SavingResult.Save, answer.IsOptionAccepted);
		}
		else if (answer.Button == MsgBox.Button.Save)
		{
			filename = Path.Combine(File.Folder ?? "", filename);
			return (SavingResult.Save, answer.IsOptionAccepted);
		}

		return (SavingResult.Cancel, false);
	}

	protected string? AskFileName(string? defaultFileName)
	{
		var savePicker = new Microsoft.Win32.SaveFileDialog()
		{
			DefaultExt = "txt",
			FileName = defaultFileName,
			Filter = "Log files (*.txt)|*.txt"
		};

		if (savePicker.ShowDialog() ?? false)
		{
			File.Folder = Path.GetDirectoryName(savePicker.FileName) ?? Environment.CurrentDirectory;
			return savePicker.FileName;
		}

		return null;
	}

	protected bool Save(string? filename, IEnumerable<T> records, string header, bool suppressConfirmation)
	{
		if (string.IsNullOrEmpty(filename))
		{
			return false;
		}

		var folder = Path.GetDirectoryName(filename) ?? Environment.CurrentDirectory;

		if (!Directory.Exists(folder))
		{
			Directory.CreateDirectory(folder);
		}

        using StreamWriter writer = System.IO.File.CreateText(filename);
        try
        {
            if (!string.IsNullOrEmpty(header))
            {
                writer.WriteLine(header);
            }

            writer.WriteLine(string.Join("\n", records));

            if (!suppressConfirmation)
            {
                MsgBox.Notify(
                    $"{Application.Current.MainWindow.Title} - Logger",
                    $"Data saved into\n'{filename}'",
                    MsgBox.Button.OK);
            }

            return true;
        }
        catch (Exception ex)
        {
            var answer = MsgBox.Ask(
                $"{Application.Current.MainWindow.Title} - Logger",
                $"Failed to save the file\n'{filename}':\n\n{ex.Message}\n\nRetry",
                MsgBox.Button.Yes, MsgBox.Button.No);
            if (answer == MsgBox.Button.Yes)
            {
                return Save(AskFileName(filename), records, header, suppressConfirmation);
            }
        }

        return false;
	}*/
}
