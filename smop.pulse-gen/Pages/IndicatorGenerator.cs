using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System;
using System.Threading.Tasks;
using Smop.PulseGen.Controls;

namespace Smop.PulseGen.Pages
{
    internal static class IndicatorGenerator
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
            if (queryResult.Error == Error.Success)
            {
                await CreateIndicators(response as Devices, callback);
            }
        }

        public static void SmellInsp(Action<ChannelIndicator> callback)
        {
            foreach (var channel in SmellInspChannels)
            {
                var indicator = new ChannelIndicator()
                {
                    Title = channel.Type,
                    Units = channel.Units,
                    Precision = 2,
                    Value = 0,
                    Source = GetSourceId(channel.Type),
                    ChannelCount = channel.Count,
                };

                if (indicator != null)
                {
                    callback(indicator);
                }
            }
        }

        public static string GetSourceId(Device.ID deviceID, Device.Capability cap) => $"od/{deviceID}/{cap}";
        public static string GetSourceId(string measure) => $"snt/{measure}";

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
            if (result.Error != Error.Success || response is not Capabilities caps)
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
                Device.Capability.InputAirHumiditySensor => "Input humd.",
                Device.Capability.OutputAirHumiditySensor => "Output humd.",
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
                Device.Capability.InputAirHumiditySensor or
                    Device.Capability.OutputAirHumiditySensor => "%",
                Device.Capability.PressureSensor => "mBar",
                Device.Capability.OdorantFlowSensor or
                    Device.Capability.DilutionAirFlowSensor => deviceID == Device.ID.Base ? "l/min" : "ccm",
                Device.Capability.OdorantValveSensor or
                    Device.Capability.OutputValveSensor => null,
                _ => null
            };

            var precision = cap switch
            {
                Device.Capability.PID => 2,
                Device.Capability.BeadThermistor or
                    Device.Capability.ChassisThermometer or
                    Device.Capability.OdorSourceThermometer or
                    Device.Capability.GeneralPurposeThermometer => 1,
                Device.Capability.InputAirHumiditySensor or
                    Device.Capability.OutputAirHumiditySensor => 1,
                Device.Capability.PressureSensor => 1,
                Device.Capability.OdorantFlowSensor or
                    Device.Capability.DilutionAirFlowSensor => 1,
                Device.Capability.OdorantValveSensor or
                    Device.Capability.OutputValveSensor => 0,
                _ => 0
            };

            return units == null ? null : new ChannelIndicator()
            {
                Title = $"{deviceID}\n{capName}",
                Units = units,
                Precision = precision,
                Value = 0,
                Source = GetSourceId(deviceID, cap),
            };
        }
    }
}
