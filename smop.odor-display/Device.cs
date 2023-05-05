namespace Smop.OdorDisplay;

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

    public enum Capability
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
        PID = Capability.PID,
        BeadThermistor = Capability.BeadThermistor,
        ChassisThermometer = Capability.ChassisThermometer,
        OdorSourceThermometer = Capability.OdorSourceThermometer,
        GeneralPurposeThermometer = Capability.GeneralPurposeThermometer,
        InputAirHumiditySensor = Capability.InputAirHumiditySensor,
        OutputAirHumiditySensor = Capability.OutputAirHumiditySensor,
        PressureSensor = Capability.PressureSensor,
        OdorantFlowSensor = Capability.OdorantFlowSensor,
        DilutionAirFlowSensor = Capability.DilutionAirFlowSensor,
        OdorantValveSensor = Capability.OdorantValveSensor,
        OutputValveSensor = Capability.OutputValveSensor,
    }

    public enum Controller
    {
        OdorantFlow = Capability.OdorantFlowController,
        DilutionAirFlow = Capability.DilutionAirFlowController,
        ChassisTemperature = Capability.ChassisTemperatureController,
        OdorantValve = Capability.OdorantValveController,
        OutputValve = Capability.OutputValveController,
    }

    /// <summary>
    /// L/min
    /// </summary>
    public static float MaxBaseAirFlowRate => 10f;

    /// <summary>
    /// L/min
    /// </summary>
    public static float MaxOdoredAirFlowRate => 1.5f;
}
