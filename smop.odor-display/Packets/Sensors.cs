using System.Collections.Generic;
using System.Linq;

namespace Smop.OdorDisplay.Packets.Sensor;

public abstract class Value
{
    public Device.Sensor Sensor { get; }
    public byte[] Data { get; }
    public abstract string[] ValueNames { get; }
    public abstract float[] Values { get; }
    public static Value? Create(Device.Sensor sensor, byte[] data, ref int offset)
    {
        int dataLength = sensor switch
        {
            Device.Sensor.PID or Device.Sensor.ChassisThermometer or Device.Sensor.OdorSourceThermometer or Device.Sensor.GeneralPurposeThermometer
                => 4,
            Device.Sensor.BeadThermistor or Device.Sensor.OutputAirHumiditySensor or Device.Sensor.InputAirHumiditySensor or Device.Sensor.PressureSensor
                => 8,
            Device.Sensor.OdorantFlowSensor or Device.Sensor.DilutionAirFlowSensor
                => 12,
            Device.Sensor.OdorantValveSensor or Device.Sensor.OutputValveSensor
                => 1,
            _ => throw new System.Exception($"Unknown sensor '{sensor}'")
        };

        if (offset + dataLength > data.Length)
        {
            return null;
        }

        int startIndex = offset;
        int endIndex = offset + dataLength;
        var sensorData = data[startIndex..endIndex];

        offset += dataLength;

        return sensor switch
        {
            Device.Sensor.PID
                => new PID(sensorData),
            Device.Sensor.ChassisThermometer or Device.Sensor.OdorSourceThermometer or Device.Sensor.GeneralPurposeThermometer
                => new Thermometer(sensor, sensorData),
            Device.Sensor.BeadThermistor
                => new BeadThermistor(sensorData),
            Device.Sensor.OutputAirHumiditySensor or Device.Sensor.InputAirHumiditySensor
                => new Humidity(sensor, sensorData),
            Device.Sensor.PressureSensor
                => new Pressure(sensorData),
            Device.Sensor.OdorantFlowSensor or Device.Sensor.DilutionAirFlowSensor
                => new Gas(sensor, sensorData),
            Device.Sensor.OdorantValveSensor or Device.Sensor.OutputValveSensor
                => new Valve(sensor, sensorData),
            _ => throw new System.Exception($"Unknown sensor '{sensor}'")
        };
    }


    public virtual byte[] ToArray()
    {
        List<byte> result = new() { (byte)Sensor };
        result.AddRange(Data);
        return result.ToArray();
    }

    // Internal

    protected const int SENSOR_NAME_LENGTH = 25;

    protected Value(Device.Sensor sensor, byte[] data)
    {
        Sensor = sensor;
        Data = data;
    }
}

public class PID : Value
{
    public float Volts { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/Volts" };
    public override float[] Values => new float[] { Volts };
    public PID(byte[] data) : base(Device.Sensor.PID, data)
    {
        Volts = FourBytes.ToFloat(data);
    }
    public PID(float value) : base(Device.Sensor.PID, FourBytes.ToArray(value))
    {
        Volts = value;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Volts:F3} V";
}
public class BeadThermistor : Value
{
    public float Ohms { get; }
    public float Volts { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/Ohms", $"{Sensor}/Volts" };
    public override float[] Values => new float[] { Ohms, Volts };
    public BeadThermistor(byte[] data) : base(Device.Sensor.BeadThermistor, data)
    {
        Ohms = FourBytes.ToFloat(data[..4]);
        Volts = FourBytes.ToFloat(data[4..]);
    }
    public BeadThermistor(float ohms, float volts) : base(Device.Sensor.BeadThermistor, new FourBytes[] {
            new(ohms),
            new(volts)
        }.SelectMany(v => v.ToArray()).ToArray())
    {
        Ohms = ohms;
        Volts = volts;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Ohms:F1} Ohm, {Volts:F3} V";
}
public class Thermometer : Value
{
    public float Celsius { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/Celsius" };
    public override float[] Values => new float[] { Celsius };
    public Thermometer(Device.Sensor sensor, byte[] data) : base(sensor, data)
    {
        Celsius = FourBytes.ToFloat(data);
    }
    public Thermometer(Device.Sensor sensor, float value) : base(sensor, FourBytes.ToArray(value))
    {
        Celsius = value;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Celsius:F2}°C";
}
public class Humidity : Value
{
    public float Percent { get; }
    public float Celsius { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/Percent", $"{Sensor}/Celsius" };
    public override float[] Values => new float[] { Percent, Celsius };
    public Humidity(Device.Sensor sensor, byte[] data) : base(sensor, data)
    {
        Percent = FourBytes.ToFloat(data[..4]);
        Celsius = FourBytes.ToFloat(data[4..]);
    }
    public Humidity(Device.Sensor sensor, float percent, float celsius) : base(sensor, new FourBytes[] {
            new(percent),
            new(celsius)
        }.SelectMany(v => v.ToArray()).ToArray())
    {
        Percent = percent;
        Celsius = celsius;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Percent:F1}%, {Celsius:F1}°C";
}
public class Pressure : Value
{
    public float Millibars { get; }
    public float Celsius { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/Millibars", $"{Sensor}/Celsius" };
    public override float[] Values => new float[] { Millibars, Celsius };
    public Pressure(byte[] data) : base(Device.Sensor.PressureSensor, data)
    {
        Millibars = FourBytes.ToFloat(data[..4]);
        Celsius = FourBytes.ToFloat(data[4..]);
    }
    public Pressure(float millibars, float celsius) : base(Device.Sensor.PressureSensor, new FourBytes[] {
            new(millibars),
            new(celsius)
        }.SelectMany(v => v.ToArray()).ToArray())
    {
        Millibars = millibars;
        Celsius = celsius;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Millibars:F1} mB, {Celsius:F1}°C";
}
public class Gas : Value
{
    public float SLPM { get; }
    public float Celsius { get; }
    public float Millibars { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/SLPM", $"{Sensor}/Millibars", $"{Sensor}/Celsius" };
    public override float[] Values => new float[] { SLPM, Millibars, Celsius };
    public Gas(Device.Sensor sensor, byte[] data) : base(sensor, data)
    {
        SLPM = FourBytes.ToFloat(data[..4]);
        Celsius = FourBytes.ToFloat(data[4..8]);
        Millibars = FourBytes.ToFloat(data[8..]);
    }
    public Gas(Device.Sensor sensor, float slpm, float celsius, float millibars) : base(sensor, new FourBytes[] {
            new(slpm),
            new(celsius),
            new(millibars),
        }.SelectMany(v => v.ToArray()).ToArray())
    {
        SLPM = slpm;
        Celsius = celsius;
        Millibars = millibars;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {SLPM:F4} L/min, {Millibars:F1} mB, {Celsius:F1}°C";
}
public class Valve : Value
{
    public bool Opened { get; }
    public override string[] ValueNames => new string[] { $"{Sensor}/Opened" };
    public override float[] Values => new float[] { Opened ? 1 : 0 };
    public Valve(Device.Sensor sensor, byte[] data) : base(sensor, data)
    {
        Opened = data[0] != 0;
    }
    public Valve(Device.Sensor sensor, bool opened) : base(sensor, new byte[] { (byte)(opened ? 1 : 0) })
    {
        Opened = opened;
    }
    public override string ToString() => $"{Sensor.AsName(),-SENSOR_NAME_LENGTH} = {Opened.AsFlag()}";
}
