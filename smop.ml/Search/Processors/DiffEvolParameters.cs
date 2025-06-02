namespace Smop.ML.Search;

public class DiffEvolParameters
{
    public double DistanceThreshold { get; set; } = 0.1;
    public int MaxIterations { get; set; } = 10;
    public double CrossoverRate { get; set; } = 0.7;
    public double MutationFactor { get; set; } = 0.8;
    public int Decimals { get; set; } = 0;
    public Kernel Kernel { get; set; } = Kernel.Euclidean;
}
