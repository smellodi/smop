using Fleck;
using Smop.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using static Smop.IonVision.API;

namespace Smop.IonVision;

internal class Simulator : IMinimalAPI
{
    public string Version => "1.5";

    public Simulator()
    {
        _wsServer = new WebSocketServer("ws://0.0.0.0:80");
        _wsServer.ListenerSocket.NoDelay = true;
        _wsServer.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                ScreenLogger.Print($"[IvWsServer] Opened from {socket.ConnectionInfo.ClientIpAddress}");
                _sockets.Add(socket);
            };
            socket.OnClose = () =>
            {
                ScreenLogger.Print("[IvWsServer] Closed");
                _sockets.Remove(socket);
            };
            // socket.OnMessage = message => { ignore messages };
        });

        _scanTimer.AutoReset = false;
        _scanTimer.Interval = SCAN_DURATION;
        _scanTimer.Elapsed += (s, e) =>
        {
            _scanProgressTimer.Stop();
            _stopwatch.Stop();
            _latestResult = RandomizeScanData(SimulatedData.ScanResult);

            Notify("scan.finished");
            DispatchOnce.Do(1, () => Notify("scan.resultsProcessed"));
        };

        _scanProgressTimer.AutoReset = true;
        _scanProgressTimer.Interval = SCAN_PROGRESS_STEP_DURATION;
        _scanProgressTimer.Elapsed += (s, e) =>
        {
            int progress = _scopeTimer.Enabled ?
                (int)Math.Min(100, _scopeProgressStopwatch.Elapsed.TotalMilliseconds / SCOPE_DURATION * 100) :
                (int)(_stopwatch.Elapsed.TotalMilliseconds / SCAN_DURATION * 100);
            Notify("scan.progress", new EventSink.ProcessProgress(progress));
        };

        _scopeTimer.AutoReset = true;
        _scopeTimer.Interval = SCOPE_DURATION;
        _scopeTimer.Elapsed += (s, e) =>
        {
            _scopeProgressStopwatch.Restart();

            _scopeData = RandomizeScopeData(SimulatedData.ScopeResult);
            _scopeStatus = _scopeStatus with { Progress = 0 };

            Notify("scope.data", _scopeData);
        };

        _scopeProgressTimer.AutoReset = true;
        _scopeProgressTimer.Interval = SCOPE_PROGRESS_STEP_DURATION;
        _scopeProgressTimer.Elapsed += (s, e) =>
        {
            int progress = (int)(_scopeProgressStopwatch.Elapsed.TotalMilliseconds / SCOPE_DURATION * 100);
            _scopeStatus = _scopeStatus with { Progress = Math.Min(100, progress) };
        };
    }

    public void Dispose()
    {
        _wsServer.Dispose();
        _scanTimer.Dispose();
        _scanProgressTimer.Dispose();
        _scopeTimer.Dispose();
        _scopeProgressTimer.Dispose();

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
        Task.FromResult(new Response<SystemInfo>(new SystemInfo(Version, null, Array.Empty<SystemVersion>()), null));
    public Task<Response<User>> GetUser() =>
        Task.FromResult(new Response<User>(SimulatedData.User, null));

    public Task<Response<Confirm>> SetUser(User user)
    {
        SimulatedData.User = user;
        Notify("scan.usernameChanged", new EventSink.ScanUserName(user.Name));
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

        Notify("project.currentChanged", new EventSink.CurrentProject(project.Project));
        Notify("project.setupCurrentStarted");

        Wait(1000);

        _currentProject = result;
        Notify("project.setupCurrentFinished");

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

        Notify("parameter.currentChanged", new EventSink.CurrentParameter(result));
        Notify("parameter.setupCurrentStarted");

        Wait(1000);

        _currentParameter = result;
        Notify("parameter.setupCurrentFinished");

        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<Confirm>> PreloadParameter()
    {
        if (_currentParameter == null)
        {
            return Task.FromResult(new Response<Confirm>(null, "No parameter is set as current"));
        }

        Notify("parameter.preloadStarted");
        Wait(300);
        Notify("parameter.preloadFinished");

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
        if (_scopeStatus.Progress >= 0)
        {
            return Task.FromResult(new Response<Confirm>(null, "In scope mode"));
        }

        _stopwatch.Reset();
        _stopwatch.Start();
        _scanTimer.Start();
        _scanProgressTimer.Start();

        Notify("scan.started");
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

        var data = _comments.GetType().GetProperties().Select(prop =>
            new KeyValuePair<string, string>(prop.Name, prop.GetValue(_comments, null)?.ToString() ?? "")).ToArray();
        Notify("scan.commentsChanged", data);

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
        Notify("message.timeChanged", new EventSink.Timestamp((long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds));
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }

    public Task<Response<ScopeStatus>> CheckScopeMode() =>
        _scopeStatus.Progress < 0 ?
            Task.FromResult(new Response<ScopeStatus>(null, "Not in the scope mode")) :
            Task.FromResult(new Response<ScopeStatus>(_scopeStatus, null));

    public Task<Response<Confirm>> EnableScopeMode()
    {
        if (_scopeStatus.Progress < 0)
        {
            _scopeStatus = _scopeStatus with { Progress = 0 };
            _scopeProgressStopwatch.Start();
            _scopeTimer.Start();
            _scopeProgressTimer.Start();
            _scanProgressTimer.Start();

            Notify("scope.started");

            return Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }
        else
        {
            return Task.FromResult(new Response<Confirm>(null, "Already in the scope mode"));
        }
    }

    public Task<Response<Confirm>> DisableScopeMode()
    {
        if (_scopeStatus.Progress < 0)
        {
            return Task.FromResult(new Response<Confirm>(null, "Not in the scope mode"));
        }
        else
        {
            _scanProgressTimer.Stop();
            _scopeProgressTimer.Stop();
            _scopeTimer.Stop();
            _scopeProgressStopwatch.Stop();
            _scopeStatus = _scopeStatus with { Progress = -1 };

            Notify("scope.stopped");

            return Task.FromResult(new Response<Confirm>(new Confirm(), null));
        }
    }

    public Task<Response<ScopeResult>> GetScopeResult() =>
        _scopeStatus.Progress < 0 || _scopeData == null ?
            Task.FromResult(new Response<ScopeResult>(null, "Not available")) :
            Task.FromResult(new Response<ScopeResult>(_scopeData, null));

    public Task<Response<ScopeParameters>> GetScopeParameters() =>
        Task.FromResult(new Response<ScopeParameters>(SimulatedData.ScopeParameters, null));

    public Task<Response<Confirm>> SetScopeParameters(ScopeParameters parameters)
    {
        SimulatedData.ScopeParameters = parameters;
        Notify("scope.parametersChanged", SimulatedData.ScopeParameters);
        return Task.FromResult(new Response<Confirm>(new Confirm(), null));
    }


    // Internal

    const double SCAN_DURATION = 2500;                 // ms
    const double SCAN_PROGRESS_STEP_DURATION = 300;    // ms
    const double SCOPE_DURATION = 2000;                // ms
    const double SCOPE_PROGRESS_STEP_DURATION = 100;        // ms

    readonly WebSocketServer _wsServer;
    readonly List<IWebSocketConnection> _sockets = new();

    readonly Stopwatch _stopwatch = new();
    readonly Timer _scanTimer = new();
    readonly Timer _scanProgressTimer = new();
    readonly Timer _scopeTimer = new();
    readonly Timer _scopeProgressTimer = new();
    readonly Stopwatch _scopeProgressStopwatch = new();

    readonly Random _rnd = new();

    readonly Project[] _projects = new Project[] {
            SimulatedData.Project1,
            SimulatedData.Project2
        };
    readonly Parameter[] _parameters = new Parameter[] {
            SimulatedData.Parameter1,
            SimulatedData.Parameter2,
        };

    Project? _currentProject = null;
    Parameter? _currentParameter = null;
    ScanResult? _latestResult = null;
    object? _comments = null;

    ScopeStatus _scopeStatus = new(-1);
    ScopeResult? _scopeData = null;

    private void Notify(string type, object? obj = null)
    {
        foreach (var socket in _sockets)
        {
            var msg = new
            {
                type,
                time = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds,
                body = obj ?? new object()
            };
            socket.Send(JsonSerializer.Serialize(msg));
        }
    }

    private ScanResult RandomizeScanData(ScanResult data)
    {
        var mdata = data.MeasurementData;
        return data with
        {
            MeasurementData = mdata with
            {
                IntensityTop = mdata.IntensityTop.Select(value => value + (float)(0.5 * _rnd.NextDouble() - 1)).ToArray(),
                IntensityBottom = mdata.IntensityBottom.Select(value => value + (float)(0.5 * _rnd.NextDouble() - 1)).ToArray()
            }
        };
    }

    private ScopeResult RandomizeScopeData(ScopeResult data) => data with
    {
        IntensityTop = data.IntensityTop.Select(value => value + (float)(0.5 * _rnd.NextDouble() - 1)).ToArray(),
        IntensityBottom = data.IntensityBottom.Select(value => value + (float)(0.5 * _rnd.NextDouble() - 1)).ToArray()
    };

    private static void Wait(int ms) => Task.WaitAll(new Task[] { Task.Delay(ms) });
}
