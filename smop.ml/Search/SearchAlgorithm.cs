using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatrixD = Smop.ML.Search.Matrix<double>;

namespace Smop.ML.Search;

internal class SearchAlgorithm
{
    public SearchAlgorithm(Config config)
    {
        _config = config;

        _channelIDs = config.Printer.Channels.Select(c => c.Id).ToArray();
        _channelNames = config.Printer.Channels.Select(c => c.Odor).ToArray();
        _channelRanges = config.Printer.Channels.Select(c => {
            var min = c.Props.TryGetValue("minFlow", out object? min_) ? min_ : null;
            var max = c.Props.TryGetValue("maxFlow", out object? max_) ? max_ : null;
            return new MinMax(
                double.Parse(min?.ToString() ?? "0"),
                double.Parse(max?.ToString() ?? "100")
            );
        }).ToArray();

        _hasDmsSource = config.Sources.Contains(Source.DMS);

        var diffEvolParameters = new DiffEvolParameters()
        {
            DistanceThreshold = _config.Threshold,
            MaxIterations = _config.MaxIterations
        };

        if (Enum.TryParse(_config.Algorithm, out Kernel kernel))
        {
            diffEvolParameters.Kernel = kernel;
        }

        _processor = new DiffEvol(diffEvolParameters, _channelIDs.Length);
        _processor.RequestFormat += Processor_RequestFormat;
    }

    public bool AddMeasurement(Content content)
    {
        if (!_hasDmsSource && content.Source != Source.SNT)
            return false;

        var measurement = GetMeasurementData(content);
        if (measurement.Length == 0)
            return false;

        // the first measurement is always the target measurement
        if (!_processor.HasTarget)
        {
            _processor.SetTarget(measurement);
        }
        else
        {
            _processor.AddMeasurement(measurement);
        }

        return true;
    }

    public async Task<Recipe> GetRecipe()
    {
        await Task.Delay(100);      // simply, to make ML being not too fast in SMOP interface

        TestCandidate testCandidate = _processor.GetTestCandidate();
        
        return ToRecipe(testCandidate);
    }

    // Internal

    record class MinMax(double Min, double Max);

    const int FLOW_DURATION_ENDLESS = -1;

    readonly Config _config;

    readonly int[] _channelIDs;
    readonly string[] _channelNames;
    readonly MinMax[] _channelRanges;

    readonly bool _hasDmsSource;

    readonly DiffEvol _processor;

    private static double[] GetMeasurementData(Content content) => content switch
        {
            DmsMeasurement dms => dms.Data.Positive.Select(f => (double)f).ToArray(),
            SntMeasurement snt => snt.Data.Features.Select(f => (double)f).ToArray(),
            PIDMeasurement pid => [],   // ignore PID
            _ => throw new ArgumentException("Invalid content type")
        };

    private (double[], double[]) GetFlowTransformations()
    {
        double[] offsets = _config.Printer.Channels.Select(_ => 0d).ToArray();
        double[] gains = _config.Printer.Channels.Select(_ => 1d).ToArray();

        int i = 0;
        foreach (var channel in _config.Printer.Channels)
        {
            var props = channel.Props;
            offsets[i] = _channelRanges[i].Min;
            gains[i] = (_channelRanges[i].Max - _channelRanges[i].Min) / DiffEvol.SearchRange;
            i++;
        }

        return (offsets, gains);
    }

    //  Formats variable names with their values as "VAR1=VALUE1 VAR2=VALUE2 ..."
    private static string FormatRecipe(string[] varNames, MatrixD vector)
    {
        if (varNames.Length != vector.Size)
            return string.Empty;

        List<string> result = new();

        for (int i = 0; i < vector.Size; i++)
        {
            result.Add($"{varNames[i]} = {vector[i],-4:F1}");
        }

        return string.Join(" ", result);
    }

    // Formats variable names with a list of values as
    //   VAR1 VALUE1 VALUE2..
    //   VAR2 VALUE1 VALUE2..
    //   ..
    private static string FormatRecipesAll(string[] varNames, MatrixD vectors)
    {
        if (varNames.Length != vectors.RowCount)
            return string.Empty;

        StringBuilder result = new();
        for (int r = 0; r < vectors.RowCount; r++)
        {
            result.Append($"{varNames[r],-12}");
            for (int c = 0; c < vectors.ColumnCount; c++)
                result.Append($" {vectors[r, c],4:F1}");
            result.AppendLine();
        }
        return result.ToString();
    }

    private Recipe ToRecipe(TestCandidate candidate)
    {
        var (offsets, gains) = GetFlowTransformations();

        var channels = new List<ChannelRecipe>();

        int i = 0;
        foreach (var flow in candidate.Vector)
        {
            channels.Add(new ChannelRecipe(
                _channelIDs[i],
                (float)(offsets[i] + flow * gains[i]),
                FLOW_DURATION_ENDLESS
            ));
            i++;
        }

        return new Recipe(candidate.Name, candidate.IsFinal, (float)candidate.Distance, channels.ToArray());
    }

    private void Processor_RequestFormat(object? sender, DiffEvol.RequestFormatEventArgs e)
    {
        if (e.Matrix.ColumnCount == 1)
            e.Result = FormatRecipe(_channelNames, e.Matrix);
        else
            e.Result = FormatRecipesAll(_channelNames, e.Matrix);
    }
}
