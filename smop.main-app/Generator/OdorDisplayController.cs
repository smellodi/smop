using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System.Collections.Generic;

namespace Smop.MainApp.Generator;

internal class OdorDisplayController
{
    public Comm.Result Init()
    {
        return Send(new SetSystem(true, true));
    }

    public Comm.Result Start()
    {
        return Send(new SetMeasurements(SetMeasurements.Command.Start));
    }

    public Comm.Result SetHumidity(float value)
    {
        var humidifierFlow = Device.MaxBaseAirFlowRate * value / 100; // l/min
        var dilutionAirFlow = Device.MaxBaseAirFlowRate - humidifierFlow;  // l/min

        return Send(new SetActuators(new Actuator[]
        {
            new Actuator(Device.ID.Base, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, humidifierFlow),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, dilutionAirFlow),
                ActuatorCapabilities.OdorantValveOpenPermanently
            )),
        }));
    }

    public Comm.Result SetFlows(PulseChannelProps[] channels)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, channel.Flow),
                KeyValuePair.Create(Device.Controller.OdorantValve, 0f)
            )));
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result OpenChannels(PulseChannelProps[] channels, float durationSec)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            if (channel.Active)
            {
                actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities()
                {
                    { Device.Controller.OdorantValve, durationSec * 1000 },
                }));
            }
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result CloseChannels(PulseChannelProps[] channels)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities()
            {
                { Device.Controller.OutputValve, 0 },
            }));
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(OdorDisplayController));

    readonly CommPort _odorDisplay = CommPort.Instance;

    private Comm.Result Send(Request request)
    {
        _nlog.Info($"Sent: {request}");

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);

        if (ack != null)
            _nlog.Info($"Received: {ack}");
        if (result.Error == Comm.Error.Success && response != null)
            _nlog.Info($"Received: {response}");

        return result;
    }
}