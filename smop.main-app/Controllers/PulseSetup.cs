using Smop.MainApp.Dialogs;
using Smop.MainApp.Pages;
using Smop.MainApp.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Smop.MainApp.Controllers;

public static class PulseChannels
{
    public static int MaxCount => 5;
}

/// <summary>
/// One odor channel properties
/// </summary>
/// <param name="Id">Channel ID, 1..<see cref="PulseChannels.MaxCount"/></param>
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
public class SessionProps(float humidity, PulseIntervals intervals)
{
    /// <summary>
    /// 0..100, or -1 or not set
    /// </summary>
    public float Humidity { get; set; } = humidity;
    public PulseIntervals Intervals { get; set; } = intervals;
    public PulseProps[] Pulses => _pulses.ToArray();

    public void AddPulse(PulseProps pulse)
    {
        _pulses.Add(pulse);
    }

    public void RemovePulse(int index)
    {
        _pulses.RemoveAt(index);
    }

    public void RandomizePulses()
    {
        var r = new Random();
        r.Shuffle(_pulses);
    }

    public int[] GetActiveChannelIds()
    {
        var activeChannelIds = new HashSet<int>();
        foreach (var pulse in _pulses)
            foreach (var channel in pulse.Channels)
                if ((channel.Flow > 0 || channel.Active) && !activeChannelIds.Contains(channel.Id))
                {
                    activeChannelIds.Add(channel.Id);
                }

        return activeChannelIds.ToArray();
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

            foreach (var line in reader.ReadToEnd().Split('\n').Where(p => !string.IsNullOrEmpty(p)).ToArray())
            {
                lineIndex += 1;

                var p = line.ToLower().Trim().Split(':');
                if (p.Length != 2)
                {
                    continue;
                }

                var (key, value) = (p[0], p[1]);

                if (key == SESSION_INIT)
                {
                    var props = value.Trim().Split(' ').Where(p => !string.IsNullOrEmpty(p)).ToArray();
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
                else if (key == SESSION_PULSE)
                {
                    if (lastSession != null)
                    {
                        var props = value.Trim().Split(' ').Where(p => !string.IsNullOrEmpty(p)).ToArray();
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
                        _nlog.Warn(Logging.LogIO.Text("Pulse", $"no session defined yet on line {lineIndex}"));
                    }
                }
                else
                {
                    linesWithInvalidData.Add(lineIndex);
                    _nlog.Warn(Logging.LogIO.Text("Pulse", "unknown command '{key}' on line {lineIndex}"));
                }
            }

            if (linesWithInvalidData.Count > 0)
            {
                var lines = string.Join(", ", linesWithInvalidData);
                MsgBox.Warn(App.Name,
                    $"The setup file was read and parsed, but the following lines were ignored:\n{lines}",
                    new MsgBox.Button[] { MsgBox.Button.OK });
            }
        }
        catch (Exception ex)
        {
            _nlog.Error(ex, "Cannot read or parse the pulse setup file");
            MsgBox.Error(App.Name, $"Cannot read or parse the pulse setup file:\n{ex.Message}");
            return null;
        }

        return new PulseSetup() { Sessions = sessions.ToArray() };
    }

    public void Save(string filename)
    {
        using var writer = new StreamWriter(filename);
        foreach (var session in Sessions)
        {
            writer.WriteLine(string.Join("",
                $"{SESSION_INIT}:",
                session.Humidity >= 0 ? $" {SESSION_HUMIDITY}={session.Humidity}" : "",
                $" {PULSE_INITIAL_PAUSE}={session.Intervals.InitialPause}",
                session.Intervals.DmsDelay >= 0 ? $" {DMS_DELAY}={session.Intervals.DmsDelay}" : "",
                $" {PULSE_DURATION}={session.Intervals.Pulse}",
                $" {PULSE_FINAL_PAUSE}={session.Intervals.FinalPause}"
            ));
            foreach (var pulse in session.Pulses)
            {
                writer.Write($"{SESSION_PULSE}:");
                foreach (var channel in pulse.Channels)
                {
                    if (channel.Active != (channel.Flow != 0))
                    {
                        var isActive = channel.Active ? PULSE_CHANNEL_ON : PULSE_CHANNEL_OFF;
                        writer.Write($" {channel.Id}={channel.Flow},{isActive}");
                    }
                    else
                    {
                        writer.Write($" {channel.Id}={channel.Flow}");
                    }
                }
                writer.WriteLine();
            }
        }
    }

    public void Randomize()
    {
        foreach (var session in Sessions)
        {
            session.RandomizePulses();
        }
    }

    public static IEnumerable<string> PulseChannelsAsStrings(PulseProps pulse) =>
        pulse.Channels.Select(ch => $"{ch.Id}={ch.Flow},{BoolToState(ch.Active)}");

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(PulseSetup));

    static readonly string SESSION_INIT = "init";
    static readonly string SESSION_PULSE = "pulse";
    static readonly string SESSION_HUMIDITY = "humidity";
    static readonly string PULSE_INITIAL_PAUSE = "delay";
    static readonly string PULSE_DURATION = "duration";
    static readonly string PULSE_FINAL_PAUSE = "final";
    static readonly string PULSE_CHANNEL_ON = "on";
    static readonly string PULSE_CHANNEL_OFF = "off";
    static readonly string DMS_DELAY = "dms";

    const float MAX_INTERVAL = 60 * 60; // seconds
    const float MAX_FLOW = 1000; // nccm

    private static string BoolToState(bool value) => value ? PULSE_CHANNEL_ON : PULSE_CHANNEL_OFF;

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
                _nlog.Warn(Logging.LogIO.Text("Pulse", $"invalid field `{field}` on line {lineIndex}"));
                return null;
            }
            var keyvalue = field.Split('=');
            if (keyvalue.Length != 2)
            {
                _nlog.Warn(Logging.LogIO.Text($"Pulse", $"invalid keyvalue `{string.Join('=', keyvalue)}` on line {lineIndex}"));
                return null;
            }

            var (key, value) = (keyvalue[0], keyvalue[1]);
            if (key == SESSION_HUMIDITY)
            {
                humidity = float.Parse(value);
            }
            else if (key == PULSE_INITIAL_PAUSE)
            {
                delay = float.Parse(value);
            }
            else if (key == PULSE_DURATION)
            {
                duration = float.Parse(value);
            }
            else if (key == DMS_DELAY)
            {
                dmsDelay = float.Parse(value);
            }
            else if (key == PULSE_FINAL_PAUSE)
            {
                finalPause = float.Parse(value);
            }
            else
            {
                _nlog.Warn(Logging.LogIO.Text("Pulse", $"unknown field '{field}' on line {lineIndex}"));
                return null;
            }
        }

        return
            humidity > 100 ||
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

            var active = flow != 0;
            if (paramList.Length > 1)
            {
                if (int.TryParse(paramList[1], out int activeValue))
                {
                    active = activeValue > 0;
                }
                else if (paramList[1] == PULSE_CHANNEL_OFF)
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
