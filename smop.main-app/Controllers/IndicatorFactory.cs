﻿using Smop.MainApp.Controls;
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

    public static string GetSourceId(Device.ID deviceID, Device.Capability cap) => $"od/{deviceID}/{cap}";
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
                var indicator = CreateIndicator(deviceID, cap, index++ > 0);
                if (indicator != null)
                {
                    callback(indicator);
                }
            }
        }
    }

    private static ChannelIndicator? CreateIndicator(Device.ID deviceID, Device.Capability cap, bool isSameDeviceAsPrevious)
    {
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
        };
    }
}
