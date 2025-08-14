using System.Collections.Generic;
using System.Text.Json;

namespace Smop.IonVision.Defs;

public record class RangeAvg(
    double Avg,
    double Min,
    double Max
);
public record class Detector(
    RangeAvg Temperature,
    RangeAvg Pressure,
    RangeAvg Humidity
);
public record class FlowDetector(
    RangeAvg Flow,
    RangeAvg Temperature,
    RangeAvg Pressure,
    RangeAvg Humidity,
    RangeAvg PumpPWM
) : Detector(Temperature, Pressure, Humidity);
public record class SystemData(
    ErrorRegister ErrorRegister,
    RangeAvg FetTemperature,
    FlowDetector Sample,
    FlowDetector Sensor,
    Detector Ambient
);
public record class MeasurementData(
    bool DataValid,
    int DataPoints,
    float[] IntensityTop,
    float[] IntensityBottom,
    float[] Usv,
    float[] Ucv,
    float[] Vb,
    float[] PP,
    float[] PW,
    short[] NForSampleAverages
);
public record class ScanResult(
    string Id,
    string? Measurer,
    string StartTime,
    string FinishTime,
    string Parameters,
    string Project,
    object Comments,
    int FormatVersion,
    SystemData SystemData,
    MeasurementData MeasurementData
) : Common.IMeasurement
{
    public string Info
    {
        get
        {
            List<string> lines = new()
            {
                $"Timestamp: {StartTime.Replace('T', ' ').Replace('Z', ' ')}"
            };

            float uc = MeasurementData.Usv[0];
            int cols = 1;
            while (cols < MeasurementData.DataPoints && uc == MeasurementData.Usv[cols])
                cols += 1;
            int rows = MeasurementData.DataPoints / cols;
            lines.Add($"Size: {rows} x {cols}");

            if (Comments != null)
            {
                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                var comments = JsonSerializer.Serialize(Comments, options);
                if (comments != "{}")
                    lines.Add($"Comments: \n{comments}");
            }

            return string.Join("\n", lines);
        }
    }
}
