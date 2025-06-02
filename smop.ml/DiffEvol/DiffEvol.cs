using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatrixD = Smop.ML.DiffEvol.Matrix<double>;

namespace Smop.ML.DiffEvol;

internal static class DebugDisplay
{
    public static void WriteLine(string? msg = null)
    {
        if (msg != null)
        {
            System.Diagnostics.Debug.WriteLine("[ML.DE] " + msg);
            Console.Out.WriteLine(msg);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(string.Empty);
            Console.Out.WriteLine();
        }
    }

    public static void Write(string msg)
    {
        System.Diagnostics.Debug.Write("[ML.DE] " + msg);
        Console.Out.Write(msg);
    }
    /*
    static DebugDisplay()
    {
        AllocConsole();
    }

    [DllImport("Kernel32")]
    static extern void AllocConsole();

    [DllImport("Kernel32", SetLastError = true)]
    static extern void FreeConsole();
    */
}

internal class DiffEvol
{
    public DiffEvol(Config config)
    {
        _config = config;
        _parameters = new();

        if (Enum.TryParse(_config.Algorithm, out Kernel kernel))
        {
            _parameters.Kernel = kernel;
        }

        _channelIDs = config.Printer.Channels.Select(c => c.Id).ToArray();
        _channelNames = config.Printer.Channels.Select(c => c.Odor).ToArray();
        _channelRanges = config.Printer.Channels.Select(c => new MinMax(
            double.Parse(c.Props["minFlow"].ToString() ?? "0"),
            double.Parse(c.Props["maxFlow"].ToString() ?? "100")
        )).ToArray();

        _hasDmsSource = config.Sources.Contains(Source.DMS);

        // Population size: set of best vectors
        _candidates = CreateInitialVectors(_channelIDs.Length, FLOW_MIN, FLOW_MAX);
        Round(_candidates, _parameters.Decimals);

        _iterationCandidates = _candidates.Copy();

        // All measured vectors (flow rates)
        _allCandidates = new MatrixD(_candidates.RowCount, 0);

        _measrIndices = Enumerable.Range(0, _candidates.ColumnCount).ToArray();    // range of X and U

        _flowVariationDelta = 0.2 * (FLOW_MAX - FLOW_MIN);
    }

    public async Task<Recipe?> AddMeasurement(Content content)
    {
        if (!_hasDmsSource && content.Source != Source.SNT)
            return null;

        var measurement = GetMeasurementData(content);
        if (measurement.Length == 0)
            return null;

        // the first measurement is always the target measurement
        if (_target.Length == 0)
        {
            if (content is DmsMeasurement dms_)
            {
                _ucv = dms_.Setup.Ucv.Steps == 1 ? dms_.Setup.Ucv.Min : 0;
            }

            _target = measurement;

            DebugDisplay.WriteLine("\nCollecting initial measurements");
        }
        else 
        {
            // Handle the arrived measurement: push it to the stack of measurements, add the  calculate distance and 

            var measurementId = _allMeasurements.Count;
            _allMeasurements.Add(measurement);

            int candId = measurementId % _measrIndices.Length;
            _allCandidates = _allCandidates.StackColumns(_iterationCandidates.Column(candId));

            var previousDistance = GetDistance(_parameters.Kernel, _target, _allMeasurements[_measrIndices[candId]]);
            _lastDistance = GetDistance(_parameters.Kernel, _target, measurement);

            DebugDisplay.Write($" DIST = {_lastDistance,6:F3}");

            if (measurementId < _measrIndices.Length)  // when the candidates are still being initialized, check the grand minima only
            {
                if (_lastDistance < _grandMinima)
                {
                    _grandMinima = _lastDistance;
                    _grandMinimaIndex = measurementId;
                }
            }
            else  // full DE procedure apllied
            {
                // update distance minimas

                var cf = Math.Min(_lastDistance, previousDistance);
                string[] info = ["  ", "  ", ""];

                var lastId = _allCandidates.ColumnCount - 1;

                // Global minima
                if (cf < _grandMinima)
                {
                    _grandMinima = cf;
                    _grandMinimaIndex = lastId;
                    info[0] = "GM";
                }

                // Minima of the tested vectors
                if (_lastDistance < _iterationMinima)
                {
                    _iterationMinima = _lastDistance;
                    _iterationMinimaIndex = candId;
                    info[1] = "IM";
                }

                // replace target vec with trial vec if it has lower cf
                if (_lastDistance < previousDistance)
                {
                    _candidates.ReplaceColumn(candId, _iterationCandidates.Column(candId));
                    info[2] = $"[{_measrIndices[candId] + 1} >> {lastId + 1}]";
                    _measrIndices[candId] = lastId;
                }

                DebugDisplay.Write($" {string.Join(' ', info)}");
            }

            DebugDisplay.WriteLine();

            if (_step == _candidates.ColumnCount) // after all candidates are initialized
            {
                DebugDisplay.WriteLine($"GM: {_grandMinima:F4} [{FormatRecipe(_channelNames, _allCandidates.Column(_grandMinimaIndex))}]");
            }
        }

        await Task.Delay(100);      // simply, to make ML being not too fast in SMOP interface

        Recipe? recipe;

        if (_step < _measrIndices.Length)  // Provide initial recipes
        {
            var flows = _candidates.Column(_step);
            DebugDisplay.Write($"[{_step + 1}] {FormatRecipe(_channelNames, flows)}");
            recipe = GetRecipe($"Reference #{_step + 1}", flows, false, _lastDistance);
        }
        else  
        {
            int candId = _step % _measrIndices.Length;

            if (candId == 0)   // new iteration starts
            {
                _iter += 1;

                if (_iter > 1)
                {
                    DebugDisplay.WriteLine($"IM: {_iterationMinima:F4} [{FormatRecipe(_channelNames, _iterationCandidates.Column(_iterationMinimaIndex))}]");
                    DebugDisplay.WriteLine($"GM: {_grandMinima:F4} [{FormatRecipe(_channelNames, _allCandidates.Column(_grandMinimaIndex))}]");

                    // Make a decision about the proximity of the best guess

                    string? recipeName = null;
                    if (_grandMinima < _config.Threshold)
                    {
                        recipeName = "Final recipe";
                    }
                    else if (_iter > _config.MaxIterations)
                    { 
                        recipeName = $"The best vectors after {_config.MaxIterations} iterations";
                    }

                    if (!string.IsNullOrEmpty(recipeName))
                    {
                        _isFinished = true;

                        // Send the final recipe
                        var f = LimitValues(_allCandidates.Column(_grandMinimaIndex), FLOW_MIN, FLOW_MAX);
                        recipe = GetRecipe(recipeName, f, _isFinished, _lastDistance);
                        DebugDisplay.WriteLine($"\n{recipeName}  {FormatRecipe(_channelNames, _allCandidates.Column(_grandMinimaIndex))}, DIST = {_grandMinima:F4}\n\nFinished");
                        return recipe;
                    }
                    else
                    {
                        DebugDisplay.WriteLine("The best vectors are:");
                        DebugDisplay.WriteLine(FormatRecipesAll(_channelNames, _candidates));
                    }
                }

                DebugDisplay.WriteLine($"\nIteration #{_iter}:");
                _iterationMinima = 1e8;
                _iterationMinimaIndex = -1;

                var mutated = Mutate(_candidates, _parameters.MutationFactor, FLOW_MIN, FLOW_MAX);   // generate new vectors
                _iterationCandidates = Crossover(_candidates, mutated, _parameters.CrossoverRate);                // mix old and new vectors
                Validate(_iterationCandidates, _flowVariationDelta, FLOW_MIN, FLOW_MAX);               // remove repetitions
                Round(_iterationCandidates, _parameters.Decimals);                          // round flow values

                DebugDisplay.WriteLine($"Vectors to test:\n{FormatRecipesAll(_channelNames, _iterationCandidates)}");
            }

            var flows = LimitValues(_iterationCandidates.Column(candId), FLOW_MIN, FLOW_MAX);

            DebugDisplay.Write($"[{_step}] {FormatRecipe(_channelNames, _iterationCandidates.Column(candId))}");

            recipe = GetRecipe($"Iteration #{_iter}, Search #{candId + 1}", flows, _isFinished, _lastDistance);
        }

        _step++;

        return recipe;
    }

    // Internal

    record class MinMax(double Min, double Max);

    const double FLOW_MIN = 0;
    const double FLOW_MAX = 100;
    const int FLOW_DURATION_ENDLESS = -1;

    readonly Config _config;
    readonly Parameters _parameters;
    readonly Random _rnd = new((int)DateTime.Now.Ticks);

    readonly int[] _channelIDs;
    readonly string[] _channelNames;
    readonly MinMax[] _channelRanges;

    readonly bool _hasDmsSource;

    readonly List<double[]> _allMeasurements = new();
    readonly int[] _measrIndices;       // indices of measurements corresponding to vectors of _candidates
    readonly double _flowVariationDelta;

    double _ucv = 0;
    double[] _target = [];

    double _grandMinima = 1e8;          // overall minimum distance (global minima)
    int _grandMinimaIndex = -1;         // column of _allMeasurements and _allCandidates corresponding to the global minima;
    double _iterationMinima = 1e8;      // minimum distance for vectors tested in iteration
    int _iterationMinimaIndex = -1;     // column of _iterationCandidates that corresponds to iteration minima
    double _lastDistance = 1e8;         // last search distance (measured trials only)

    MatrixD _allCandidates;
    MatrixD _candidates;
    MatrixD _iterationCandidates;

    int _step = 0;
    int _iter = 0;      // iteration counter

    bool _isFinished = false;     // switch for terminating the iterative process


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
            gains[i] = (_channelRanges[i].Max - _channelRanges[i].Min) / (FLOW_MAX - FLOW_MIN);
            i++;
        }

        return (offsets, gains);
    }

    //  Formats gas names N with their flows V as "GAS1=FLOW1 GAS2=FLOW2 ..."
    private static string FormatRecipe(string[] channelNames, MatrixD recipeFlows)
    {
        if (channelNames.Length != recipeFlows.Size)
            return string.Empty;

        List<string> result = new();

        for (int i = 0; i < recipeFlows.Size && i < channelNames.Length; i++)
        {
            result.Add($"{channelNames[i]} = {recipeFlows[i],-4:F1}");
        }

        return string.Join(" ", result);
    }

    // Formats gas names N with a list of flows V as
    //   GAS1 FLOW1 FLOW2..
    //   GAS2 FLOW1 FLOW2..
    //   ..
    private static string FormatRecipesAll(string[] channelNames, MatrixD allRecipeFlows)
    {
        StringBuilder result = new();
        for (int r = 0; r < allRecipeFlows.RowCount && r < channelNames.Length; r++)
        {
            result.Append($"{channelNames[r],-12}");
            for (int c = 0; c < allRecipeFlows.ColumnCount; c++)
                result.Append($" {allRecipeFlows[r, c],4:F1}");
            result.AppendLine();
        }
        return result.ToString();
    }

    private MatrixD CreateInitialVectors(int channelCount, double flowMin, double flowMax)
    {
        var interval = flowMax - flowMin;
        var center = (flowMax + flowMin) / 2;

        if (channelCount < 1)
            return new MatrixD(1, 1, center);

        var min = flowMin + interval * 0.12f;
        var max = flowMax - interval * 0.08f;   // different delta to add some imbalance

        MatrixD? result = null;

        for (int i = channelCount; i >= 0; i--)
        {
            var a = new MatrixD(1, i, min);           // [min, min, ...  (or nothing if i == 0)
            var b = new MatrixD(1, channelCount - i, max);      // ... max, max] (or nothing if i == n)
            var ab = MatrixD.StackColumns(a, b);      // unite both parts
            ab = MatrixD.Permutate(ab);               // save all permutations as rows
            if (result == null)
                result = ab;
            else
                result = MatrixD.StackRows(result, ab);   // add to the resulting list
        }

        result = result!
            .RemoveDuplicates(Direction.Rows)       // remove duplications produced by permutations
            .Transpose();                           // and transpose the matrix

        var central = new MatrixD(channelCount, 1, center);    // add central point
        result = MatrixD.StackColumns(result, central);

        while (result.Size < 4)                     // in case n = 1 that gives only 3 values..
        {
            var value = _rnd.NextDouble(min, max);
            result = MatrixD.StackColumns(result, new MatrixD(result.RowCount, 1, value));    // ..add a random value
        }

        return result;
    }

    // A cost function that measures similarity(distance) between dispersion plot 
    // of the mixture we want to recreate and the training dispesion plot.
    private static double GetDistance(Kernel kernel, double[] a, double[] b) => kernel switch
        {
            Kernel.Euclidean => Math.Sqrt((new MatrixD(a) - new MatrixD(b)).Power(2).Mean()),
            _ => throw new NotImplementedException("Kernel not supported")
        };

    private MatrixD Mutate(MatrixD testedFlows, double f, double flowMin, double flowMax)
    {
        var result = new MatrixD(testedFlows.RowCount, 0);

        // Allow to accept mutated vectors with values slightly beyond the
        // limits.The values anyway will be adjusted to bring them within
        // the scope in the LimitValues function
        var delta = (flowMax - flowMin) * 0.05;   // to each side beyond the limits
        flowMin = flowMin - delta;
        flowMax = flowMax + delta;

        for (int c = 0; c < testedFlows.ColumnCount; c++)
        {
            var candidate = KeepInRange(testedFlows, c, flowMin, flowMax, (flows, column) => MutateOne(flows, f, column));
            result = result.StackColumns(candidate);
        }

        return result;
    }

    private MatrixD MutateOne(MatrixD flows, double f, int column)
    {
        if (column < 0 || column >= flows.ColumnCount)
            throw new ArgumentException("Mutation: Invalid column");

        // Pick three distinct vectors from flows that are different from "column"

        int[] ids = Enumerable.Range(0, flows.ColumnCount).ToArray();  // random permutation of integers 0..ColumnCount
        _rnd.Shuffle(ids);

        // remove index "column"
        int[] indices = new int[flows.ColumnCount - 1];
        for (int i = 0, j = 0; i < ids.Length; i++)
            if (ids[i] != column)
                indices[j++] = ids[i];

        // Compute donor vector from first three vectors
        // pick three distinct dispersion plots (also PID, SNT)
        // (need to be distinct from each other and from x)
        return flows.Column(indices[1]) + f * (flows.Column(indices[2]) - flows.Column(indices[3]));
    }

    // Uses binomial method for combining components from target and donor vectors
    private MatrixD Crossover(MatrixD testedFlows, MatrixD candidateFlows, double cr)
    {
        // Generate randomly chosen indices to ensure that at least one
        // component of the donor vector is included in the target vector
        var randomIndices = testedFlows.Row(0).Select(_ => _rnd.Next(testedFlows.ColumnCount)).ToArray();
        
        // Random numbers in [0, 1] for each component of each target vector
        var crossoverProbabilities = new MatrixD(testedFlows.RowCount, testedFlows.ColumnCount, (r,c) => _rnd.NextDouble());
        
        // Combining target and donor vectors to get trial vectors
        var result = new MatrixD(testedFlows.RowCount, testedFlows.ColumnCount, double.NaN);

        for (int c = 0; c < testedFlows.ColumnCount; c++)
        {
            for (int r = 0; r < testedFlows.RowCount; r++)
            {
                if (crossoverProbabilities[r, c] <= cr || randomIndices[r] == c)
                    result[r, c] = candidateFlows[r, c];
                else                                // OLEG: extreamly rare if cr = 0.8, but
                    result[r, c] = testedFlows[r, c];     //% happens in 10 - 30 % cases when cr = 0.5
            }
        }

        return result;
    }

    // Avoid the search algorithm to stuck with testing same or similar vectors
    private void Validate(MatrixD candidateFlows, double delta, double min, double max)
    {
        double[] interval = [-delta, delta];

        // Replace repeated pairs with random pairs
        for (int c = 1; c < candidateFlows.ColumnCount; c++)
        {
            for (int i = 0; i < c - 1; i++)
            {
                var prevColumn = candidateFlows.Column(i);
                if (candidateFlows.Column(c).Equals(prevColumn))
                {
                    // deviate by +- delta
                    var candidate = KeepInRange(candidateFlows, i, min, max, (flows, j) =>
                        flows.Column(j) + new MatrixD(candidateFlows.RowCount, 1, (r, c) =>
                            _rnd.NextDouble(-delta, delta)
                        )
                    );
                    candidateFlows.ReplaceColumn(i, candidate);
                    DebugDisplay.WriteLine($"Validator: [{i}] '{prevColumn}' >> '{candidateFlows.Column(i)}'");
                }
            }
        }

        //  If all flow values of a certain gas are the same..
        bool[] areAllSame = candidateFlows.Column(0).Select(_ => true).ToArray();
        for (int c = 1; c < candidateFlows.ColumnCount; c++)
            for (int r = 0; r < candidateFlows.RowCount; r++)
                areAllSame[r] = areAllSame[r] && (candidateFlows[r, c - 1] == candidateFlows[r, c]);

        // .. then randomize those values a bit
        for (int r = 0; r < candidateFlows.RowCount; r++)
        {
            if (areAllSame[r])
            {
                var prevChannelFlows = candidateFlows.Row(r);
                var newChannelFlows = KeepInRange(candidateFlows, r, min, max, (flows, i) =>
                    flows.Row(i) + new MatrixD(1, candidateFlows.ColumnCount, (r, c) =>
                        _rnd.NextDouble(-delta, delta)
                    )
                );
                candidateFlows.ReplaceRow(r, newChannelFlows);
                DebugDisplay.WriteLine($"Validator: '{prevChannelFlows}' >> '{newChannelFlows}'");
            }
        }
    }

    private MatrixD KeepInRange(MatrixD flows, int index, double min, double max, Func<MatrixD, int, MatrixD> createVector)
    {
        // We try to change vector values so that all stay in the range [min, max]
        // However, we may get scenarios when this is impossible or takes
        // too many trials to complete

        var maxTrialCount = 20;

        var result = new MatrixD();

        while (maxTrialCount > 0)
        {
            result = createVector(flows, index);  // the callback create either a column or a row

            if (result.All(v => v >= min && v <= max))  // nice, all values are withing the range
                break;

            // some values are out of range, lets display then and try again
            var temp = result.Copy();
            if (temp.RowCount > 1)
                temp = temp.Transpose();

            DebugDisplay.WriteLine($"Rejected: [{index}] {temp}");

            maxTrialCount -= 1;
        }

        if (maxTrialCount == 0) // oh, we ran out of trials.. just generate some random numbers within the range
        {
            min = Math.Round(min);
            max = Math.Round(max);

            result = new MatrixD(result.RowCount, result.ColumnCount, (r,c) => _rnd.NextDouble(min, max));

            DebugDisplay.WriteLine($"[{index}] Failed to create a vector within the limits. A random vector is generated instead.");
        }

        return result;
    }

    // Rounds values in the vector.
    // If dec < 0, then rounds the values to be divisible by | dec |.
    private static void Round(MatrixD matrix, int decimals)
    {
        var decim = Math.Max(decimals, 0);

        for (int r = 0; r < matrix.RowCount; r++)
            for (int c = 0; c < matrix.ColumnCount; c++)
                matrix[r, c] = Math.Round(matrix[r, c], decim);

        if (decimals < 0)
        {
            decim = Math.Abs(decimals) + 1;
            for (int r = 0; r < matrix.RowCount; r++)
                for (int c = 0; c < matrix.ColumnCount; c++)
                    matrix[r, c] = Math.Round(matrix[r, c] / decim) * decim;
        }
    }


    // Limits the values to stay within bounds
    private static MatrixD LimitValues(MatrixD matrix, double min, double max) =>
        new(matrix.RowCount, matrix.ColumnCount, (r, c) =>
        {
            if (matrix[r, c] < min)
                return min;
            if (matrix[r, c] > max)
                return max;
            return matrix[r, c];
        });

    private Recipe GetRecipe(string name, MatrixD flows, bool isFinal, double distance)
    {
        var (offsets, gains) = GetFlowTransformations();

        var channels = new List<ChannelRecipe>();

        int i = 0;
        foreach (var flow in flows)
        {
            channels.Add(new ChannelRecipe(
                _channelIDs[i],
                (float)(offsets[i] + flow * gains[i]),
                FLOW_DURATION_ENDLESS
            ));
            i++;
        }

        return new Recipe(name, isFinal, (float)distance, channels.ToArray());
    }
}
