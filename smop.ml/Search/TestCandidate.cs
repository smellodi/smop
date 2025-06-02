using MatrixD = Smop.ML.Search.Matrix<double>;

namespace Smop.ML.Search;

internal record class TestCandidate(
    string Name,
    MatrixD Vector,
    bool IsFinal,
    double Distance);

