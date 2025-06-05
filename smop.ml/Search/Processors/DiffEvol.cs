using System;
using System.Collections.Generic;
using System.Linq;
using MatrixD = Smop.ML.Search.Matrix<double>;

namespace Smop.ML.Search;

/// <summary>
/// In future, this class may implement interface that would be common
/// for all iterative algorithms
/// </summary>
internal class DiffEvol : IDisposable
{
    public bool HasTarget => _target.Length > 0;
    public static double SearchRange => VALUE_MAX - VALUE_MIN;

    /// <summary>
    /// Arguments for <see cref="RequestFormat"> event
    /// </summary>
    /// <param name="matrix">the natrix with vectors in columns</param>
    public class RequestFormatEventArgs(MatrixD matrix) : EventArgs
    {
        public MatrixD Matrix => matrix;

        /// <summary>
        /// The listener must set this member
        /// </summary>
        public string Result { get; set; } = string.Empty;
    }

    /// <summary>
    /// Requests the general search procedure (the owner)
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
        _vectorCount = _bestVectors.ColumnCount;

        _candidateIndices = Enumerable.Range(0, _vectorCount).ToArray();    // range of X and U

        _debugDisplay.WriteLine($"Parameters:\n{parameters}\n");
        _debugDisplay.Show();
    }

    public void Dispose()
    {
        _debugDisplay.Close();
    }

    public void SetTarget(double[] target)
    {
        _target = target;

        _debugDisplay.WriteLine("Initializing the pool of vectors");
    }

    public void AddMeasurement(double[] measurement)
    {
        // Handling the arrived measurement: push it to the stack of candidates
        // with the vector that was used for this measurement and calculated distance to the target

        var candidateId = _testedCandidates.Count;
        int vectorId = candidateId % _vectorCount;

        _lastDistance = GetDistance(_parameters.Kernel, _target, measurement);

        _testedCandidates.Add(new(_iterationVectors.Column(vectorId), measurement, _lastDistance));

        _debugDisplay.Write($" DIST = {_lastDistance,6:F3}");

        string[] info = ["  ", "  ", ""];

        // Update and print minimas

        // Global minima
        if (_lastDistance < _grandMinima)
        {
            _grandMinima = _lastDistance;
            _grandMinimaIndex = candidateId;
            info[0] = "GM";
        }

        if (candidateId >= _vectorCount) // after all vectors were initialized
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

            _debugDisplay.Write($" {string.Join(' ', info)}");
        }

        _debugDisplay.WriteLine();
    }

    public TestCandidate GetTestCandidate()
    {
        TestCandidate result;

        int vectorId = _testedCandidates.Count % _vectorCount;
        int iterationId = _testedCandidates.Count / _vectorCount;

        if (iterationId > 0 && vectorId == 0)   // new iteration starts: create a new set of vectors to test and store them into _iterationVectors
        {
            PrintIterationResultInfo();
            PrintSearchResultInfo();

            TestCandidate? bestCandidate = GetBestCandidate(iterationId);
            if (bestCandidate != null)
            {
                return bestCandidate;
            }

            _debugDisplay.WriteLine($"Iteration #{iterationId}:");

            _iterationMinima = 1e8;
            _iterationMinimaIndex = -1;

            var mutatedVectors = Mutate(_bestVectors, _parameters.MutationFactor, VALUE_MIN, VALUE_MAX);   // generate new vectors
            _iterationVectors = Crossover(_bestVectors, mutatedVectors, _parameters.CrossoverRate);        // mix old and new vectors
            Validate(_iterationVectors, VALUE_DELTA, VALUE_MIN, VALUE_MAX);          // remove repetitions
            Round(_iterationVectors, _parameters.Decimals);                          // round values

            PrintIterationStartInfo();
        }

        var vector = _iterationVectors.Column(vectorId);

        if (iterationId == 0)   // first iteration
        {
            result = new TestCandidate($"Initial #{vectorId + 1}", vector, false, _lastDistance);
        }
        else                    // all other iterations
        {
            var validVector = LimitValues(vector, VALUE_MIN, VALUE_MAX);    // the vector values could be beyound the valid scope after mutation
            result = new TestCandidate($"Iteration #{iterationId}, Search #{vectorId + 1}", validVector, false, _lastDistance);
        }
        
        PrintTrialStartInfo(vectorId, vector);

        return result;
    }

    // Internal

    record class Candidate(MatrixD Vector, double[] Measurement, double Distance);

    const double VALUE_MIN = 0;
    const double VALUE_MAX = 100;
    const double VALUE_DELTA = 0.2 * (VALUE_MAX - VALUE_MIN);
    const int CHECK_MUTATED_MAX_COUUNT = 20;

    readonly static Random _rnd = new((int)DateTime.Now.Ticks);

    readonly DebugDisplay _debugDisplay = new();

    readonly DiffEvolParameters _parameters;

    readonly List<Candidate> _testedCandidates = new(); // all candidates tested by ML; each incudes a vector, a measurement and a distance from the target
    readonly int[] _candidateIndices;       // indices of best candidates in _testedCandidates; correspond to vectors stored in _bestVectors
    readonly MatrixD _bestVectors;          // storage for the best vectors
    readonly int _vectorCount;              // same as _bestVectors.ColumnCount

    MatrixD _iterationVectors;          // vectors to test in an iteration

    double[] _target = [];              // e-nose measurement of the target mixture

    double _grandMinima = 1e8;          // overall minimum distance (global minima)
    int _grandMinimaIndex = -1;         // column of _testedCandidates corresponding to the global minima;
    double _iterationMinima = 1e8;      // minimum distance for vectors tested in an iteration
    int _iterationMinimaIndex = -1;     // column of _iterationVectors that corresponds to the iteration minima

    double _lastDistance = 1e8;         // distance between the target and the last arrived measurement

    // Creates vectors with values close to edges, plus adds a vector with central values
    private static MatrixD CreateInitialVectors(int varCount, double min, double max)
    {
        var interval = max - min;
        var center = (max + min) / 2;

        if (varCount < 1)
            return new MatrixD(1, 1, center);

        min += interval * 0.12f;
        max -= interval * 0.08f;   // different delta to add some imbalance

        MatrixD? result = null;

        // This is a bit tricky algorithm on how to create vectors with all possible combinations, but it works fine

        // first we create a long set of all possible permutations
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

        // ..then we remove the repeating vectors,
        result = result!
            .RemoveDuplicates(Direction.Rows)       // remove duplications produced by permutations
            .Transpose();                           // and transpose the matrix

        // ..and finally add the central vector
        var central = new MatrixD(varCount, 1, center);
        result = MatrixD.StackColumns(result, central);

        // in case varCount = 1 that gives only 3 vectors, a random vector should be added, as DE requires at least 4 vectors
        while (result.Size < 4)
        {
            var value = _rnd.NextDouble(min, max);
            result = MatrixD.StackColumns(result, new MatrixD(result.RowCount, 1, value));
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

    // A cost function that measures similarity (distance) between e-nose measurements
    // of the target mixture and the candidate mixture
    private static double GetDistance(Kernel kernel, double[] a, double[] b) => kernel switch
    {
        Kernel.Euclidean => Math.Sqrt((new MatrixD(a) - new MatrixD(b)).Power(2).Mean()),
        _ => throw new NotImplementedException("Kernel not supported")
    };

    // Limits the values to stay within bounds (this is needed as the mutation algorithm
    // may produce values slighly outlide the valid range)
    private static MatrixD LimitValues(MatrixD matrix, double min, double max) =>
        new(matrix.RowCount, matrix.ColumnCount, (r, c) =>
        {
            if (matrix[r, c] < min)
                return min;
            if (matrix[r, c] > max)
                return max;
            return matrix[r, c];
        });

    // Vector mutation procedure that iterates over all vectors
    private MatrixD Mutate(MatrixD vectors, double f, double min, double max)
    {
        var result = new MatrixD(vectors.RowCount, 0);

        // Allow to accept mutated vectors with values slightly beyond the
        // limits. The values anyway will be adjusted to bring them within
        // the scope in the LimitValues function
        var delta = (max - min) * 0.05;   // to each side beyond the limits
        min -= delta;
        max += delta;

        for (int c = 0; c < vectors.ColumnCount; c++)
        {
            var candidate = KeepInRange(min, max, () => MutateOne(vectors, c, f));
            result = result.StackColumns(candidate);
        }

        return result;
    }

    // Single vector mutation procedure
    private static MatrixD MutateOne(MatrixD vectors, int column, double f)
    {
        if (column < 0 || column >= vectors.ColumnCount)
            throw new ArgumentException("Mutation: Invalid column");

        // Pick three distinct vectors that are different from the "column" vector

        int[] ids = Enumerable.Range(0, vectors.ColumnCount).ToArray();  // random permutation of integers 0..ColumnCount
        _rnd.Shuffle(ids);

        // remove index "column" from "ids"
        int[] indices = new int[vectors.ColumnCount - 1];
        for (int i = 0, j = 0; i < ids.Length; i++)
            if (ids[i] != column)
                indices[j++] = ids[i];

        // Compute donor vector from the first three vectors
        // (need to be distinct from each other and from x)
        return vectors.Column(indices[1]) + f * (vectors.Column(indices[2]) - vectors.Column(indices[3]));
    }

    // Uses "createVector" function to obtain a vector, then checks if all vectors values
    // stay within a range [min, max]. If not, then it requests another vectors.
    // If the number of attempts exceed a limit, it returns a random vector
    private MatrixD KeepInRange(double min, double max, Func<MatrixD> createVector)
    {
        // We try to change vector values so that all stay in the range [min, max]
        // However, we may get scenarios when this is impossible or takes
        // too many trials to complete

        var result = new MatrixD();

        var maxTrialCount = CHECK_MUTATED_MAX_COUUNT;

        while (maxTrialCount > 0)
        {
            result = createVector();  // the callback create either a column or a row

            if (result.All(v => v >= min && v <= max))  // nice, all values are withing the range
                break;

            // some values are out of range, lets display them and try again

            _debugDisplay.WriteLine($"Rejected: {result}");

            maxTrialCount -= 1;
        }

        if (maxTrialCount == 0) // oh, we ran out of trials.. just generate some random numbers within the range
        {
            min = Math.Round(min);
            max = Math.Round(max);

            result = new MatrixD(result.RowCount, result.ColumnCount, (r, c) => _rnd.NextDouble(min, max));

            _debugDisplay.WriteLine($"Failed to create a vector within the limits. A random vector is generated instead.");
        }

        return result;
    }

    // Uses binomial method for combining components from donor and mutated vectors
    private static MatrixD Crossover(MatrixD donorVectors, MatrixD mutatedVectors, double cr)
    {
        // Generate random indices of donor vectors
        var randomIndices = donorVectors.Row(0).Select(_ => _rnd.Next(donorVectors.ColumnCount)).ToArray();

        // Random numbers in [0, 1] for each component of each mutated vector
        var crossoverProbabilities = new MatrixD(donorVectors.RowCount, donorVectors.ColumnCount, (r, c) => _rnd.NextDouble());

        // Combining mutated and donor vectors to get vectors for the next iteration

        var result = new MatrixD(donorVectors.RowCount, donorVectors.ColumnCount);

        for (int c = 0; c < donorVectors.ColumnCount; c++)
        {
            for (int r = 0; r < donorVectors.RowCount; r++)
            {
                if (crossoverProbabilities[r, c] <= cr || randomIndices[r] == c)
                    result[r, c] = mutatedVectors[r, c];
                else                                        // OLEG: extreamly rare if cr = 0.8, but
                    result[r, c] = donorVectors[r, c];      // happens in 10 - 30 % cases when cr = 0.5
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
                    var candidate = KeepInRange(min, max, () =>
                        vectors.Column(i) + new MatrixD(vectors.RowCount, 1, (r, c) =>
                            _rnd.NextDouble(-delta, delta)
                        )
                    );
                    vectors.ReplaceColumn(i, candidate);
                    _debugDisplay.WriteLine($"Validator: [{i}] '{prevVector}' >> '{candidate}'");
                }
            }
        }

        // If all variable values are the same..
        bool[] areAllSame = vectors.Column(0).Select(_ => true).ToArray();
        for (int c = 1; c < vectors.ColumnCount; c++)
            for (int r = 0; r < vectors.RowCount; r++)
                areAllSame[r] = areAllSame[r] && (vectors[r, c - 1] == vectors[r, c]);

        // ..then randomize those values a bit
        for (int r = 0; r < vectors.RowCount; r++)
        {
            if (areAllSame[r])
            {
                var variablePreviousValues = vectors.Row(r);
                var variableNewValues = KeepInRange(min, max, () =>
                    vectors.Row(r) + new MatrixD(1, vectors.ColumnCount, (r, c) =>
                        _rnd.NextDouble(-delta, delta)
                    )
                );
                vectors.ReplaceRow(r, variableNewValues);
                _debugDisplay.WriteLine($"Validator: '{variablePreviousValues}' >> '{variableNewValues}'");
            }
        }
    }

    // Check if some of the search-end criteria is satisfied, and return the best candidate if this is so
    private TestCandidate? GetBestCandidate(int iterationId)
    {
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
            // Return the final recipe
            var bestVector = _testedCandidates[_grandMinimaIndex].Vector;
            var bestValidVector = LimitValues(bestVector, VALUE_MIN, VALUE_MAX);

            PrintBestVectorInfo(bestVector, bestRecipeName);
            _debugDisplay.WriteLine("\nFinished");

            return new TestCandidate(bestRecipeName, bestValidVector, true, _lastDistance);
        }
        else
        {
            _debugDisplay.WriteLine("\nThe best vectors are:");
            PrintVectors(_bestVectors);
            return null;
        }
    }

    // Debug helpers

    private void PrintIterationResultInfo()
    {
        if (_iterationMinimaIndex >= 0)
        {
            var vector = _iterationVectors.Column(_iterationMinimaIndex);
            var formatArgs = new RequestFormatEventArgs(vector);
            RequestFormat?.Invoke(this, formatArgs);
            _debugDisplay.WriteLine($"IM: {_iterationMinima:F4} [{formatArgs.Result}]");
        }
    }

    private void PrintSearchResultInfo()
    {
        var vector = _testedCandidates[_grandMinimaIndex].Vector;
        var formatArgs = new RequestFormatEventArgs(vector);
        RequestFormat?.Invoke(this, formatArgs);
        _debugDisplay.WriteLine($"GM: {_grandMinima:F4} [{formatArgs.Result}]");
    }

    private void PrintIterationStartInfo()
    {
        var formatArgs = new RequestFormatEventArgs(_iterationVectors);
        RequestFormat?.Invoke(this, formatArgs);
        _debugDisplay.WriteLine($"\nVectors to test:\n{formatArgs.Result}");
    }

    private void PrintTrialStartInfo(int index, MatrixD vector)
    {
        var formatArgs = new RequestFormatEventArgs(vector);
        RequestFormat?.Invoke(this, formatArgs);
        _debugDisplay.Write($"[{index + 1}] {formatArgs.Result}");
    }

    private void PrintBestVectorInfo(MatrixD bestVector, string name)
    {
        var formatArgs = new RequestFormatEventArgs(bestVector);
        RequestFormat?.Invoke(this, formatArgs);
        _debugDisplay.WriteLine($"\n{name}:\n  {formatArgs.Result}\n  DIST = {_grandMinima:F4}");
    }

    void PrintVectors(MatrixD vectors)
    {
        var formatArgs = new RequestFormatEventArgs(vectors);
        RequestFormat?.Invoke(this, formatArgs);
        _debugDisplay.WriteLine(formatArgs.Result);
    }
}
