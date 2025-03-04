using Smop.MainApp.Controls;
using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System;
using System.Threading.Tasks;

namespace Smop.MainApp.Controllers;

internal static class IndicatorFactory
{
    public record class SmellInspChannel(string Type, string Units, int Count);
    public static SmellInspChannel[] SmellInspChannels => new SmellInspChannel[] {
        new("Resistor", "Ohms", 64),
        new("Temperature", "°C", 0),
        new("Humidity", "%", 0)
    };

    public static async Task OdorDisplay(Action<ChannelIndicator> callback)
    {
        var queryDevices = new QueryDevices();
        var queryResult = CommPort.Instance.Request(queryDevices, out Ack? ack, out Response? response);
        if (queryResult.Error == Comm.Error.Success)
        {
            await CreateIndicators(response as Devices, callback);
        }
    }

    public static async Task SmellInsp(Action<ChannelIndicator> callback)
    {
        foreach (var channel in SmellInspChannels)
        {
            await Task.Delay(50);
            var indicator = new ChannelIndicator()
            {
                Title = channel.Type,
                Units = channel.Units,
                Precision = 1,
                Value = 0,
                Source = GetSourceId(channel.Type),
                ChannelCount = channel.Count,
            };

            callback(indicator);
        }
    }

    public static string GetSourceId(Device.ID deviceID, Device.Capability cap, int sensorSubID) => $"od/{deviceID}/{cap}/{sensorSubID}";
    public static string GetSourceId(string measure) => $"snt/{measure}";

    public static void ApplyChannelProps(ChannelIndicator indicator, Device.ID channelID, string odorName)
    {
        if (indicator.ChannelID == channelID)
        {
            var p = indicator.Title.Split(SEPARATOR);
            if (string.IsNullOrEmpty(odorName))
            {
                odorName = channelID.ToString();
            }
            indicator.Title = odorName + SEPARATOR + string.Join(SEPARATOR, p[1..]);
        }
    }

    // Internal

    const char SEPARATOR = '\n';

    private static async Task CreateIndicators(Devices? devices, Action<ChannelIndicator> callback)
    {
        if (devices == null)
        {
            return;
        }

        if (devices.HasBaseModule)
        {
            await CreateIndicators(Device.ID.Base, callback);
        }
        if (devices.HasDilutionModule)
        {
            await CreateIndicators(Device.ID.DilutionAir, callback);
        }

        for (int i = 0; i < Devices.MaxOdorModuleCount; i++)
        {
            if (devices.HasOdorModule(i))
            {
                /// IMPORTANT! this depends on <see cref="OdorDisplay.Devices.ID"/>
                await CreateIndicators((Device.ID)(i + 1), callback);
            }
        }
    }

    private static async Task CreateIndicators(Device.ID deviceID, Action<ChannelIndicator> callback)
    {
        // Because of the frequent request to the device, lets have some pause
        await Task.Delay(50);

        // Read the capabilities
        var query = new QueryCapabilities(deviceID);
        var result = CommPort.Instance.Request(query, out Ack? _, out Response? response);
        if (result.Error != Comm.Error.Success || response is not Capabilities caps)
        {
            return;
        }

        // Create an indicator for each capability
        int index = 0;
        foreach (var capId in Enum.GetValues(typeof(Device.Capability)))
        {
            var cap = (Device.Capability)capId;
            if (caps.Has(cap))
            {
                var dummySensorValue = CreateDummySensor(cap);
                if (dummySensorValue == null)
                    continue;

                for (int subID = 0; subID < dummySensorValue.ValueNames.Length; subID++)
                {
                    var indicator = CreateIndicator(deviceID, cap, subID, index++ > 0);
                    if (indicator != null)
                    {
                        callback(indicator);
                    }
                }
            }
        }
    }

    private static ChannelIndicator? CreateIndicator(Device.ID deviceID, Device.Capability cap, int sensorSubID, bool isSameDeviceAsPrevious)
    {
        var mfc1Name = deviceID switch
        {
            Device.ID.Base => "Humid. flow",
            Device.ID.DilutionAir => "Odor. flow",
            _ => "Flow"
        };
        var mfc2Name = deviceID switch
        {
            Device.ID.Base => "Dry flow",
            Device.ID.DilutionAir => "Clean flow",
            _ => "Flow"
        };

        (string ? name, string? subname, string? units, int precision) = (cap, sensorSubID) switch
        {
            (Device.Capability.PID, 0) => ("PID", null, "mV", 1),
            (Device.Capability.BeadThermistor, 0) => ("Bead therm.", null, "°C", 1),
            (Device.Capability.BeadThermistor, 1) => ("Bead therm.", "U", "V", 1),
            (Device.Capability.ChassisThermometer, 0) => ("Chassis therm.", null, "°C", 1),
            (Device.Capability.OdorSourceThermometer, 0) => ("Source therm.", null, "°C", 1),
            (Device.Capability.GeneralPurposeThermometer, 0) => ("Thermometer", null, "°C", 1),
            (Device.Capability.OutputAirHumiditySensor, 0) => ("Output humid.", null, "%", 1),
            (Device.Capability.OutputAirHumiditySensor, 1) => ("Output humid.", "T", "°C", 1),
            (Device.Capability.InputAirHumiditySensor, 0) => ("Input humid.", null, "%", 1),
            (Device.Capability.InputAirHumiditySensor, 1) => ("Input humid.", "T", "°C", 1),
            (Device.Capability.PressureSensor, 0) => ("Pressure", null, "mBar", 1),
            (Device.Capability.PressureSensor, 1) => ("Pressure", "T", "°C", 1),
            (Device.Capability.OdorantFlowSensor, 0) => (mfc1Name, null, 
                deviceID == Device.ID.Base ? "l/min" : "sccm", 1),
            (Device.Capability.OdorantFlowSensor, 1) => (mfc1Name, "P", "mBar", 1),
            (Device.Capability.OdorantFlowSensor, 2) => (mfc1Name, "T", "°C", 1),
            (Device.Capability.DilutionAirFlowSensor, 0) => (mfc2Name, null, "l/min", 1),
            (Device.Capability.DilutionAirFlowSensor, 1) => (mfc2Name, "P", "mBar", 1),
            (Device.Capability.DilutionAirFlowSensor, 2) => (mfc2Name, "T", "°C", 1),
            (Device.Capability.OdorantValveSensor, 0) => ("Valve", null, null, 0),
            (Device.Capability.OutputValveSensor, 0) => ("Output valve", null, null, 0),
            _ => (null, null, null, 0)
        };

        if (!string.IsNullOrEmpty(subname))
            name = $"{name} {subname}";

        return name == null || units == null ? null : new ChannelIndicator()
        {
            ChannelID = deviceID,
            Title = $"{deviceID}{SEPARATOR}{name}",
            Units = units,
            Precision = precision,
            Value = 0,
            Source = GetSourceId(deviceID, cap, sensorSubID),
            HasLeftBorder = !isSameDeviceAsPrevious,
        };

        /*
        var capName = cap switch
        {
            Device.Capability.PID => "PID",
            Device.Capability.BeadThermistor => "Bead therm.",
            Device.Capability.ChassisThermometer => "Chassis therm.",
            Device.Capability.OdorSourceThermometer => "Source therm.",
            Device.Capability.GeneralPurposeThermometer => "Thermometer",
            Device.Capability.OutputAirHumiditySensor => "Output humid.",
            Device.Capability.InputAirHumiditySensor => "Input humid.",
            Device.Capability.PressureSensor => "Pressure",
            Device.Capability.OdorantFlowSensor => deviceID == Device.ID.Base ? "Humid. flow" : "Flow",
            Device.Capability.DilutionAirFlowSensor => "Dilut. flow",
            Device.Capability.OdorantValveSensor => "Valve",
            Device.Capability.OutputValveSensor => "Output valve",
            _ => null
        };
        var units = cap switch
        {
            Device.Capability.PID => "mV",
            Device.Capability.BeadThermistor or
                Device.Capability.ChassisThermometer or
                Device.Capability.OdorSourceThermometer or
                Device.Capability.GeneralPurposeThermometer => "°C",
            Device.Capability.OutputAirHumiditySensor or
                Device.Capability.InputAirHumiditySensor => "%",
            Device.Capability.PressureSensor => "mBar",
            Device.Capability.OdorantFlowSensor or
                Device.Capability.DilutionAirFlowSensor => deviceID == Device.ID.Base ? "l/min" : "sccm",
            Device.Capability.OdorantValveSensor or
                Device.Capability.OutputValveSensor => null,
            _ => null
        };

        var precision = cap switch
        {
            Device.Capability.PID => 1,
            Device.Capability.BeadThermistor or
                Device.Capability.ChassisThermometer or
                Device.Capability.OdorSourceThermometer or
                Device.Capability.GeneralPurposeThermometer => 1,
            Device.Capability.OutputAirHumiditySensor or
                Device.Capability.InputAirHumiditySensor => 1,
            Device.Capability.PressureSensor => 1,
            Device.Capability.OdorantFlowSensor or
                Device.Capability.DilutionAirFlowSensor => 1,
            Device.Capability.OdorantValveSensor or
                Device.Capability.OutputValveSensor => 0,
            _ => 0
        };

        return units == null ? null : new ChannelIndicator()
        {
            ChannelID = deviceID,
            Title = $"{deviceID}{SEPARATOR}{capName}",
            Units = units,
            Precision = precision,
            Value = 0,
            Source = GetSourceId(deviceID, cap),
            HasLeftBorder = !isSameDeviceAsPrevious,
        };*/
    }

    private static OdorDisplay.Packets.Sensor.Value? CreateDummySensor(Device.Capability cap) => cap switch
    {
        Device.Capability.PID => new OdorDisplay.Packets.Sensor.PID(0),
        Device.Capability.BeadThermistor => new OdorDisplay.Packets.Sensor.BeadThermistor(0, 0),
        Device.Capability.ChassisThermometer => new OdorDisplay.Packets.Sensor.Thermometer(Device.Sensor.ChassisThermometer, 0),
        Device.Capability.OdorSourceThermometer => new OdorDisplay.Packets.Sensor.Thermometer(Device.Sensor.OdorSourceThermometer, 0),
        Device.Capability.GeneralPurposeThermometer => new OdorDisplay.Packets.Sensor.Thermometer(Device.Sensor.GeneralPurposeThermometer, 0),
        Device.Capability.OutputAirHumiditySensor => new OdorDisplay.Packets.Sensor.Humidity(Device.Sensor.OutputAirHumiditySensor, 0, 0),
        Device.Capability.InputAirHumiditySensor => new OdorDisplay.Packets.Sensor.Humidity(Device.Sensor.InputAirHumiditySensor, 0, 0),
        Device.Capability.PressureSensor => new OdorDisplay.Packets.Sensor.Pressure(0, 0),
        Device.Capability.OdorantFlowSensor => new OdorDisplay.Packets.Sensor.Gas(Device.Sensor.OdorantFlowSensor, 0, 0, 0),
        Device.Capability.DilutionAirFlowSensor => new OdorDisplay.Packets.Sensor.Gas(Device.Sensor.DilutionAirFlowSensor, 0, 0, 0),
        Device.Capability.OdorantValveSensor => new OdorDisplay.Packets.Sensor.Valve(Device.Sensor.OdorantValveSensor, false),
        Device.Capability.OutputValveSensor => new OdorDisplay.Packets.Sensor.Valve(Device.Sensor.OutputValveSensor, false),
        _ => null
    };
}
