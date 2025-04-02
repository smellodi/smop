using Smop.Common;

namespace Smop.MainApp.Controllers.HumanTests;

internal class OneOutController : CommonController
{
    public int TripletID => _tripletIndex + 1;

    public OneOutController(Settings settings) : base("one-out", settings)
    {
        _session = new OneOutSession(settings);
    }

    public override void Start()
    {
        base.Start();
        RunNextTriplet();
    }

    public void Continue()
    {
        if (IsPaused)
        {
            _delayedAction?.Dispose();
            _delayedAction = null;

            RunNextTriplet();
        }
    }

    public void ForceToFinish()
    {
        if (_session.Triplets.Length > 0)
        {
            _tripletIndex = _session.Triplets.Length - 1;
        }
    }

    public void SetAnswer(string answer)
    {
        var tripet = _session.Triplets[_tripletIndex];

        tripet.Answer = int.TryParse(answer, out int id) ? id : 0;
        System.Diagnostics.Debug.WriteLine(tripet);

        _eventLogger.Add("answer", answer);

        if (_settings.PauseBetweenTrials > 0)
        {
            PublishStage(Stage.TimedPause);
            _delayedAction = DispatchOnce.Do(_settings.PauseBetweenTrials, RunNextTriplet);
        }
        else
        {
            RunNextTriplet();
        }
    }

    // Internal

    readonly int BlockSize = 4;

    readonly OneOutSession _session;

    int _tripletIndex = -1;

    private void RunNextTriplet()
    {
        if (!IsPaused)
        {
            _tripletIndex += 1;

            if (_tripletIndex > 0 && _tripletIndex < _session.Triplets.Length && (_tripletIndex % BlockSize) == 0)
            {
                PublishStage(Stage.UserControlledPause);
                _delayedAction = DispatchOnce.Do(_settings.PauseBetweenBlocks, RunNextTriplet);
                return;
            }
        }

        if (_tripletIndex < _session.Triplets.Length)
        {
            var triplet = _session.Triplets[_tripletIndex];
            _eventLogger.Add("triplet", _tripletIndex.ToString());

            _mixtureIndex = 0;
            StartPulse(triplet.Mixtures[_mixtureIndex]);
        }
        else
        {
            foreach (var triplet in _session.Triplets)
                _eventLogger.Add(Name, triplet.ToString());

            PublishStage(Stage.Finished);
        }
    }

    protected override Mixture? GetNextMixture()
    {
        var triplet = _session.Triplets[_tripletIndex];

        _mixtureIndex += 1;

        return _mixtureIndex < triplet.Mixtures.Length
            ? triplet.Mixtures[_mixtureIndex]
            : null;
    }

    protected override Stage GetStageAfterMixture() => Stage.Question;
}