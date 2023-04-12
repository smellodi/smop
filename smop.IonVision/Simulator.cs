using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using static Smop.IonVision.API;

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
                _latestResult = _scanResult;
            };
        }

        public async Task<Response<SystemStatus>> GetSystemStatus()
        {
            return await Task.FromResult(new Response<SystemStatus>(new SystemStatus(
                new GasFilter("never", 1, 1000),
                "simulator", 1, 1, true,
                new SystemStorage(1_000_000_000, 2_000_000_000)
            ), null));
        }
        public async Task<Response<User>> GetUser() => 
            await Task.FromResult(new Response<User>(_user, null));

        public async Task<Response<Confirm>> SetUser(User user)
        {
            _user = user;
            return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }

        public async Task<Response<string[]>> GetProjects() => 
            await Task.FromResult(new Response<string[]>(new string[] { _project.Name }, null));

        public async Task<Response<Parameter[]>> GetParameters() =>
            await Task.FromResult(new Response<Parameter[]>(new Parameter[] { _parameter }, null));

        public async Task<Response<ProjectAsName>> GetProject() =>
            await Task.FromResult(new Response<ProjectAsName>(
                _currentProject == null ? null : new ProjectAsName(_currentProject.Name),
                _currentProject != null ? null : "No project is set as current"
            ));

        public async Task<Response<Confirm>> SetProject(ProjectAsName project)
        {
            if (project.Project != _project.Name)
            {
                return await Task.FromResult(new Response<Confirm>(null, $"Project '{project.Project}' doesn't exist"));
            }

            _currentProject = _project;
            return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }

        public async Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter) =>
            await Task.FromResult(new Response<ParameterDefinition>(
                _currentParameter == null ? null : _parameterDefinition,
                _currentParameter != null ? null : "No parameter is set as current"));

        public async Task<Response<ParameterAsNameAndId>> GetParameter() =>
            await Task.FromResult(new Response<ParameterAsNameAndId>(
                _currentParameter == null ? null : new ParameterAsNameAndId(_currentParameter),
                _currentParameter != null ? null : "No parameter is set as current"
            ));

        public async Task<Response<Confirm>> SetParameter(ParameterAsId parameter)
        {
            if (parameter.Parameter != _parameter.Id)
            {
                return await Task.FromResult(new Response<Confirm>(null, $"Parameter '{parameter.Parameter}' doesn't exist"));
            }

            _currentParameter = _parameter;
            return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }

        public async Task<Response<Confirm>> PreloadParameter()
        {
            if (_currentParameter == null)
            {
                return await Task.FromResult(new Response<Confirm>(null, "No parameter is set as current"));
            }
            return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }

        public async Task<Response<Confirm>> StartScan()
        {
            if (_currentProject == null)
            {
                return await Task.FromResult(new Response<Confirm>(null, "No project is set as current"));
            }
            if (_currentParameter == null)
            {
                return await Task.FromResult(new Response<Confirm>(null, "No parameter set as current"));
            }
            if (_stopwatch.IsRunning)
            {
                return await Task.FromResult(new Response<Confirm>(null, "Scan is in progress"));
            }

            _stopwatch.Reset();
            _stopwatch.Start();
            _timer.Start();

            return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }

        public async Task<Response<ScanProgress>> GetScanProgress()
        {
            if (!_stopwatch.IsRunning)
            {
                return await Task.FromResult(new Response<ScanProgress>(null, "Scan is not running now"));
            }

            int progress = (int)(_stopwatch.Elapsed.TotalMilliseconds / SCAN_DURATION * 100);
            return await Task.FromResult(new Response<ScanProgress>(new ScanProgress(progress, new()), null));
        }

        public async Task<Response<ScanResult>> GetLatestResult()
        {
            if (_latestResult == null)
            {
                return await Task.FromResult(new Response<ScanResult>(null, "Scan was not yet performed"));
            }

            return await Task.FromResult(new Response<ScanResult>(_latestResult, null));
        }

        public async Task<Response<string[]>> GetProjectResults(ProjectAsName project)
        {
            if (project.Project != _project.Name)
            {
                return await Task.FromResult(new Response<string[]>(null, $"Project '{project.Project}' doesn;t exist"));
            }
            if (_latestResult == null)
            {
                return await Task.FromResult(new Response<string[]>(null, "The project has no scan yet"));
            }

            return await Task.FromResult(new Response<string[]>(new string[] { _latestResult.Id }, null));
        }

        // Internal

        const double SCAN_DURATION = 10000;

        const int DATA_ROWS = 3;
        const int DATA_COLS = 4;
        const int DATA_POINT_COUNT = DATA_ROWS * DATA_COLS;
        const int DATA_PP = 1000;
        const int DATA_PW = 200;
        const short DATA_SAMPLE_COUNT = 64;

        static readonly Settings _setting = Settings.Instance;
        static User _user = new(_setting.User);
        static readonly Parameter _parameter = new(_setting.ParameterId, _setting.ParameterName);
        static readonly Project _project = new(_setting.Project, new Parameter[] { _parameter });
        static readonly ParameterDefinition _parameterDefinition = new(
            _parameter.Id,
            _parameter.Name,
            "2022-12-09T13:27:46.945Z",
            "Scan simulation",
            new SystemParameters(
                500000000,
                new SampleSensor(
                    90,
                    new RangeWithPID(0.2f, 2, new PID(0.7f, 1, 0.5f, 200, -200, 0.02f, 0.5f)),
                    new Range(10, 45),
                    new Range(900, 1200),
                    new Range(0, 40),
                    new RangeWithPID(0, 90, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                    0
                ),
                new Ambient(new Range(0, 30), new Range(900, 1300), new Range(0, 40)),
                new Miscellaneous(new Range(0, 80)),
                new SampleSensor(
                    90,
                    new RangeWithPID(2, 6, new PID(1, 2, 1, 200, -200, 0.03f, 4.5f)),
                    new Range(10, 45),
                    new Range(800, 1200),
                    new Range(0, 40),
                    new RangeWithPID(0, 80, new PID(1, 0.1f, 0.1f, 2, -2, 0.1f, 35)),
                    0
                )
            ),
            new MeasurementParameters(
                0,
                false,
                true,
                new Delays(100000000, 300, 10000000, 3000000, 5000000000, 200000000),
                new SteppingControl(
                    new StepRange(200, 800, DATA_COLS),
                    new StepRange(-3, 13, DATA_ROWS),
                    new StepRange(-6, -6, 1),
                    DATA_PP,
                    DATA_PW,
                    DATA_SAMPLE_COUNT,
                    1
                ),
                new PointConfiguration(
                    new float[DATA_POINT_COUNT] { 200, 200, 200, 400, 400, 400, 600, 600, 600, 800, 800, 800 },
                    new float[DATA_POINT_COUNT] { -3, 5, 13, -3, 5, 13, -3, 5, 13, -3, 5, 13 },
                    new float[DATA_POINT_COUNT] { -6, -6, -6, -6, -6, -6, -6, -6, -6, -6, -6, -6 },
                    new float[DATA_POINT_COUNT] { DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP, DATA_PP },
                    new float[DATA_POINT_COUNT] { DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW, DATA_PW },
                    new short[DATA_POINT_COUNT] { DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, 
                        DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT, DATA_SAMPLE_COUNT }
                )
            ),
            6
        );

        static readonly ScanResult _scanResult = new(
            "07d26c66-33e9-48fa-9877-4f64156d6b75",
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
                DATA_POINT_COUNT,
                new float[DATA_POINT_COUNT] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                new float[DATA_POINT_COUNT] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                _parameterDefinition.MeasurementParameters.PointConfiguration.Usv,
                _parameterDefinition.MeasurementParameters.PointConfiguration.Ucv,
                _parameterDefinition.MeasurementParameters.PointConfiguration.Vb,
                _parameterDefinition.MeasurementParameters.PointConfiguration.PP,
                _parameterDefinition.MeasurementParameters.PointConfiguration.PW,
                _parameterDefinition.MeasurementParameters.PointConfiguration.NForSampleAverages
            )
        );
        readonly Stopwatch _stopwatch = new();
        readonly Timer _timer = new();

        Project? _currentProject = null;
        Parameter? _currentParameter = null;
        ScanResult? _latestResult = null;
    }
}
