using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using Smop.MainApp.Controls;
using System;
using System.Threading.Tasks;
using Smop.MainApp.Reproducer;

namespace Smop.MainApp.Indicators;

internal static class Factory
{
    public record class SmellInspChannel(string Type, string Units, int Count);
    public static SmellInspChannel[] SmellInspChannels => new SmellInspChannel[] {
        new SmellInspChannel("Resistor", "Ohms", 64),
        new SmellInspChannel("Temperature", "°C", 0),
        new SmellInspChannel("Humidity", "%", 0)
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

    public static string GetSourceId(Device.ID deviceID, Device.Capability cap) => $"od/{deviceID}/{cap}";
    public static string GetSourceId(string measure) => $"snt/{measure}";

    public static void ApplyGasProps(ChannelIndicator indicator, Device.ID gasID, string gasName)
    {
        if (indicator.OdorID == gasID)
        {
            var p = indicator.Title.Split('\n');
            if (string.IsNullOrEmpty(gasName))
            {
                gasName = gasID.ToString();
            }
            indicator.Title = gasName + '\n' + string.Join('\n', p[1..]);
        }
    }

    // Internal

    private static async Task CreateIndicators(Devices? devices, Action<ChannelIndicator> callback)
    {
        if (devices == null)
        {
            return;
        }

        await CreateIndicators(Device.ID.Base, callback);
        await CreateIndicators(Device.ID.DilutionAir, callback);

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
        foreach (var capId in Enum.GetValues(typeof(Device.Capability)))
        {
            var cap = (Device.Capability)capId;
            if (caps.Has(cap))
            {
                var indicator = CreateIndicator(deviceID, cap);
                if (indicator != null)
                {
                    callback(indicator);
                }
            }
        }
    }

    private static ChannelIndicator? CreateIndicator(Device.ID deviceID, Device.Capability cap)
    {
        var capName = cap switch
        {
            Device.Capability.PID => "PID",
            Device.Capability.BeadThermistor => "Bead therm.",
            Device.Capability.ChassisThermometer => "Chassis therm.",
            Device.Capability.OdorSourceThermometer => "Source therm.",
            Device.Capability.GeneralPurposeThermometer => "Thermometer",
            Device.Capability.OutputAirHumiditySensor => "Input humid.",
            Device.Capability.InputAirHumiditySensor => "Output humid.",
            Device.Capability.PressureSensor => "Pressure",
            Device.Capability.OdorantFlowSensor => "Flow",
            Device.Capability.DilutionAirFlowSensor => "Dil. flow",
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
            OdorID = deviceID,
            Title = $"{deviceID}\n{capName}",
            Units = units,
            Precision = precision,
            Value = 0,
            Source = GetSourceId(deviceID, cap),
        };
    }
}
