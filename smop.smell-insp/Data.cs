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

        return new Data(resistances, temperature, humidity).AsFeatures();
    }

    public Data AsFeatures()
    {
        return new Data(new float[]
            {
                (Resistances[11] + Resistances[12] + Resistances[13]) / 3,
                (Resistances[8] + Resistances[9] + Resistances[10]) / 3,
                (Resistances[5] + Resistances[6] + Resistances[7]) / 3,
                (Resistances[43] + Resistances[44] + Resistances[45]) / 3,
                (Resistances[27] + Resistances[28] + Resistances[29]) / 3,
                (Resistances[24] + Resistances[25] + Resistances[26]) / 3,
                (Resistances[40] + Resistances[41] + Resistances[42]) / 3,
                (Resistances[59] + Resistances[60] + Resistances[61]) / 3,
                (Resistances[21] + Resistances[22] + Resistances[23]) / 3,
                (Resistances[37] + Resistances[38] + Resistances[39]) / 3,
                (Resistances[2] + Resistances[3] + Resistances[4]) / 3,
                (Resistances[56] + Resistances[57] + Resistances[58]) / 3,
                (Resistances[53] + Resistances[54] + Resistances[55]) / 3,
                (Resistances[34] + Resistances[35] + Resistances[36]) / 3,
                (Resistances[50] + Resistances[51] + Resistances[52]) / 3,
            }, Temperature, Humidity
        );
    }
}

public record class DeviceInfo(string Version, string Address);
