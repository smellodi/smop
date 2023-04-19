using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using System;
using System.Linq;
using static Smop.IonVision.API;

namespace Smop.IonVision;

internal class Simulator : IMinimalAPI
{
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

    public async Task<Response<SystemStatus>> GetSystemStatus()
    {
        return await Task.FromResult(new Response<SystemStatus>(new SystemStatus(
            new GasFilter("never", 1, 1000),
            "simulator", 1, 1, true,
            new SystemStorage(1_000_000_000, 2_000_000_000)
        ), null));
    }
    public async Task<Response<User>> GetUser() =>
        await Task.FromResult(new Response<User>(SimulatedData.User, null));

    public async Task<Response<Confirm>> SetUser(User user)
    {
        SimulatedData.User = user;
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<string[]>> GetProjects() =>
        await Task.FromResult(new Response<string[]>(_projects.Select(p => p.Name).ToArray(), null));

    public async Task<Response<Parameter[]>> GetParameters() =>
        await Task.FromResult(new Response<Parameter[]>(_parameters, null));

    public async Task<Response<ProjectAsName>> GetProject() =>
        await Task.FromResult(new Response<ProjectAsName>(
            _currentProject == null ? null : new ProjectAsName(_currentProject.Name),
            _currentProject != null ? null : "No project is set as current"
        ));

    public async Task<Response<Project>> GetProjectDefinition(string name)
    {
        string? error = null;

        var result = _projects.FirstOrDefault(p => p.Name == name);
        if (result == null)
        {
            error = $"No such project '{name}'";
        }

        return await Task.FromResult(new Response<Project>(result, error));
    }

    public async Task<Response<Confirm>> SetProject(ProjectAsName project)
    {
        var result = _projects.FirstOrDefault(p => p.Name == project.Project);
        if (result == null)
        {
            return await Task.FromResult(new Response<Confirm>(null, $"Project '{project.Project}' doesn't exist"));
        }

        _currentProject = result;
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<ParameterDefinition>> GetParameterDefinition(Parameter parameter) =>
        await Task.FromResult(new Response<ParameterDefinition>(
            _currentParameter == null ? null : SimulatedData.ParameterDefinition,
            _currentParameter != null ? null : "No parameter is set as current"));

    public async Task<Response<ParameterAsNameAndId>> GetParameter() =>
        await Task.FromResult(new Response<ParameterAsNameAndId>(
            _currentParameter == null ? null : new ParameterAsNameAndId(_currentParameter),
            _currentParameter != null ? null : "No parameter is set as current"
        ));

    public async Task<Response<Confirm>> SetParameter(ParameterAsId parameter)
    {
        var result = _parameters.FirstOrDefault(p => p.Id == parameter.Parameter);
        if (result == null)
        {
            return await Task.FromResult(new Response<Confirm>(null, $"Parameter '{parameter.Parameter}' doesn't exist"));
        }

        _currentParameter = result;
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

    public async Task<Response<Confirm>> SetScanComments(Comments comment)
    {
        if (_latestResult == null)
        {
            return await Task.FromResult(new Response<Confirm>(null, "Scan was not yet performed"));
        }

        _comments = comment;
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public async Task<Response<ScanResult>> GetLatestResult()
    {
        if (_latestResult == null)
        {
            return await Task.FromResult(new Response<ScanResult>(null, "Scan was not yet performed"));
        }

        return await Task.FromResult(new Response<ScanResult>(_latestResult with { Comments = _comments ?? new Comments() }, null));
    }

    public async Task<Response<string[]>> GetProjectResults(string name)
    {
        var project = _projects.FirstOrDefault(p => p.Name == name);
        if (project == null)
        {
            return await Task.FromResult(new Response<string[]>(null, $"Project '{name}' doesn't exist"));
        }
        if (_latestResult == null)
        {
            return await Task.FromResult(new Response<string[]>(null, "The project has no scans yet"));
        }

        return await Task.FromResult(new Response<string[]>(new string[] { _latestResult.Id }, null));
    }

    public async Task<Response<Clock>> GetClock()
    {
        return await Task.FromResult(new Response<Clock>(new Clock(
            DateTime.UtcNow.ToString(),
            new Timezone(
                TimeZoneInfo.Local.BaseUtcOffset.Hours,
                TimeZoneInfo.Local.StandardName
            ),
            false
        ), null));
    }

    public async Task<Response<Confirm>> SetClock(Clock clock)
    {
        return await Task.FromResult(new Response<Confirm>(new Confirm(), null));
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
    Comments? _comments = null;
}
