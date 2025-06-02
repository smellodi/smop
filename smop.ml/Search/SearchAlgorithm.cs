using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatrixD = Smop.ML.Search.Matrix<double>;

namespace Smop.ML.Search;

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

internal class SearchAlgorithm
{
    public SearchAlgorithm(Config config)
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
        _bestVectors = CreateInitialVectors(_channelIDs.Length, VALUE_MIN, VALUE_MAX);
        Round(_bestVectors, _parameters.Decimals);

        _iterationVectors = _bestVectors.Copy();

        _candidateIndices = Enumerable.Range(0, _bestVectors.ColumnCount).ToArray();    // range of X and U

        _valueDelta = 0.2 * (VALUE_MAX - VALUE_MIN);
    }

    public bool AddMeasurement(Content content)
    {
        if (!_hasDmsSource && content.Source != Source.SNT)
            return false;

        var measurement = GetMeasurementData(content);
        if (measurement.Length == 0)
            return false;

        // the first measurement is always the target measurement
        if (_target.Length == 0)
        {
            if (content is DmsMeasurement dms)
            {
                _ucv = dms.Setup.Ucv.Steps == 1 ? dms.Setup.Ucv.Min : 0;
            }

            _target = measurement;

            DebugDisplay.WriteLine("\nCollecting initial measurements");
        }
        else
        {
            // Handling the arrived measurement: push it to the stack of candidates
            // with the vector that was used for this measurement and calculated distance to the target

            var candidateId = _testedCandidates.Count;
            int vectorId = candidateId % _candidateIndices.Length;

            _lastDistance = GetDistance(_parameters.Kernel, _target, measurement);

            _testedCandidates.Add(new(_iterationVectors.Column(vectorId), measurement, _lastDistance));

            DebugDisplay.Write($" DIST = {_lastDistance,6:F3}");

            string[] info = ["  ", "  ", ""];

            // Global minima
            if (_lastDistance < _grandMinima)
            {
                _grandMinima = _lastDistance;
                _grandMinimaIndex = candidateId;
                info[0] = "GM";
            }

            if (candidateId >= _bestVectors.ColumnCount) // after all vectors were initialized
            {
                // Iteration minima
                if (_lastDistance < _iterationMinima)
                {
                    _iterationMinima = _lastDistance;
                    _iterationMinimaIndex = vectorId;
                    info[1] = "IM";
                }

                // replace target vector with trial vector if it has smaller distance
                var previousDistance = _testedCandidates[_candidateIndices[vectorId]].Distance;
                if (_lastDistance < previousDistance)
                {
                    _bestVectors.ReplaceColumn(vectorId, _iterationVectors.Column(vectorId));
                    info[2] = $"[{_candidateIndices[vectorId] + 1} >> {candidateId + 1}]";
                    _candidateIndices[vectorId] = candidateId;
                }

                DebugDisplay.Write($" {string.Join(' ', info)}");
            }

            DebugDisplay.WriteLine();
        }

        return true;
    }

    public async Task<Recipe> GetRecipe()
    {
        await Task.Delay(100);      // simply, to make ML being not too fast in SMOP interface

        Recipe? recipe;

        if (_testedCandidates.Count < _candidateIndices.Length)  // Provide initial recipes
        {
            var index = _testedCandidates.Count;
            var vector = _bestVectors.Column(index);
            DebugDisplay.Write($"[{index + 1}] {FormatRecipe(_channelNames, vector)}");
            recipe = GetRecipe($"Reference #{index + 1}", vector, false, _lastDistance);
        }
        else  
        {
            int vectorId = _testedCandidates.Count % _bestVectors.ColumnCount;
            int iterationId = _testedCandidates.Count / _bestVectors.ColumnCount;

            if (vectorId == 0)   // new iteration starts
            {
                if (iterationId > 0)
                {
                    if (_iterationMinimaIndex >= 0)
                        DebugDisplay.WriteLine($"IM: {_iterationMinima:F4} [{FormatRecipe(_channelNames, _iterationVectors.Column(_iterationMinimaIndex))}]");

                    DebugDisplay.WriteLine($"GM: {_grandMinima:F4} [{FormatRecipe(_channelNames, _testedCandidates[_grandMinimaIndex].Vector)}]");

                    // Make a decision about the proximity of the best guess

                    string? bestRecipeName = null;
                    if (_grandMinima < _config.Threshold)
                    {
                        bestRecipeName = "Final recipe";
                    }
                    else if (iterationId > _config.MaxIterations)
                    { 
                        bestRecipeName = $"The best vectors after {iterationId} iterations";
                    }

                    if (!string.IsNullOrEmpty(bestRecipeName))
                    {
                        // Send the final recipe
                        var bestVector = _testedCandidates[_grandMinimaIndex].Vector;
                        var bestValidVector = LimitValues(bestVector, VALUE_MIN, VALUE_MAX);
                        DebugDisplay.WriteLine($"\n{bestRecipeName}  {FormatRecipe(_channelNames, bestVector)}, DIST = {_grandMinima:F4}\n\nFinished");
                        return GetRecipe(bestRecipeName, bestValidVector, true, _lastDistance);
                    }
                    else
                    {
                        DebugDisplay.WriteLine("The best vectors are:");
                        DebugDisplay.WriteLine(FormatRecipesAll(_channelNames, _bestVectors));
                    }
                }

                DebugDisplay.WriteLine($"\nIteration #{iterationId}:");
                _iterationMinima = 1e8;
                _iterationMinimaIndex = -1;

                var mutatedVectors = Mutate(_bestVectors, _parameters.MutationFactor, VALUE_MIN, VALUE_MAX);   // generate new vectors
                _iterationVectors = Crossover(_bestVectors, mutatedVectors, _parameters.CrossoverRate);        // mix old and new vectors
                Validate(_iterationVectors, _valueDelta, VALUE_MIN, VALUE_MAX);          // remove repetitions
                Round(_iterationVectors, _parameters.Decimals);                          // round values

                DebugDisplay.WriteLine($"Vectors to test:\n{FormatRecipesAll(_channelNames, _iterationVectors)}");
            }

            var validVector = LimitValues(_iterationVectors.Column(vectorId), VALUE_MIN, VALUE_MAX);

            DebugDisplay.Write($"[{_testedCandidates.Count + 1}] {FormatRecipe(_channelNames, _iterationVectors.Column(vectorId))}");
            recipe = GetRecipe($"Iteration #{iterationId}, Search #{vectorId + 1}", validVector, false, _lastDistance);
        }

        return recipe;
    }

    // Internal

    record class MinMax(double Min, double Max);

    record class Candidate(MatrixD Vector, double[] Measurement, double Distance);

    const double VALUE_MIN = 0;
    const double VALUE_MAX = 100;
    const int FLOW_DURATION_ENDLESS = -1;

    readonly Config _config;
    readonly Parameters _parameters;
    readonly Random _rnd = new((int)DateTime.Now.Ticks);

    readonly int[] _channelIDs;
    readonly string[] _channelNames;
    readonly MinMax[] _channelRanges;

    readonly bool _hasDmsSource;

    //readonly List<double[]> _allMeasurements = new();
    readonly List<Candidate> _testedCandidates = new();
    readonly int[] _candidateIndices;       // indices of measurements corresponding to vectors of _candidates
    readonly double _valueDelta;

    double _ucv = 0;
    double[] _target = [];

    double _grandMinima = 1e8;          // overall minimum distance (global minima)
    int _grandMinimaIndex = -1;         // column of _allMeasurements and _allCandidates corresponding to the global minima;
    double _iterationMinima = 1e8;      // minimum distance for vectors tested in iteration
    int _iterationMinimaIndex = -1;     // column of _iterationCandidates that corresponds to iteration minima
    double _lastDistance = 1e8;         // last search distance (measured trials only)

    MatrixD _bestVectors;
    MatrixD _iterationVectors;

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
            gains[i] = (_channelRanges[i].Max - _channelRanges[i].Min) / (VALUE_MAX - VALUE_MIN);
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

        for (int i = 0; i < vector.Size && i < varNames.Length; i++)
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
        StringBuilder result = new();
        for (int r = 0; r < vectors.RowCount && r < varNames.Length; r++)
        {
            result.Append($"{varNames[r],-12}");
            for (int c = 0; c < vectors.ColumnCount; c++)
                result.Append($" {vectors[r, c],4:F1}");
            result.AppendLine();
        }
        return result.ToString();
    }

    private MatrixD CreateInitialVectors(int channelCount, double min, double max)
    {
        var interval = max - min;
        var center = (max + min) / 2;

        if (channelCount < 1)
            return new MatrixD(1, 1, center);

        min = min + interval * 0.12f;
        max = max - interval * 0.08f;   // different delta to add some imbalance

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

    private MatrixD Mutate(MatrixD vectors, double f, double min, double max)
    {
        var result = new MatrixD(vectors.RowCount, 0);

        // Allow to accept mutated vectors with values slightly beyond the
        // limits.The values anyway will be adjusted to bring them within
        // the scope in the LimitValues function
        var delta = (max - min) * 0.05;   // to each side beyond the limits
        min = min - delta;
        max = max + delta;

        for (int c = 0; c < vectors.ColumnCount; c++)
        {
            var candidate = KeepInRange(vectors, c, min, max, (vectors, column) => MutateOne(vectors, column, f));
            result = result.StackColumns(candidate);
        }

        return result;
    }

    private MatrixD MutateOne(MatrixD vectors, int column, double f)
    {
        if (column < 0 || column >= vectors.ColumnCount)
            throw new ArgumentException("Mutation: Invalid column");

        // Pick three distinct vectors that are different from "column"

        int[] ids = Enumerable.Range(0, vectors.ColumnCount).ToArray();  // random permutation of integers 0..ColumnCount
        _rnd.Shuffle(ids);

        // remove index "column"
        int[] indices = new int[vectors.ColumnCount - 1];
        for (int i = 0, j = 0; i < ids.Length; i++)
            if (ids[i] != column)
                indices[j++] = ids[i];

        // Compute donor vector from first three vectors
        // pick three distinct dispersion plots (also PID, SNT)
        // (need to be distinct from each other and from x)
        return vectors.Column(indices[1]) + f * (vectors.Column(indices[2]) - vectors.Column(indices[3]));
    }

    // Uses binomial method for combining components from target and donor vectors
    private MatrixD Crossover(MatrixD originalVectors, MatrixD mutetedVectors, double cr)
    {
        // Generate randomly chosen indices to ensure that at least one
        // component of the donor vector is included in the target vector
        var randomIndices = originalVectors.Row(0).Select(_ => _rnd.Next(originalVectors.ColumnCount)).ToArray();
        
        // Random numbers in [0, 1] for each component of each target vector
        var crossoverProbabilities = new MatrixD(originalVectors.RowCount, originalVectors.ColumnCount, (r,c) => _rnd.NextDouble());
        
        // Combining target and donor vectors to get trial vectors
        var result = new MatrixD(originalVectors.RowCount, originalVectors.ColumnCount, double.NaN);

        for (int c = 0; c < originalVectors.ColumnCount; c++)
        {
            for (int r = 0; r < originalVectors.RowCount; r++)
            {
                if (crossoverProbabilities[r, c] <= cr || randomIndices[r] == c)
                    result[r, c] = mutetedVectors[r, c];
                else                                // OLEG: extreamly rare if cr = 0.8, but
                    result[r, c] = originalVectors[r, c];     //% happens in 10 - 30 % cases when cr = 0.5
            }
        }

        return result;
    }

    // Avoid the search algorithm to stuck with testing same or similar vectors
    private void Validate(MatrixD vectors, double delta, double min, double max)
    {
        double[] interval = [-delta, delta];

        // Replace repeated pairs with random pairs
        for (int c = 1; c < vectors.ColumnCount; c++)
        {
            for (int i = 0; i < c - 1; i++)
            {
                var prevVector = vectors.Column(i);
                if (vectors.Column(c).Equals(prevVector))
                {
                    // deviate by +- delta
                    var candidate = KeepInRange(vectors, i, min, max, (vectors, j) =>
                        vectors.Column(j) + new MatrixD(vectors.RowCount, 1, (r, c) =>
                            _rnd.NextDouble(-delta, delta)
                        )
                    );
                    vectors.ReplaceColumn(i, candidate);
                    DebugDisplay.WriteLine($"Validator: [{i}] '{prevVector}' >> '{vectors.Column(i)}'");
                }
            }
        }

        //  If all variable values are the same..
        bool[] areAllSame = vectors.Column(0).Select(_ => true).ToArray();
        for (int c = 1; c < vectors.ColumnCount; c++)
            for (int r = 0; r < vectors.RowCount; r++)
                areAllSame[r] = areAllSame[r] && (vectors[r, c - 1] == vectors[r, c]);

        // .. then randomize those values a bit
        for (int r = 0; r < vectors.RowCount; r++)
        {
            if (areAllSame[r])
            {
                var variablePreviousValues = vectors.Row(r);
                var variableNewValues = KeepInRange(vectors, r, min, max, (vectors, i) =>
                    vectors.Row(i) + new MatrixD(1, vectors.ColumnCount, (r, c) =>
                        _rnd.NextDouble(-delta, delta)
                    )
                );
                vectors.ReplaceRow(r, variableNewValues);
                DebugDisplay.WriteLine($"Validator: '{variablePreviousValues}' >> '{variableNewValues}'");
            }
        }
    }

    private MatrixD KeepInRange(MatrixD matrix, int index, double min, double max, Func<MatrixD, int, MatrixD> createVector)
    {
        // We try to change vector values so that all stay in the range [min, max]
        // However, we may get scenarios when this is impossible or takes
        // too many trials to complete

        var maxTrialCount = 20;

        var result = new MatrixD();

        while (maxTrialCount > 0)
        {
            result = createVector(matrix, index);  // the callback create either a column or a row

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

    private Recipe GetRecipe(string name, MatrixD vector, bool isFinal, double distance)
    {
        var (offsets, gains) = GetFlowTransformations();

        var channels = new List<ChannelRecipe>();

        int i = 0;
        foreach (var flow in vector)
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
