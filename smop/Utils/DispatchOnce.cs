using System;
using System.Collections.Generic;
//using System.Windows.Threading;

namespace Smop.Utils
{
    /*
    public class DispatchOnceUI : DispatcherTimer
    {
        public DispatchOnceUI(double seconds, Action action, bool start = true) : base()
        {
            _actions.Enqueue(new ScheduledAction() { Pause = seconds, Action = action });

            Interval = TimeSpan.FromSeconds(seconds);
            Tick += (s, e) => Execute();

            if (start)
            {
                Start();
            }
        }

        public static DispatchOnceUI Do(double seconds, Action action)
        {
            if (seconds > 0)
            {
                return new DispatchOnceUI(seconds, action);
            }
            else
            {
                action();
                return null;
            }
        }

        public DispatchOnceUI Then(double seconds, Action action)
        {
            _actions.Enqueue(new ScheduledAction() { Pause = seconds, Action = action });
            return this;
        }


        // Internal

        struct ScheduledAction
        {
            public double Pause;
            public Action Action;
        }

        readonly Queue<ScheduledAction> _actions = new();

        private void Execute()
        {
            Stop();

            var action = _actions.Dequeue();
            action.Action();

            if (_actions.Count > 0)
            {
                var next = _actions.Peek();
                Interval = TimeSpan.FromSeconds(next.Pause);
                Start();
            }
        }
    }
    */
    public class DispatchOnce : System.Timers.Timer
    {
        public DispatchOnce(double seconds, Action action, bool start = true) : base()
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
}
