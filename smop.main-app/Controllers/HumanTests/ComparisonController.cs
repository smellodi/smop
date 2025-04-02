using Smop.Common;

namespace Smop.MainApp.Controllers.HumanTests;

internal class ComparisonController : CommonController
{
    public int BlockID => _blockIndex + 1;
    public int ComparisonID => _comparisonIndex + 1;

    public ComparisonController(Settings settings) : base("comparison", settings)
    {
        _session = new ComparisonSession(settings);
    }

    public override void Start()
    {
        base.Start();
        RunNextBlock();
    }

    public void Continue()
    {
        if (IsPaused)
        {
            _delayedAction?.Dispose();
            _delayedAction = null;

            RunNextBlock();
        }
    }

    public void ForceToFinish()
    {
        if (_session.Blocks.Length > 0)
        {
            _blockIndex = _session.Blocks.Length - 1;
            _comparisonIndex = _session.Blocks[_blockIndex].Comparisons.Length - 1;
        }
    }

    public void SetAnswer(bool areSame)
    {
        var block = _session.Blocks[_blockIndex];
        var comparison = block.Comparisons[_comparisonIndex];

        comparison.AreSame = areSame;

        _eventLogger.Add("answer", areSame ? "same" : "different");

        if (PauseBetweenTrials > 0)
        {
            PublishStage(Stage.TimedPause);
            _delayedAction = DispatchOnce.Do(PauseBetweenTrials, RunNextComparison);
        }
        else
        {
            RunNextComparison();
        }
    }

    // Internal

    //static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(HumanTestsComparisonController));

    readonly ComparisonSession _session;

    int _blockIndex = -1;
    int _comparisonIndex = -1;

    private void RunNextBlock()
    {
        _blockIndex += 1;

        if (_blockIndex < _session.Blocks.Length)
        {
            var block = _session.Blocks[_blockIndex];
            _eventLogger.Add("block", _blockIndex.ToString());

            _comparisonIndex = -1;
            _delayedAction = DispatchOnce.Do(0.1, RunNextComparison);
        }
        else
        {
            foreach (var block in _session.Blocks)
                foreach (var comp in block.Comparisons)
                    _eventLogger.Add(Name, comp.ToString());

            PublishStage(Stage.Finished);
        }
    }

    private void RunNextComparison()
    {
        var block = _session.Blocks[_blockIndex];

        _comparisonIndex += 1;
        _mixtureIndex = 0;

        if (_comparisonIndex < block.Comparisons.Length)
        {
            var comparison = block.Comparisons[_comparisonIndex];
            _eventLogger.Add("pair", _comparisonIndex.ToString());

            StartPulse(comparison.Mixtures[_mixtureIndex]);
        }
        else if (PauseBetweenBlocks > 0 && _blockIndex < _session.Blocks.Length - 1)
        {
            PublishStage(Stage.UserControlledPause);
            _delayedAction = DispatchOnce.Do(PauseBetweenBlocks, RunNextBlock);
        }
        else
        {
            _delayedAction = DispatchOnce.Do(0.1, RunNextBlock);
        }
    }

    protected override Mixture? GetNextMixture()
    {
        var block = _session.Blocks[_blockIndex];
        var comparison = block.Comparisons[_comparisonIndex];

        _mixtureIndex += 1;

        return _mixtureIndex < comparison.Mixtures.Length
            ? comparison.Mixtures[_mixtureIndex]
            : null;
    }

    protected override Stage GetStageAfterMixture() => Stage.Question;
}