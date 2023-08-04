using Smop.PulseGen.Utils;
using Smop.PulseGen.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Smop.PulseGen.Generator;

/// <summary>
/// One odor channel properties
/// </summary>
/// <param name="Id">Channel ID, 1..9</param>
/// <param name="Flow">Flow in ccm</param>
/// <param name="Active">Set to true for the channel to be opened during the pulse</param>
public record class PulseChannelProps(int Id, float Flow, bool Active);

/// <summary>
/// Pulse parameters
/// </summary>
/// <param name="Channels">List of pulse channels</param>
public record class PulseProps(PulseChannelProps[] Channels);

/// <summary>
/// Pulse intervals
/// </summary>
/// <param name="InitialPause">the interval between the flows are set and the valves are opened</param>
/// <param name="Pulse">Pulse duration from the valves are open till the valves are closed</param>
/// <param name="DmsDelay">Interval between valves are opened and the DMS measurement starts</param>
/// <param name="FinalPause">Internal between the valves are close and the next pulse starts</param>
public record class PulseIntervals(float InitialPause, float Pulse, float DmsDelay, float FinalPause)
{
    public bool HasDms => DmsDelay >= 0;
}


/// <summary>
/// Session properties: humidity, intervals, list of pulses
/// </summary>
public class SessionProps
{
    /// <summary>
    /// 0..1
    /// </summary>
    public float Humidity { get; init; }
    public PulseIntervals Intervals { get; init; }
    public PulseProps[] Pulses => _pulses.ToArray();

    public SessionProps(float humidity, PulseIntervals intervals)
    {
        Humidity = humidity;
        Intervals = intervals;
    }

    public void AddPulse(PulseProps pulse)
    {
        _pulses.Add(pulse);
    }

    public void RandomizePulses()
    {
        var r = new Random();
        r.Shuffle(_pulses);
    }

    // Internal

    readonly List<PulseProps> _pulses = new();
}

/// <summary>
/// Pulse generation setup (list of sessions)
/// </summary>
public class PulseSetup
{
    public SessionProps[] Sessions { get; init; } = Array.Empty<SessionProps>();

    public static PulseSetup? Load(string filename)
    {
        var sessions = new List<SessionProps>();
        SessionProps? lastSession = null;

        try
        {
            var linesWithInvalidData = new List<int>();

            using var reader = new StreamReader(filename);
            int lineIndex = 0;

            foreach (var line in reader.ReadToEnd().Split('\n'))
            {
                lineIndex += 1;

                var p = line.ToLower().Trim().Split(':');
                if (p.Length != 2)
                {
                    continue;
                }

                var (key, value) = (p[0], p[1]);

                if (key == "init")
                {
                    var props = value.Trim().Split(' ');
                    var sessionProps = CreateSessionProps(props, lastSession, lineIndex);
                    lastSession = sessionProps;

                    if (sessionProps != null)
                    {
                        sessions.Add(sessionProps);
                    }
                    else
                    {
                        linesWithInvalidData.Add(lineIndex);
                    }
                }
                else if (key == "pulse")
                {
                    if (lastSession != null)
                    {
                        var props = value.Trim().Split(' ');
                        var (pulseProps, hasInvalidValues) = CreatePulseProps(props);

                        if (pulseProps != null)
                        {
                            lastSession.AddPulse(pulseProps);
                        }

                        if (pulseProps == null || hasInvalidValues)
                        {
                            linesWithInvalidData.Add(lineIndex);
                        }
                    }
                    else
                    {
                        linesWithInvalidData.Add(lineIndex);
                        _nlog.Warn("no session defined yet on line {LineIndex}", lineIndex);
                    }
                }
                else
                {
                    linesWithInvalidData.Add(lineIndex);
                    _nlog.Warn($"unknown command '{key}' on line {lineIndex}");
                }
            }

            if (linesWithInvalidData.Count > 0)
            {
                var lines = string.Join(", ", linesWithInvalidData);
                MsgBox.Warn(System.Windows.Application.Current.MainWindow.Title,
                    $"The setup file was read and parsed, but the following lines were ignored:\n{lines}",
                    new MsgBox.Button[] { MsgBox.Button.OK });
            }
        }
        catch (Exception ex)
        {
            _nlog.Error(ex, "Cannot read or parse the pulse setup file");
            MsgBox.Error(System.Windows.Application.Current.MainWindow.Title, $"Cannot read or parse the pulse setup file:\n{ex.Message}");
        }

        return new PulseSetup() { Sessions = sessions.ToArray() };
    }

    public void Randomize()
    {
        foreach (var session in Sessions)
        {
            session.RandomizePulses();
        }
    }

    public static IEnumerable<string> PulseChannelsAsStrings(PulseProps pulse) =>
        pulse.Channels.Select(ch => $"{ch.Id}:{ch.Flow}/{BoolToState(ch.Active)}");

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(PulseSetup));

    const float MAX_INTERVAL = 60 * 60; // seconds
    const float MAX_FLOW = 1000; // nccm

    private static string BoolToState(bool value) => value ? "ON" : "OFF";

    private static SessionProps? CreateSessionProps(string[] p, SessionProps? lastSessionProps, int lineIndex)
    {
        float humidity = lastSessionProps?.Humidity ?? -1;
        float delay = lastSessionProps?.Intervals.InitialPause ?? 0;
        float duration = lastSessionProps?.Intervals.Pulse ?? 1000;
        float dmsDelay = lastSessionProps?.Intervals.DmsDelay ?? -1;
        float finalPause = lastSessionProps?.Intervals.FinalPause ?? 0;

        foreach (var field in p)
        {
            if (field.Length < 3)
            {
                _nlog.Warn($"invalid field `{field}` on line {lineIndex}");
                return null;
            }
            var keyvalue = field.Split('=');
            if (keyvalue.Length != 2)
            {
                _nlog.Warn($"invalid keyvalue `{string.Join('=', keyvalue)}` on line {lineIndex}");
                return null;
            }

            var (key, value) = (keyvalue[0], keyvalue[1]);
            if (key == "humidity")
            {
                humidity = float.Parse(value);
            }
            else if (key == "delay")
            {
                delay = float.Parse(value);
            }
            else if (key == "duration")
            {
                duration = float.Parse(value);
            }
            else if (key == "dms")
            {
                dmsDelay = float.Parse(value);
            }
            else if (key == "final")
            {
                finalPause = float.Parse(value);
            }
            else
            {
                _nlog.Warn($"unknown field '{field}' on line {lineIndex}");
                return null;
            }
        }

        return
            humidity < 0 || humidity > MAX_INTERVAL ||
            delay < 0 || delay > MAX_INTERVAL ||
            duration <= 0 || duration > MAX_INTERVAL ||
            dmsDelay > MAX_INTERVAL ||
            finalPause < 0 || finalPause > MAX_INTERVAL ||
            dmsDelay > duration
                ? null
                : new SessionProps(humidity, new PulseIntervals(delay, duration, dmsDelay, finalPause));
    }

    private static (PulseProps?, bool) CreatePulseProps(string[] fields)
    {
        bool hasInvalidValues = false;
        var channels = new List<PulseChannelProps>();
        foreach (var field in fields)
        {
            if (field.Length < 3)
            {
                return (null, true);
            }
            var keyvalue = field.Split('=');
            if (keyvalue.Length != 2)
            {
                return (null, true);
            }

            var paramList = keyvalue[1].Split(',');
            if (paramList.Length > 2)
            {
                return (null, true);
            }

            var id = int.Parse(keyvalue[0]);
            var flow = float.Parse(paramList[0]);

            var active = true;
            if (paramList.Length > 1)
            {
                if (int.TryParse(paramList[1], out int activeValue))
                {
                    active = activeValue > 0;
                }
                else if (paramList[1] == "off")
                {
                    active = false;
                }
            }

            if (id > 0 && id < 10 && flow >= 0 && flow <= MAX_FLOW)
            {
                var pulseChannelProps = new PulseChannelProps(id, flow, active);
                channels.Add(pulseChannelProps);
            }
            else
            {
                hasInvalidValues = true;
            }
        }

        return (channels.Count > 0 ? new PulseProps(channels.ToArray()) : null, hasInvalidValues);
    }
}
