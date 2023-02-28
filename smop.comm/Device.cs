﻿namespace SMOP.Comm
{
    public class Device
    {
        public enum ID
        {
            Base = 0,
            Odor1 = 1,
            Odor2 = 2,
            Odor3 = 3,
            Odor4 = 4,
            Odor5 = 5,
            Odor6 = 6,
            Odor7 = 7,
            Odor8 = 8,
            Odor9 = 9,
            DiluionAir = 10,
        }

        public enum Capabality
        {
            // Sensors

            /// <summary>
            /// Base module only has it
            /// </summary>
            PID = 0,
            /// <summary>
            /// Dilution module only has it
            /// </summary>
            BeadThermistor = 1,
            /// <summary>
            /// Each module has it
            /// </summary>
            ChassisThermometer = 2,
            /// <summary>
            /// Each module has it
            /// </summary>
            OdorSourceThermometer = 3,
            /// <summary>
            /// Base module only has it, same as ChassisThermometer
            /// </summary>
            GeneralPurposeThermometer = 4,
            /// <summary>
            /// Base module only has it
            /// </summary>
            InputAirHumiditySensor = 5,
            /// <summary>
            /// Base module only has it
            /// </summary>
            OutputAirHumiditySensor = 6,
            /// <summary>
            /// Base module only has it
            /// </summary>
            PressureSensor = 7,
            /// <summary>
            /// Each module has it
            /// </summary>
            OdorantFlowSensor = 8,
            /// <summary>
            /// Base module only has it
            /// </summary>
            DilutionAirFlowSensor = 9,
            /// <summary>
            /// Each module has it
            /// </summary>
            OdorantValveSensor = 10,
            /// <summary>
            /// Dilution module only has it
            /// </summary>
            OutputValveSensor = 11,

            // Actuators

            /// <summary>
            /// Each module has it
            /// </summary>
            OdorantFlowController = 12,
            /// <summary>
            /// Base module only has it
            /// </summary>
            DilutionAirFlowController = 13,
            /// <summary>
            /// Each module has it
            /// </summary>
            ChassisTemperatureController = 14,
            /// <summary>
            /// Each module has it
            /// </summary>
            OdorantValveController = 15,
            /// <summary>
            /// Dilution module only has it
            /// </summary>
            OutputValveController = 16,
        }

        public enum Sensor
        {
            PID = Capabality.PID,
            BeadThermistor = Capabality.BeadThermistor,
            ChassisThermometer = Capabality.ChassisThermometer,
            OdorSourceThermometer = Capabality.OdorSourceThermometer,
            GeneralPurposeThermometer = Capabality.GeneralPurposeThermometer,
            InputAirHumiditySensor = Capabality.InputAirHumiditySensor,
            OutputAirHumiditySensor = Capabality.OutputAirHumiditySensor,
            PressureSensor = Capabality.PressureSensor,
            OdorantFlowSensor = Capabality.OdorantFlowSensor,
            DilutionAirFlowSensor = Capabality.DilutionAirFlowSensor,
            OdorantValveSensor = Capabality.OdorantValveSensor,
            OutputValveSensor = Capabality.OutputValveSensor,
        }

        public enum Controller
        {
            OdorantFlow = Capabality.OdorantFlowController,
            DilutionAirFlow = Capabality.DilutionAirFlowController,
            ChassisTemperature = Capabality.ChassisTemperatureController,
            OdorantValve = Capabality.OdorantValveController,
            OutputValve = Capabality.OutputValveController,
        }

        /// <summary>
        /// L/min
        /// </summary>
        public static float MAX_DILUTION_AIR_FLOW_RATE = 10;

        /// <summary>
        /// L/min
        /// </summary>
        public static float MAX_ODOR_FLOW_RATE = 1.5f;
    }
}
