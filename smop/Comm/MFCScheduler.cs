using System;
using System.Linq;

namespace SMOP.OdorDisplay
{
    public class MFCScheduler
    {
        public enum ProcedureType
        {
            PreparationFast,
            Preparation,
            CleaningFast,
            Cleaning,
        }

        /// <summary>
        /// Seconds
        /// </summary>
        public double Duration { get; private set; }

        public event EventHandler? Finished;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="procedureType">Type of the procedure, refer to <see cref="ProcedureType"/></param>
        public MFCScheduler(ProcedureType procedureType)
        {
            _timer.AutoReset = false;
            _timer.Elapsed += Timer_Elapsed;

            _actions = procedureType switch
            {
                ProcedureType.PreparationFast => new ActionStep[]
                {
                    new ActionStep(ActionType.CloseValves, 1),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 2),
                    
                    new ActionStepFlowConditional(30, new FlowCondition(PREPERATION_ODOR_SPEED_FAST, 160, FlowConditionOperation.ExceedsUp)),
                    new ActionStep(ActionType.StopFlow, 2),
                    new ActionStepFlowConditional(30, new FlowCondition(PREPERATION_ODOR_SPEED_SLOW, 180, FlowConditionOperation.ExceedsUp)),
                },
                ProcedureType.Preparation => new ActionStep[]
                {
                    new ActionStep(ActionType.CloseValves, 1),
                    new ActionStep(ActionType.OpenUserValve, 30),
                    
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 30),
                    
                    new ActionStep(ActionType.CloseValves, 1),
                    new ActionStep(ActionType.OpenMixerValve, 1),
                    
                    new ActionStepFlow(0.5, 80),
                    new ActionStep(ActionType.StopFlow, 2),
                    new ActionStepFlow(0.5, 80),
                    new ActionStep(ActionType.StopFlow, 2),
                    new ActionStepFlow(0.5, 80),
                    new ActionStep(ActionType.StopFlow, 2),
                    
                    new ActionStepFlowConditional(300, new FlowCondition(PREPERATION_ODOR_SPEED_FAST, 160, FlowConditionOperation.ExceedsUp)),
                    new ActionStep(ActionType.StopFlow, 2),
                    new ActionStepFlowConditional(300, new FlowCondition(PREPERATION_ODOR_SPEED_SLOW, 180, FlowConditionOperation.ExceedsUp)),
                },
                ProcedureType.CleaningFast => new ActionStep[]
                {
                    new ActionStep(ActionType.CloseValves, 1),
                    new ActionStepFlowConditional(100, new FlowCondition(CLEANING_ODOR_SPEED, 65, FlowConditionOperation.ExceedsDown)),
                    
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 10),
                    
                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 10),
                    
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 1),
                    
                    new ActionStep(ActionType.StopFlow, 1),
                },
                ProcedureType.Cleaning => new ActionStep[]
                {
                    new ActionStep(ActionType.CloseValves, 1),
                    new ActionStepFlow(120, CLEANING_ODOR_SPEED),

                    new ActionStep(ActionType.OpenUserValve, 2),    // the flow continues
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 120),

                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 120),

                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 120),

                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenMixerValve, 120),

                    new ActionStep(ActionType.CloseValves, 1),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 2),
                    new ActionStep(ActionType.OpenUserValve, 2),
                    new ActionStep(ActionType.CloseValves, 1),

                    new ActionStep(ActionType.StopFlow, 1),
                },
                _ => throw new NotImplementedException()
            };

            Duration = _actions.Sum(action => action.Duration);
        }

        /// <summary>
        /// Starts the procedure
        /// </summary>
        public void Start()
        {
            _step = 0;

            Run(_actions[_step]);
        }

        public void FeedPID(double value)
        {
            var action = _actions[_step];
            if (action.Type == ActionType.StartFlowConditional)
            {
                var condition = (action as ActionStepFlowConditional)?.Conditions;
                if (condition != null)
                {
                    bool isExceeding = condition.Operation switch
                    {
                        FlowConditionOperation.ExceedsUp => value > condition.PIDThreshold,
                        FlowConditionOperation.ExceedsDown => value < condition.PIDThreshold,
                        _ => false
                    };

                    if (isExceeding)
                    {
                        _timer.Stop();
                        Timer_Elapsed(null, null);
                    }
                }
            }
        }

        // Internal

        enum ActionType
        {
            OpenUserValve,
            OpenMixerValve,
            CloseValves,
            StartFlow,
            StartFlowConditional,
            StopFlow,
        }

        enum FlowConditionOperation
        {
            ExceedsUp,
            ExceedsDown,
        }

        class FlowCondition
        {
            public double FlowSpeed { get; set; }
            public double PIDThreshold { get; set; }
            public FlowConditionOperation Operation { get; set; }
            public FlowCondition(double flowSpeed, double pidThreshold, FlowConditionOperation operation)
            {
                FlowSpeed = flowSpeed;
                PIDThreshold = pidThreshold;
                Operation = operation;
            }
        }

        class ActionStep
        {
            public ActionType Type { get; set; }
            public double Duration { get; set; }
            public ActionStep(ActionType type, double duration)
            {
                Type = type;
                Duration = duration;
            }
        }

        class ActionStepFlow : ActionStep
        {
            public double FlowSpeed { get; set; }
            public ActionStepFlow(double duration, double flowSpeed) : base(ActionType.StartFlow, duration)
            {
                FlowSpeed = flowSpeed;
            }
        }

        class ActionStepFlowConditional : ActionStep
        {
            public FlowCondition Conditions { get; set; }
            public ActionStepFlowConditional(double duration, FlowCondition conditions) : base(ActionType.StartFlowConditional, duration)
            {
                Conditions = conditions;
            }
        }

        const double PREPERATION_ODOR_SPEED_FAST = 50;   // ml/min
        const double PREPERATION_ODOR_SPEED_SLOW = 10;   // ml/min
        const double CLEANING_ODOR_SPEED = 200;          // ml/min

        readonly System.Timers.Timer _timer = new();
        readonly MFC _mfc = MFC.Instance;
        readonly ActionStep[] _actions;

        int _step = 0;

        private void Run(ActionStep action)
        {
            switch (action.Type)
            {
                case ActionType.OpenUserValve:
                    _mfc.OdorDirection = MFC.OdorFlowsTo.WasteAndUser;
                    break;
                case ActionType.OpenMixerValve:
                    _mfc.OdorDirection = MFC.OdorFlowsTo.SystemAndWaste;
                    break;
                case ActionType.CloseValves:
                    _mfc.OdorDirection = MFC.OdorFlowsTo.Waste;
                    break;
                case ActionType.StartFlow:
                    {
                        double flowSpeed = (action as ActionStepFlow)?.FlowSpeed ?? 0;
                        _mfc.OdorSpeed = flowSpeed;
                    }
                    break;
                case ActionType.StartFlowConditional:
                    {
                        double flowSpeed = (action as ActionStepFlowConditional)?.Conditions.FlowSpeed ?? 0;
                        _mfc.OdorSpeed = flowSpeed;
                    }
                    break;
                case ActionType.StopFlow:
                    _mfc.OdorSpeed = 0;
                    break;
            }

            System.Diagnostics.Debug.WriteLine($"[SCHD] {action.Type}");

            _timer.Interval = action.Duration * 1000;
            _timer.Start();
        }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs? e)
        {
            if (++_step < _actions.Length)
            {
                Run(_actions[_step]);
            }
            else
            {
                Finished?.Invoke(this, new EventArgs());
            }
        }
    }
}
