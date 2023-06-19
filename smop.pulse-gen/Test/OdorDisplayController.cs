using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System.Collections.Generic;
using System.Diagnostics;

namespace Smop.PulseGen.Test;

internal class OdorDisplayController
{
    public OdorDisplay.Result Init()
    {
        return Send(new SetSystem(true, true));
    }

    public OdorDisplay.Result Start()
    {
        return Send(new SetMeasurements(SetMeasurements.Command.Start));
    }

    public OdorDisplay.Result SetHumidity(float value)
    {
        var humidifierFlow = Device.MaxBaseAirFlowRate * value / 100;
        var dilutionAirFlow = 1f - humidifierFlow;

        return Send(new SetActuators(new Actuator[]
        {
            new Actuator(Device.ID.Base, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, humidifierFlow / Device.MaxBaseAirFlowRate),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, dilutionAirFlow / Device.MaxBaseAirFlowRate),
                ActuatorCapabilities.OdorantValveOpenPermanently
            )),
        }));
    }

    public OdorDisplay.Result SetFlows(PulseChannelProps[] channels)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, channel.Flow / 1000 / Device.MaxBaseAirFlowRate)
            )));
        }
        
        return Send(new SetActuators(actuators.ToArray()));
    }

    public OdorDisplay.Result OpenChannels(PulseChannelProps[] channels, float durationSec)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities()
            {
                { Device.Controller.OdorantValve, durationSec * 1000 },
            }));
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    public OdorDisplay.Result CloseChannels(PulseChannelProps[] channels)
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

    readonly CommPort _odorDisplay = CommPort.Instance;

    private OdorDisplay.Result Send(Request request)
    {
        Debug.WriteLine($"[OD] Sent:     {request}");

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);

        if (ack != null)
            Debug.WriteLine($"[OD] Received: {ack}");
        if (result.Error == Error.Success && response != null)
            Debug.WriteLine("[OD]   " + response);

        return result;
    }
}