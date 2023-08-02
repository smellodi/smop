using System;
using System.Collections.Generic;

namespace Smop.PulseGen.Utils;

/// <summary>
/// Use <see cref="Do(double, Action)"/> static method of this class to execute a delayed action
/// </summary>
public class DispatchOnce : System.Timers.Timer
{
	public static DispatchOnce? Do(double seconds, Action action)
	{
		if (seconds > 0)
		{
			return new DispatchOnce(seconds, action);
		}
		else
		{
			action();
			return null;
		}
	}

	public DispatchOnce Then(double seconds, Action action)
	{
		_actions.Enqueue(new ScheduledAction() { Pause = (int)(1000 * seconds), Action = action });
		return this;
	}


	// Internal

	struct ScheduledAction
	{
		public int Pause;
		public Action Action;
	}

	readonly Queue<ScheduledAction> _actions = new();

    private DispatchOnce(double seconds, Action action, bool start = true) : base()
    {
        var pause = (int)(1000 * seconds);
        _actions.Enqueue(new ScheduledAction() { Pause = pause, Action = action });

        Interval = pause;
        AutoReset = false;

        Elapsed += (s, e) => Execute();

        if (start)
        {
            Start();
        }
    }

    private void Execute()
	{
		Stop();

		var action = _actions.Dequeue();
		action.Action();

		if (_actions.Count > 0)
		{
			var next = _actions.Peek();
			Interval = next.Pause;
			Start();
		}
	}
}
