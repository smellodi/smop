using Smop.PulseGen.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Smop.PulseGen.Test;

public record class PulseChannelProps(int Id, double Flow, bool Active);
public record class PulseProps(PulseChannelProps[] Channels);
public record class PulseIntervals(float Delay, float Duration, float DmsDelay, float FinalPause);

public class SessionProps
{
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

    // Internal

    List<PulseProps> _pulses = new();
}

public class PulseSetup
{
    public SessionProps[] Sessions { get; init; } = Array.Empty<SessionProps>();

    public static PulseSetup? Load(string filename)
    {
        var sessions = new List<SessionProps>();
        SessionProps? lastSession = null;

        try
        {
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

                if (p[0] == "init")
                {
                    p = p[1].Trim().Split(' ');
                    var sessionProps = CreateSessionProps(p, lastSession, lineIndex);
                    lastSession = sessionProps;

                    sessions.Add(sessionProps);
                }
                else if (p[0] == "pulse")
                {
                    if (lastSession != null)
                    {
                        p = p[1].Trim().Split(' ');
                        var pulseProps = CreatePulseProps(p);
                        lastSession.AddPulse(pulseProps);
                    }
                    else
                    {
                        Debug.WriteLine($"[PST] no session defined yet on line {lineIndex}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[PST] unknown command '{p[0]}' on line {lineIndex}");
                }
            }
        }
        catch (Exception e)
        {
            MsgBox.Error(App.Current.MainWindow.Title, $"Cannot read or parse the pulse setup file:\n{e.Message}");
        }

        return new PulseSetup() { Sessions = sessions.ToArray() };
    }

    public void Randomize()
    {
        var r = new Random();
        foreach (var session in Sessions)
        {
            r.Shuffle(session.Pulses);
        }
    }

    // Internal

    private static SessionProps CreateSessionProps(string[] p, SessionProps? lastSessionProps, int lineIndex)
    {
        float humidity = lastSessionProps?.Humidity ?? -1;
        float delay = lastSessionProps?.Intervals.Delay ?? 0;
        float duration = lastSessionProps?.Intervals.Duration ?? 1000;
        float dmsDelay = lastSessionProps?.Intervals.DmsDelay ?? 0;
        float finalPause = lastSessionProps?.Intervals.FinalPause ?? 0;

        foreach (var field in p)
        {
            if (field.Length < 3)
            {
                continue;
            }
            var keyvalue = field.Split('=');
            if (keyvalue.Length != 2)
            {
                continue;
            }

            var key = keyvalue[0];
            if (key == "humidity")
            {
                humidity = float.Parse(keyvalue[1]);
            }
            else if (key == "delay")
            {
                delay = float.Parse(keyvalue[1]);
            }
            else if (key == "duration")
            {
                duration = float.Parse(keyvalue[1]);
            }
            else if (key == "dms")
            {
                dmsDelay = float.Parse(keyvalue[1]);
            }
            else if (key == "final")
            {
                finalPause = float.Parse(keyvalue[1]);
            }
            else
            {
                Debug.WriteLine($"[PST] unknown field '{field}' on line {lineIndex}");
            }
        }

        return new SessionProps(humidity, new PulseIntervals(delay, duration, dmsDelay, finalPause));
    }

    private static PulseProps CreatePulseProps(string[] p)
    {
        var channels = new List<PulseChannelProps>();
        foreach (var field in p)
        {
            if (field.Length < 5)
            {
                continue;
            }
            var keyvalue = field.Split('=');
            if (keyvalue.Length != 2)
            {
                continue;
            }

            var pcp = keyvalue[1].Split(',');
            if (pcp.Length != 2)
            {
                continue;
            }

            var id = int.Parse(keyvalue[0]);
            var flow = float.Parse(pcp[0]);
            var active = int.Parse(pcp[1]) != 0;

            var pulseChannelProps = new PulseChannelProps(id, flow, active);
            channels.Add(pulseChannelProps);
        }

        return new PulseProps(channels.ToArray());
    }
}
