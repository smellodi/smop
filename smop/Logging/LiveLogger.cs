using System;
using System.IO;

namespace Smop
{
    public class LiveLogger : IDisposable
    {
        public static LiveLogger Instance => _instance ??= new();

        public LiveLogger()
        {
            string folder = GetAppSettingsFolder();
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = folder + @"\debug_log.txt";

            if (File.Exists(path))
            {
                var newPath = folder + @"\debug_log_prev.txt";
                File.Move(path, newPath, true);
            }

            _file = new StreamWriter(path);
        }

        public async System.Threading.Tasks.Task AddAsync(string evt)
        {
            try
            {
                await _file.WriteLineAsync($"{Utils.Timestamp.Ms}\t{evt}");
                await _file.FlushAsync();
            }
            catch { }
        }

        public async void Dispose()
        {
            await _file.DisposeAsync();
        }

        // Internal

        static LiveLogger? _instance = null;

        readonly StreamWriter _file;

        private string GetAppSettingsFolder()
        {
            var sysAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appName = AppDomain.CurrentDomain.FriendlyName;
            return $@"{sysAppDataFolder}\{appName}";
        }
    }
}
