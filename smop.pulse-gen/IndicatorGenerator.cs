using Smop.OdorDisplay.Packets;
using System;
using System.Threading.Tasks;
using Smop.PulseGen.Controls;
using static Smop.OdorDisplay.Device;

namespace Smop.PulseGen
{
    internal class IndicatorGenerator
    {
        public IndicatorGenerator() { }

        public async Task OdorDisplay(Action<ChannelIndicator> callback)
        {
            var queryDevices = new QueryDevices();
            var queryResult = _odorDisplay.Request(queryDevices, out Ack? ack, out Response? response);
            if (queryResult.Error == Smop.OdorDisplay.Error.Success)
            {
                await CreateIndicators(response as Devices, callback);
            }
        }

        public void SmellInsp(Action<ChannelIndicator> callback)
        {
            ChannelIndicator? indicator;

            indicator = CreateIndicator("Resistor", 64);
            if (indicator != null)
            {
                callback(indicator);
            }

            indicator = CreateIndicator("Temperature");
            if (indicator != null)
            {
                callback(indicator);
            }

            indicator = CreateIndicator("Humidity");
            if (indicator != null)
            {
                callback(indicator);
            }
        }

        public static string GetSourceId(ID deviceID, Capability cap) => $"od/{deviceID}/{cap}";
        public static string GetSourceId(string measure) => $"snt/{measure}";

        // Internal

        readonly OdorDisplay.CommPort _odorDisplay = Smop.OdorDisplay.CommPort.Instance;

        private async Task CreateIndicators(Devices? devices, Action<ChannelIndicator> callback)
        {
            if (devices == null)
            {
                return;
            }

            await CreateIndicators(ID.Base, callback);
            await CreateIndicators(ID.DilutionAir, callback);

            for (int i = 0; i < Devices.MaxOdorModuleCount; i++)
            {
                if (devices.HasOdorModule(i))
                {
                    /// IMPORTANT! this depends on <see cref="OdorDisplay.Devices.ID"/>
                    await CreateIndicators((ID)(i+1), callback);
                }
            }
        }

        private async Task CreateIndicators(ID deviceID, Action<ChannelIndicator> callback)
        {
            // Because of the frequent request to the device, lets have some pause
            await Task.Delay(50);

            // Read the capabilities
            var query = new QueryCapabilities(deviceID);
            var result = _odorDisplay.Request(query, out Ack? _, out Response? response);
            if (result.Error != Smop.OdorDisplay.Error.Success || response is not Capabilities caps)
            {
                return;
            }

            // Create an indicator for each capability
            foreach (var capId in Enum.GetValues(typeof(Capability)))
            {
                var cap = (Capability)capId;
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

        private static ChannelIndicator? CreateIndicator(ID deviceID, Capability cap)
        {
            var capName = cap switch
            {
                Capability.PID => "PID",
                Capability.BeadThermistor => "Bead therm.",
                Capability.ChassisThermometer => "Chassis therm.",
                Capability.OdorSourceThermometer => "Source therm.",
                Capability.GeneralPurposeThermometer => "Thermometer",
                Capability.InputAirHumiditySensor => "Input humd.",
                Capability.OutputAirHumiditySensor => "Output humd.",
                Capability.PressureSensor => "Pressure",
                Capability.OdorantFlowSensor => "Flow",
                Capability.DilutionAirFlowSensor => "Dil. flow",
                Capability.OdorantValveSensor => "Valve",
                Capability.OutputValveSensor => "Output valve",
                _ => null
            };
            var units = cap switch
            {
                Capability.PID => "mV",
                Capability.BeadThermistor or Capability.ChassisThermometer or Capability.OdorSourceThermometer or Capability.GeneralPurposeThermometer => "°C",
                Capability.InputAirHumiditySensor or Capability.OutputAirHumiditySensor => "%",
                Capability.PressureSensor => "mBar",
                Capability.OdorantFlowSensor or Capability.DilutionAirFlowSensor => "l/min",
                Capability.OdorantValveSensor or Capability.OutputValveSensor => null,
                _ => null
            };

            var precision = cap switch
            {
                Capability.PID => 2,
                Capability.BeadThermistor or Capability.ChassisThermometer or Capability.OdorSourceThermometer or Capability.GeneralPurposeThermometer => 1,
                Capability.InputAirHumiditySensor or Capability.OutputAirHumiditySensor => 1,
                Capability.PressureSensor => 1,
                Capability.OdorantFlowSensor or Capability.DilutionAirFlowSensor => 2,
                Capability.OdorantValveSensor or Capability.OutputValveSensor => 0,
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

        private static ChannelIndicator? CreateIndicator(string measure, int channelCount = 0)
        {
            var units = measure.ToLower() switch
            {
                "resistor" => "Ohms",
                "temperature" => "°C",
                "humidity" => "%",
                _ => null,
            };

            return units == null ? null : new ChannelIndicator()
            {
                Title = measure,
                Units = units,
                Precision = 2,
                Value = 0,
                Source = GetSourceId(measure.ToLower()),
                ChannelCount = channelCount,
            };
        }
    }
}
