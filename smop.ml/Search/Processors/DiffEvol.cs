using System;
using System.Collections.Generic;
using System.Linq;
using MatrixD = Smop.ML.Search.Matrix<double>;

namespace Smop.ML.Search;

/// <summary>
/// In future, this class may implement interface that would be common
/// for all iterative algorithms
/// </summary>
internal class DiffEvol
{
    public bool HasTarget => _target.Length > 0;
    public static double SearchRange => VALUE_MAX - VALUE_MIN;

    /// <summary>
    /// Arguments for RequestFormat event
    /// </summary>
    /// <param name="matrix">The Matrix stores vectors in columns</param>
    public class RequestFormatEventArgs(MatrixD matrix) : EventArgs
    {
        public MatrixD Matrix => matrix;
        /// <summary>
        /// The listener must set this member
        /// </summary>
        public string Result { get; set; } = string.Empty;
    }

    /// <summary>
    /// Requests from the general search procedure (the parent)
    /// to format vectors so that they are ready to display for debugging purposes
    /// </summary>
    public event EventHandler<RequestFormatEventArgs>? RequestFormat;

    public DiffEvol(DiffEvolParameters parameters, int varCount)
    {
        _parameters = parameters;

        // Population size: set of best vectors
        _bestVectors = CreateInitialVectors(varCount, VALUE_MIN, VALUE_MAX);
        Round(_bestVectors, _parameters.Decimals);

        _iterationVectors = _bestVectors.Copy();

        _candidateIndices = Enumerable.Range(0, _bestVectors.ColumnCount).ToArray();    // range of X and U

        _valueDelta = 0.2 * (VALUE_MAX - VALUE_MIN);
    }

    public void SetTarget(double[] target)
    {
        _target = target;

        DebugDisplay.WriteLine("\nCollecting initial measurements");
    }

    public void AddMeasurement(double[] measurement)
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

        if (candidateId >= _bestVectors.ColumnCount) // after all vectors were initialized (iterations 2+)
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
            else
            {
                info[2] = $"[{_candidateIndices[vectorId] + 1}]";
            }

            DebugDisplay.Write($" {string.Join(' ', info)}");
        }

        DebugDisplay.WriteLine();
    }

    public TestCandidate GetTestCandidate()
    {
        TestCandidate result;

        if (_testedCandidates.Count < _candidateIndices.Length)  // Provide initial recipes
        {
            var index = _testedCandidates.Count;
            var vector = _bestVectors.Column(index);

            var formatArgs = new RequestFormatEventArgs(vector);
            RequestFormat?.Invoke(this, formatArgs);
            DebugDisplay.Write($"[{index + 1}] {formatArgs.Result}");

            result = new TestCandidate($"Reference #{index + 1}", vector, false, _lastDistance);
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
                    {
                        var vector = _iterationVectors.Column(_iterationMinimaIndex);
                        var formatArgs = new RequestFormatEventArgs(vector);
                        RequestFormat?.Invoke(this, formatArgs);
                        DebugDisplay.WriteLine($"IM: {_iterationMinima:F4} [{formatArgs.Result}]");
                    }

                    {
                        var vector = _testedCandidates[_grandMinimaIndex].Vector;
                        var formatArgs = new RequestFormatEventArgs(vector);
                        RequestFormat?.Invoke(this, formatArgs);
                        DebugDisplay.WriteLine($"GM: {_grandMinima:F4} [{formatArgs.Result}]");
                    }

                    // Make a decision about the proximity of the best guess

                    string? bestRecipeName = null;
                    if (_grandMinima < _parameters.DistanceThreshold)
                    {
                        bestRecipeName = "Final recipe";
                    }
                    else if (iterationId > _parameters.MaxIterations)
                    {
                        bestRecipeName = $"The best vector after {_parameters.MaxIterations} iterations";
                    }

                    if (!string.IsNullOrEmpty(bestRecipeName))
                    {
                        // Send the final recipe
                        var bestVector = _testedCandidates[_grandMinimaIndex].Vector;
                        var bestValidVector = LimitValues(bestVector, VALUE_MIN, VALUE_MAX);

                        var formatArgs = new RequestFormatEventArgs(bestVector);
                        RequestFormat?.Invoke(this, formatArgs);
                        DebugDisplay.WriteLine($"\n{bestRecipeName}:\n  {formatArgs.Result}\n  DIST = {_grandMinima:F4}\n\nFinished");

                        return new TestCandidate(bestRecipeName, bestValidVector, true, _lastDistance);
                    }
                    else
                    {
                        DebugDisplay.WriteLine("The best vectors are:");

                        var formatArgs = new RequestFormatEventArgs(_bestVectors);
                        RequestFormat?.Invoke(this, formatArgs);
                        DebugDisplay.WriteLine(formatArgs.Result);
                    }
                }

                DebugDisplay.WriteLine($"\nIteration #{iterationId}:");
                _iterationMinima = 1e8;
                _iterationMinimaIndex = -1;

                var mutatedVectors = Mutate(_bestVectors, _parameters.MutationFactor, VALUE_MIN, VALUE_MAX);   // generate new vectors
                _iterationVectors = Crossover(_bestVectors, mutatedVectors, _parameters.CrossoverRate);        // mix old and new vectors
                Validate(_iterationVectors, _valueDelta, VALUE_MIN, VALUE_MAX);          // remove repetitions
                Round(_iterationVectors, _parameters.Decimals);                          // round values

                {
                    var formatArgs = new RequestFormatEventArgs(_iterationVectors);
                    RequestFormat?.Invoke(this, formatArgs);
                    DebugDisplay.WriteLine($"Vectors to test:\n{formatArgs.Result}");
                }
            }

            var validVector = LimitValues(_iterationVectors.Column(vectorId), VALUE_MIN, VALUE_MAX);

            {
                var formatArgs = new RequestFormatEventArgs(_iterationVectors.Column(vectorId));
                RequestFormat?.Invoke(this, formatArgs);
                DebugDisplay.Write($"[{_testedCandidates.Count + 1}] {formatArgs.Result}");
            }

            result = new TestCandidate($"Iteration #{iterationId}, Search #{vectorId + 1}", validVector, false, _lastDistance);
        }

        return result;
    }

    // Internal

    record class Candidate(MatrixD Vector, double[] Measurement, double Distance);

    const double VALUE_MIN = 0;
    const double VALUE_MAX = 100;

    readonly DiffEvolParameters _parameters;
    readonly Random _rnd = new((int)DateTime.Now.Ticks);

    readonly List<Candidate> _testedCandidates = new();
    readonly int[] _candidateIndices;       // indices of measurements corresponding to vectors of _testedCandidates
    readonly MatrixD _bestVectors;

    readonly double _valueDelta;

    double[] _target = [];

    double _grandMinima = 1e8;          // overall minimum distance (global minima)
    int _grandMinimaIndex = -1;         // column of _testedCandidates corresponding to the global minima;
    double _iterationMinima = 1e8;      // minimum distance for vectors tested in iteration
    int _iterationMinimaIndex = -1;     // column of _iterationVectors that corresponds to iteration minima

    double _lastDistance = 1e8;         // last search distance (measured trials only)

    MatrixD _iterationVectors;

    // Creates vector with values close to edges, plus adds a ector with central values
    private MatrixD CreateInitialVectors(int varCount, double min, double max)
    {
        var interval = max - min;
        var center = (max + min) / 2;

        if (varCount < 1)
            return new MatrixD(1, 1, center);

        min += interval * 0.12f;
        max -= interval * 0.08f;   // different delta to add some imbalance

        MatrixD? result = null;

        for (int i = varCount; i >= 0; i--)
        {
            var a = new MatrixD(1, i, min);           // [min, min, ...  (or nothing if i == 0)
            var b = new MatrixD(1, varCount - i, max);      // ... max, max] (or nothing if i == n)
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

        var central = new MatrixD(varCount, 1, center);    // add central point
        result = MatrixD.StackColumns(result, central);

        while (result.Size < 4)                     // in case n = 1 that gives only 3 values..
        {
            var value = _rnd.NextDouble(min, max);
            result = MatrixD.StackColumns(result, new MatrixD(result.RowCount, 1, value));    // ..add a random value
        }

        return result;
    }

    // Rounds values in the vector.
    // If decimals < 0, then rounds the values to be divisible by | dec |.
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

    // A cost function that measures similarity(distance) between dispersion plot 
    // of the mixture we want to recreate and the training dispesion plot.
    private static double GetDistance(Kernel kernel, double[] a, double[] b) => kernel switch
    {
        Kernel.Euclidean => Math.Sqrt((new MatrixD(a) - new MatrixD(b)).Power(2).Mean()),
        _ => throw new NotImplementedException("Kernel not supported")
    };

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

    // Vector mutation procedure that iterated over all vectors
    private MatrixD Mutate(MatrixD vectors, double f, double min, double max)
    {
        var result = new MatrixD(vectors.RowCount, 0);

        // Allow to accept mutated vectors with values slightly beyond the
        // limits.The values anyway will be adjusted to bring them within
        // the scope in the LimitValues function
        var delta = (max - min) * 0.05;   // to each side beyond the limits
        min -= delta;
        max += delta;

        for (int c = 0; c < vectors.ColumnCount; c++)
        {
            var candidate = KeepInRange(vectors, c, min, max, (vectors, column) => MutateOne(vectors, column, f));
            result = result.StackColumns(candidate);
        }

        return result;
    }

    // SIngle vector mutatin procedure
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

            result = new MatrixD(result.RowCount, result.ColumnCount, (r, c) => _rnd.NextDouble(min, max));

            DebugDisplay.WriteLine($"[{index}] Failed to create a vector within the limits. A random vector is generated instead.");
        }

        return result;
    }

    // Uses binomial method for combining components from target and donor vectors
    private MatrixD Crossover(MatrixD originalVectors, MatrixD mutetedVectors, double cr)
    {
        // Generate randomly chosen indices to ensure that at least one
        // component of the donor vector is included in the target vector
        var randomIndices = originalVectors.Row(0).Select(_ => _rnd.Next(originalVectors.ColumnCount)).ToArray();

        // Random numbers in [0, 1] for each component of each target vector
        var crossoverProbabilities = new MatrixD(originalVectors.RowCount, originalVectors.ColumnCount, (r, c) => _rnd.NextDouble());

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
}
