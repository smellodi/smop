namespace Smop.ML.DiffEvol;

public class Parameters
{
    public double CrossoverRate { get; set; } = 0.7;
    public double MutationFactor { get; set; } = 0.8;
    public int Decimals { get; set; } = 0;
    public Kernel Kernel { get; set; } = Kernel.Euclidean;
}
