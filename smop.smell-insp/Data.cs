using Smop.Common;
using System.Collections.Generic;

namespace Smop.SmellInsp;

public enum Command
{
    FAN1,
    FAN2,
    FAN3,
    FAN0,
    GET_INFO
}

public record class Data(float[] Resistances, float Temperature, float Humidity) : IMeasurement
{
    public static int ResistantCount => 64;

    public static Data GetMean(IList<Data> samples)
    {
        var resistances = new float[ResistantCount];
        float temperature = 0;
        float humidity = 0;

        foreach (var sample in samples)
        {
            for (int i = 0; i < ResistantCount; i++)
            {
                resistances[i] += sample.Resistances[i];
            }
            temperature += sample.Temperature;
            humidity += sample.Humidity;
        }

        for (int i = 0; i < ResistantCount; i++)
        {
            resistances[i] /= samples.Count;
        }
        temperature /= samples.Count;
        humidity /= samples.Count;

        return new(resistances, temperature, humidity);
    }
}

public record class DeviceInfo(string Version, string Address);
