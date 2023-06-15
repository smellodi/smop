using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using System;
using System.Linq;
using static Smop.IonVision.API;

namespace Smop.IonVision;

internal class Simulator : IMinimalAPI
{
    public string Version => "1.5";

    public Simulator()
    {
        _timer.AutoReset = false;
        _timer.Interval = SCAN_DURATION;
        _timer.Elapsed += (s, e) =>
        {
            _stopwatch.Stop();
            _latestResult = SimulatedData.ScanResult;
        };
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task<Response<SystemStatus>> GetSystemStatus()
    {
        return Task.FromResult(new Response<SystemStatus>(new SystemStatus(
            "simulator", 1, 1, true,
            new SystemStorage(1_000_000_000, 2_000_000_000),
            new GasFilter("never", 1, 1000)
        ), null));
    }
    public Task<Response<SystemInfo>> GetSystemInfo() =>
        Task.FromResult(new Response<SystemInfo>(new SystemInfo("1.5", null, new SystemVersion[] { }), null));
    public Task<Response<User>> GetUser() =>
        Task.FromResult(new Response<User>(SimulatedData.User, null));

    public Task<Response<Confirm>> SetUser(User user)
    {
        SimulatedData.User = user;
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<string[]>> GetProjects() =>
         Task.FromResult(new Response<string[]>(_projects.Select(p => p.Name).ToArray(), null));

    public Task<Response<Parameter[]>> GetParameters() =>
        Task.FromResult(new Response<Parameter[]>(_parameters, null));

    public Task<Response<ProjectAsName>> GetProject() =>
        Task.FromResult(new Response<ProjectAsName>(
            _currentProject == null ? null : new ProjectAsName(_currentProject.Name),
            _currentProject != null ? null : "No project is set as current"
        ));

    public Task<Response<Project>> GetProjectDefinition(string name)
    {
        string? error = null;

        var result = _projects.FirstOrDefault(p => p.Name == name);
        if (result == null)
        {
            error = $"No such project '{name}'";
        }

        return Task.FromResult(new Response<Project>(result, error));
    }

    public Task<Response<Confirm>> SetProject(ProjectAsName project)
    {
        var result = _projects.FirstOrDefault(p => p.Name == project.Project);
        if (result == null)
        {
            return Task.FromResult(new Response<Confirm>(null, $"Project '{project.Project}' doesn't exist"));
        }

        _currentProject = result;
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter) =>
        Task.FromResult(new Response<ParameterDefinition>(
            _currentParameter == null ? null : SimulatedData.ParameterDefinition,
            _currentParameter != null ? null : "No parameter is set as current"));

    public Task<Response<ParameterAsNameAndId>> GetParameter() =>
        Task.FromResult(new Response<ParameterAsNameAndId>(
            _currentParameter == null ? null : new ParameterAsNameAndId(_currentParameter),
            _currentParameter != null ? null : "No parameter is set as current"
        ));

    public Task<Response<Confirm>> SetParameter(ParameterAsId parameter)
    {
        var result = _parameters.FirstOrDefault(p => p.Id == parameter.Parameter);
        if (result == null)
        {
            return Task.FromResult(new Response<Confirm>(null, $"Parameter '{parameter.Parameter}' doesn't exist"));
        }

        _currentParameter = result;
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<Confirm>> PreloadParameter()
    {
        if (_currentParameter == null)
        {
            return Task.FromResult(new Response<Confirm>(null, "No parameter is set as current"));
        }

        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<Confirm>> StartScan()
    {
        if (_currentProject == null)
        {
            return Task.FromResult(new Response<Confirm>(null, "No project is set as current"));
        }
        if (_currentParameter == null)
        {
            return Task.FromResult(new Response<Confirm>(null, "No parameter set as current"));
        }
        if (_stopwatch.IsRunning)
        {
            return Task.FromResult(new Response<Confirm>(null, "Scan is in progress"));
        }

        _stopwatch.Reset();
        _stopwatch.Start();
        _timer.Start();

        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<ScanProgress>> GetScanProgress()
    {
        if (!_stopwatch.IsRunning)
        {
            return Task.FromResult(new Response<ScanProgress>(null, "Scan is not running now"));
        }

        int progress = (int)(_stopwatch.Elapsed.TotalMilliseconds / SCAN_DURATION * 100);
        return Task.FromResult(new Response<ScanProgress>(new ScanProgress(progress, new()), null));
    }

    public Task<Response<Confirm>> SetScanComments(object comment)
    {
        /*if (_latestResult == null)
        {
            return Task.FromResult(new Response<Confirm>(null, "Scan was not yet performed"));
        }
        */
        _comments = comment;
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<ScanResult>> GetLatestResult()
    {
        if (_latestResult == null)
        {
            return Task.FromResult(new Response<ScanResult>(null, "Scan was not yet performed"));
        }

        return Task.FromResult(new Response<ScanResult>(_latestResult with { Comments = _comments ?? new SimpleComment() }, null));
    }

    public Task<Response<string[]>> GetProjectResults(string name)
    {
        var project = _projects.FirstOrDefault(p => p.Name == name);
        if (project == null)
        {
            return Task.FromResult(new Response<string[]>(null, $"Project '{name}' doesn't exist"));
        }
        if (_latestResult == null)
        {
            return Task.FromResult(new Response<string[]>(null, "The project has no scans yet"));
        }

        return Task.FromResult(new Response<string[]>(new string[] { _latestResult.Id }, null));
    }

    public Task<Response<Clock>> GetClock()
    {
        return Task.FromResult(new Response<Clock>(new Clock(
            DateTime.UtcNow.ToString(),
            new Timezone(
                TimeZoneInfo.Local.BaseUtcOffset.Hours,
                TimeZoneInfo.Local.StandardName
            ),
            false
        ), null));
    }

    public Task<Response<Confirm>> SetClock(ClockToSet clock)
    {
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    // Internal

    const double SCAN_DURATION = 10000;

    readonly Stopwatch _stopwatch = new();
    readonly Timer _timer = new();

    readonly Project[] _projects = new Project[] {
            SimulatedData.Project,
            SimulatedData.Project2
        };
    readonly Parameter[] _parameters = new Parameter[] {
            SimulatedData.Parameter,
            SimulatedData.Parameter2,
        };

    Project? _currentProject = null;
    Parameter? _currentParameter = null;
    ScanResult? _latestResult = null;
    object? _comments = null;
}
