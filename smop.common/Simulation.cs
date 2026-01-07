namespace Smop.Common;

public class Simulation
{
    public static float DmsWaterGain => 60;

    public static float[] DmsGains { get; set; } = new float[OdorPrinter.MaxOdorCount] { 100, 90, 40, 30, 20 };

    public static float[] SntGains { get; set; } = new float[OdorPrinter.MaxOdorCount] { 8, 6, 5, 0, 0 };
}
