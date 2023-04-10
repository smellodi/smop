using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;

namespace Smop.IonVision
{
    internal class Simulator : IMinimalAPI
    {
        public Simulator()
        {
            _timer.AutoReset = false;
            _timer.Interval = SCAN_DURATION;
            _timer.Elapsed += (s, e) =>
            {
                _stopwatch.Stop();
                _latestResult = new ScanResult(
                    "07D26C66-33E9-48FA-9877-4F64156D6B75",
                    _user.Name,
                    "2023-04-05T14:38:58.349Z",
                    "2023-04-05T14:40:10.288Z",
                    _parameter.Id,
                    _project.Name,
                    new(),
                    3,
                    new SystemData(
                        new ErrorRegister(false, false, false, false, false, false,
                            false, false,
                            true, false, false, false, false, false, false, false,
                            true, false, false, false, false, false, false, false,
                            false, false,
                            false, false
                        ),
                        new(0, 17657, 0),
                        new FlowDetector(
                            new(0.11, 301.26, 0),
                            new(21.81, 0, 21.75),
                            new(1100, 21.78, 1018.5),
                            new(20.12, 0.67, 17.84),
                            new(90, 30, 90)
                        ),
                        new FlowDetector(
                            new(3.92, 578.74, 3.29),
                            new(21.68, 589.82, 21.6),
                            new(1019.96, 21.68, 960.51),
                            new(17.4, 2.16, 0.93),
                            new(90, 1, 90)
                        ),
                        new Detector(
                            new(26, 589.82, 25.94),
                            new(1017.33, 25.99, 1017.12),
                            new(17.34, 99.34, 17.24)
                        )
                    ),
                    new MeasurementData(
                        true,
                        12,
                        new float[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                        new float[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                        new float[] { 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3 },
                        new float[] { 1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3 },
                        new float[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
                        new float[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
                        new float[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 },
                        new short[] { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3 }
                    )
                );
            };
        }

        public async Task<API.Response<SystemStatus>> GetSystemStatus()
        {
            return await Task.FromResult(new API.Response<SystemStatus>(new SystemStatus(
                new GasFilter("never", 1, 1000),
                "simulator",
                1,
                1,
                true,
                new SystemStorage(1_000_000_000, 2_000_000_000)
            ), null));
        }
        public async Task<API.Response<User>> GetUser() => 
            await Task.FromResult(new API.Response<User>(_user, null));
        public async Task<API.Response<Error>> SetUser(User user)
        {
            _user = user;
            return await Task.FromResult(new API.Response<Error>(new Error("OK"), null));
        }
        public async Task<API.Response<string[]>> GetProjects() => 
            await Task.FromResult(new API.Response<string[]>(new string[] { _project.Name }, null));
        public async Task<API.Response<Parameter[]>> GetParameters() =>
            await Task.FromResult(new API.Response<Parameter[]>(new Parameter[] { _parameter }, null));
        public async Task<API.Response<Error>> SetProject(ProjectAsName project)
        {
            if (project.Project != _project.Name)
            {
                return await Task.FromResult(new API.Response<Error>(null, $"Project '{project.Project}' doesn't exist"));
            }

            _currentProject = _project;
            return await Task.FromResult(new API.Response<Error>(new Error("OK"), null));
        }
        public async Task<API.Response<Error>> SetParameter(ParameterAsId parameter)
        {
            if (parameter.Parameter != _parameter.Id)
            {
                return await Task.FromResult(new API.Response<Error>(null, $"Parameter '{parameter.Parameter}' doesn't exist"));
            }

            _currentParameter = _parameter;
            return await Task.FromResult(new API.Response<Error>(new Error("OK"), null));
        }
        public async Task<API.Response<Error>> PreloadParameter()
        {
            if (_currentParameter == null)
            {
                return await Task.FromResult(new API.Response<Error>(null, "No parameter is set as current"));
            }
            return await Task.FromResult(new API.Response<Error>(new Error("OK"), null));
        }
        public async Task<API.Response<Error>> StartScan()
        {
            if (_currentProject == null)
            {
                return await Task.FromResult(new API.Response<Error>(null, "No project is set as current"));
            }
            if (_currentParameter == null)
            {
                return await Task.FromResult(new API.Response<Error>(null, "No parameter set as current"));
            }
            if (_stopwatch.IsRunning)
            {
                return await Task.FromResult(new API.Response<Error>(null, "Scan is in progress"));
            }

            _stopwatch.Reset();
            _stopwatch.Start();
            _timer.Start();

            return await Task.FromResult(new API.Response<Error>(new Error("OK"), null));
        }
        public async Task<API.Response<ScanProgress>> GetScanProgress()
        {
            if (!_stopwatch.IsRunning)
            {
                return await Task.FromResult(new API.Response<ScanProgress>(null, "Scan is not running now"));
            }

            int progress = (int)(_stopwatch.Elapsed.TotalMilliseconds / SCAN_DURATION * 100);
            return await Task.FromResult(new API.Response<ScanProgress>(new ScanProgress(progress, new()), null));
        }
        public async Task<API.Response<ScanResult>> GetLatestResult()
        {
            if (_latestResult == null)
            {
                return await Task.FromResult(new API.Response<ScanResult>(null, "Scan was not yet performed"));
            }

            return await Task.FromResult(new API.Response<ScanResult>(_latestResult, null));
        }
        public async Task<API.Response<string[]>> GetProjectResults(ProjectAsName project)
        {
            if (project.Project != _project.Name)
            {
                return await Task.FromResult(new API.Response<string[]>(null, $"Project '{project.Project}' doesn;t exist"));
            }
            if (_latestResult == null)
            {
                return await Task.FromResult(new API.Response<string[]>(null, "The project has no scan yet"));
            }

            return await Task.FromResult(new API.Response<string[]>(new string[] { _latestResult.Id }, null));
        }

        // Internal

        static readonly Settings _setting = Settings.Instance;
        static readonly Parameter _parameter = new Parameter(_setting.Parameter, "Default");

        const double SCAN_DURATION = 10000;

        User _user = new(_setting.User);
        
        readonly Project _project = new(_setting.Project, new Parameter[] { _parameter });
        readonly Stopwatch _stopwatch = new();
        readonly Timer _timer = new();

        Project? _currentProject = null;
        Parameter? _currentParameter = null;
        ScanResult? _latestResult = null;
    }
}
