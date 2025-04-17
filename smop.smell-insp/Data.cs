using Smop.Common;
using System.Collections.Generic;
using System.Linq;

namespace Smop.SmellInsp;

public enum Command
{
    FAN1,
    FAN2,
    FAN3,
    FAN0,
    GET_INFO
}

public class Data : IMeasurement
{
    public static int ResistantCount => 64;

    public float[] Resistances { get; }
    public float Temperature { get; }
    public float Humidity { get; }

    public Data(float[] resistances, float temperature, float humidity)
    {
        Resistances = resistances;
        Temperature = temperature;
        Humidity = humidity;
    }

    public static Data GetMean(IList<Data> samples)
    {
        var arrayLength = samples.First()?.Resistances.Length ?? ResistantCount;
        var resistances = new float[arrayLength];
        float temperature = 0;
        float humidity = 0;

        foreach (var sample in samples)
        {
            for (int i = 0; i < sample.Resistances.Length; i++)
            {
                resistances[i] += sample.Resistances[i];
            }
            temperature += sample.Temperature;
            humidity += sample.Humidity;
        }

        for (int i = 0; i < resistances.Length; i++)
        {
            resistances[i] /= samples.Count;
        }
        temperature /= samples.Count;
        humidity /= samples.Count;

        return new Data(resistances, temperature, humidity);
    }

    public FeatureData AsFeatures() => new(this);

    public override string ToString()
    {
        return string.Join(' ', Resistances.Select(r => $"{r:F2}")) + " " +
               Temperature.ToString("F2") + " " +
               Humidity.ToString("F2");
    }
}

public class FeatureData : IMeasurement
{
    public static int FeatureCount => 15;

    public float[] Features { get; }
    public float Temperature { get; }
    public float Humidity { get; }

    public FeatureData(float[] features, float temperature, float humidity)
    {
        Features = features;
        Temperature = temperature;
        Humidity = humidity;
    }

    public FeatureData(Data data)
    {
        var resistances = data.Resistances;

        Features = new float[]
            {
                (resistances[11] + resistances[12] + resistances[13]) / 3,
                (resistances[8] + resistances[9] + resistances[10]) / 3,
                (resistances[5] + resistances[6] + resistances[7]) / 3,
                (resistances[43] + resistances[44] + resistances[45]) / 3,
                (resistances[27] + resistances[28] + resistances[29]) / 3,
                (resistances[24] + resistances[25] + resistances[26]) / 3,
                (resistances[40] + resistances[41] + resistances[42]) / 3,
                (resistances[59] + resistances[60] + resistances[61]) / 3,
                (resistances[21] + resistances[22] + resistances[23]) / 3,
                (resistances[37] + resistances[38] + resistances[39]) / 3,
                (resistances[2] + resistances[3] + resistances[4]) / 3,
                (resistances[56] + resistances[57] + resistances[58]) / 3,
                (resistances[53] + resistances[54] + resistances[55]) / 3,
                (resistances[34] + resistances[35] + resistances[36]) / 3,
                (resistances[50] + resistances[51] + resistances[52]) / 3,
            };
        Temperature = data.Temperature;
        Humidity = data.Humidity;
    }

    public static FeatureData GetMean(IList<FeatureData> samples)
    {
        var arrayLength = samples.First()?.Features.Length ?? FeatureCount;
        var features = new float[arrayLength];
        float temperature = 0;
        float humidity = 0;

        foreach (var sample in samples)
        {
            for (int i = 0; i < sample.Features.Length; i++)
            {
                features[i] += sample.Features[i];
            }
            temperature += sample.Temperature;
            humidity += sample.Humidity;
        }

        for (int i = 0; i < features.Length; i++)
        {
            features[i] /= samples.Count;
        }
        temperature /= samples.Count;
        humidity /= samples.Count;

        return new FeatureData(features, temperature, humidity);
    }

    public override string ToString()
    {
        return string.Join(' ', Features.Select(f => $"{f:F2}")) + " " +
               Temperature.ToString("F2") + " " +
               Humidity.ToString("F2");
    }
}

public record class DeviceInfo(string Version, string Address);
