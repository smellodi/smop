using Smop.Common;
using Smop.MainApp.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Smop.MainApp.Controllers.HumanTests
{
    internal class RatingController(Settings settings) : IDisposable
    {
        public class StageChangedEventArgs(Stage stage, string mixtureName) : EventArgs
        {
            public Stage Stage { get; } = stage;
            public string MixtureName { get; } = mixtureName;
        }

        public int MixtureId => _mixtureIndex + 1;

        public event EventHandler<StageChangedEventArgs>? StageChanged;

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _delayedAction?.Stop();
            _delayedAction?.Dispose();
            _delayedAction = null;

            GC.SuppressFinalize(this);
        }

        public void Start()
        {
            _odorDisplay.OpenValves(Session.GetChannelIds(settings.Channels));

            _eventLogger.Add("start", "rating");
            _mixtureIndex = 0;

            StartPulse(_mixtures[_mixtureIndex]);
        }

        public void ReleaseOdor()
        {
            OpenParticipantValve();
        }

        public void ForceToFinish()
        {
            _mixtureIndex = _mixtures.Length - 1;
        }

        public void Stop()
        {
            var channelsIDs = Session.GetChannelIds(settings.Channels);
            _odorDisplay.StopFlows(channelsIDs.Select(id => (OdorDisplay.Device.ID)id).ToArray());
        }

        public void SetAnswers(string[] ratings)
        {
            _ratings.Add(_mixtures[_mixtureIndex].Name, ratings);

            _eventLogger.Add("answer", string.Join(" ", ratings));
        }

        public bool Continue()
        {
            _mixtureIndex += 1;

            if (_mixtureIndex < _mixtures.Length)
            {
                StartPulse(_mixtures[_mixtureIndex]);
                return true;
            }
            else
            {
                foreach (var (name, r) in _ratings)
                {
                    _eventLogger.Add("rating", name, string.Join(" ", r));
                }

                _ratings.Clear();
                return false;
            }
        }

        // Internal

        //static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(HumanTestsComparisonController));

        readonly OdorDisplayController _odorDisplay = new();
        readonly EventLogger _eventLogger = EventLogger.Instance;

        readonly Mixture[] _mixtures = Session.GetAllMixtures(settings.Channels);
        readonly Dictionary<string, string[]> _ratings = new();

        int _mixtureIndex = -1;
        Stage _currentStage = Stage.Initial;
        DispatchOnce? _delayedAction = null;
        bool _isDisposed = false;

        private void StartPulse(Mixture mixture)
        {
            _eventLogger.Add("mixture", _mixtureIndex.ToString(), mixture.Name, mixture.ToString());

            _odorDisplay.SetFlows(mixture.Channels);

            PublishStage(Stage.WaitingMixture);

            _delayedAction = DispatchOnce.Do(Mixture.WaitingInterval, () => PublishStage(Stage.Ready));
        }

        private void OpenParticipantValve()
        {
            _odorDisplay.SetExternalValveState(true);

            _eventLogger.Add("valve", "open");

            PublishStage(Stage.SniffingMixture);

            _delayedAction = DispatchOnce.Do(Mixture.SniffingInterval, CloseParticipantValve);
        }

        private void CloseParticipantValve()
        {
            _odorDisplay.SetExternalValveState(false);

            _eventLogger.Add("valve", "close");

            PublishStage(Stage.Ready);
        }

        private void PublishStage(Stage stage)
        {
            _currentStage = stage;
            StageChanged?.Invoke(this, new StageChangedEventArgs(_currentStage, 
                _mixtureIndex >= 0 && _mixtureIndex < _mixtures.Length ? _mixtures[_mixtureIndex].Name : "" ));
        }
    }
}