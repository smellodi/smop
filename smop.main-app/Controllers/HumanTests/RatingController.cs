﻿using System.Collections.Generic;

namespace Smop.MainApp.Controllers.HumanTests
{
    internal class RatingController : CommonController
    {
        public RatingController(Settings settings) : base("ratings", settings)
        {
            _mixtures = _settings.IsPracticingProcedure
                ? [new Mixture("empty", [], _settings.Channels)]
                : OdorDisplayHelper.GetAllMixtures(_settings.Channels);
        }

        public override void Start()
        {
            base.Start();

            _mixtureIndex = 0;
            StartPulse(_mixtures[_mixtureIndex]);
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
                    _eventLogger.Add(Name, name, string.Join(" ", r));

                _ratings.Clear();
                return false;
            }
        }

        public void ReleaseOdor()
        {
            base.OpenParticipantValve();
        }

        public void ForceToFinish()
        {
            _mixtureIndex = _mixtures.Length - 1;
        }

        public void SetAnswers(string[] ratings)
        {
            _ratings.Add(_mixtures[_mixtureIndex].Name, ratings);

            _eventLogger.Add("answer", string.Join(" ", ratings));
        }

        // Internal

        //static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(HumanTestsComparisonController));

        readonly Mixture[] _mixtures;
        readonly Dictionary<string, string[]> _ratings = new();


        protected override void OpenParticipantValve()
        {
            PublishStage(Stage.Ready);
        }

        protected override Mixture? GetNextMixture() => null;

        protected override Stage GetStageAfterMixture() => Stage.Ready;
    }
}