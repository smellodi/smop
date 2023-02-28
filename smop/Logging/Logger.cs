using System;
using System.Collections.Generic;
using System.IO;
using SMOP.Utils;

namespace SMOP
{
    [Flags]
    public enum LogSource
    {
        COM = 1,
        PID = 2,

        __MIN_TEST_ID = 4,
        ThTest = 4,
        OdProd = 8,
    }

    public enum SavingResult
    {
        None,
        Save,
        Discard,
        Cancel,
    }

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

    public abstract class Logger<T> where T : class
    {
        public LoggerStorage File { get; private set; } = new();

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
                    filename = filename.Substring(0, filename.Length - 1 - LoggerStorage.FILE_EXT.Length) + ending;
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

        public void Clear()
        {
            _records.Clear();
        }


        // Internal

        protected const string NAME = "olfactory";

        protected readonly List<T> _records = new();

        protected abstract string Header { get; }


        protected Logger() { }

        protected (SavingResult, bool) PromptToSave(ref string filename, bool canCancel, string greeting = "")
        {
            if (!string.IsNullOrEmpty(greeting))
            {
                greeting += "\n";
            }

            var saveInto = L10n.T("SaveDataInto");
            var pressChange = L10n.T("PressChange");
            var pressDicard = L10n.T("PressDiscard");
            var pressCancel = L10n.T("PressCancel");

            var title = L10n.T("OlfactoryTestTool") + " - " + L10n.T("Logger");
            var message = $"{greeting}{saveInto}\n'{File.Folder}\\{filename}'?\n\n{pressChange}\n{pressDicard}";
            MsgBox.Result answer;

            var msgBoxOptionText = L10n.T("EnableAutoSaving");

            if (canCancel)
            {
                message += $"\n{pressCancel}";
                answer = MsgBox.Custom(title, message, MsgBox.MsgIcon.Question, msgBoxOptionText, null, 
                    MsgBox.Button.Save, MsgBox.Button.Change, MsgBox.Button.Discard, MsgBox.Button.Cancel);
            }
            else
            {
                answer = MsgBox.Custom(title, message, MsgBox.MsgIcon.Question, msgBoxOptionText, null,
                    MsgBox.Button.Save, MsgBox.Button.Change, MsgBox.Button.Discard);
            }

            if (answer.Button == MsgBox.Button.Discard)
            {
                if (MsgBox.Warn(title, L10n.T("ConfirmDiscard"), MsgBox.Button.Yes, MsgBox.Button.No) == MsgBox.Button.Yes)
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

        protected bool Save(string? filename, IEnumerable<object> records, string header, bool suppressConfirmation)
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

            using (StreamWriter writer = System.IO.File.CreateText(filename))
            {
                try
                {
                    if (!string.IsNullOrEmpty(header))
                    {
                        writer.WriteLine(header);
                    }

                    writer.WriteLine(string.Join("\n", records));

                    if (!suppressConfirmation)
                    {
                        var dataSavedInto = L10n.T("DataSavedInto");
                        MsgBox.Notify(
                            L10n.T("OlfactoryTestTool") + " - " + L10n.T("Logger"),
                            $"{dataSavedInto}\n'{filename}'",
                            MsgBox.Button.OK);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    var failedToSave = L10n.T("FailedToSave");
                    var retry = L10n.T("Retry");
                    var answer = MsgBox.Ask(
                        L10n.T("OlfactoryTestTool") + " - " + L10n.T("Logger"),
                        $"{failedToSave}\n'{filename}':\n\n{ex.Message}\n\n{retry}",
                        MsgBox.Button.Yes, MsgBox.Button.No);
                    if (answer == MsgBox.Button.Yes)
                    {
                        return Save(AskFileName(filename), records, header, suppressConfirmation);
                    }
                }
            }

            return false;
        }
    }
}
