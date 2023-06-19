using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System.Collections.Generic;
using System.Diagnostics;

namespace Smop.PulseGen.Test;

internal class OdorDisplayController
{
    public void SetHumidity(float value)
    {
        var humidifierFlow = Device.MaxBaseAirFlowRate * value / 100;
        var dilutionAirFlow = 1f - humidifierFlow;

        Send(new SetActuators(new Actuator[]
        {
            new Actuator(Device.ID.Base, new ActuatorCapabilities(
                ActuatorCapabilities.OdorantValveOpenPermanently,
                KeyValuePair.Create(Device.Controller.OdorantFlow, humidifierFlow),
                KeyValuePair.Create(Device.Controller.DilutionAirFlow, dilutionAirFlow)
            )),
        }));
    }

    public void SetFlows(PulseChannelProps[] channels)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities()
            {
                { Device.Controller.OdorantFlow, channel.Flow },
            }));
        }
        Send(new SetActuators(actuators.ToArray()));
    }

    public void OpenChannels(PulseChannelProps[] channels)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities()
            {
                { Device.Controller.OutputValve, channel.Active ? 1 : 0 },
            }));
        }
        Send(new SetActuators(actuators.ToArray()));
    }

    public void CloseChannels(PulseChannelProps[] channels)
    {
        var actuators = new List<Actuator>();
        foreach (var channel in channels)
        {
            actuators.Add(new Actuator((Device.ID)channel.Id, new ActuatorCapabilities()
            {
                { Device.Controller.OutputValve, 0 },
            }));
        }
        Send(new SetActuators(actuators.ToArray()));
    }

    // Internal

    readonly CommPort _odorDisplay = CommPort.Instance;

    private void Send(SetActuators request)
    {
        Debug.WriteLine($"[OD] Sent:     {request}");

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);

        if (ack != null)
            Debug.WriteLine($"[OD] Received: {ack}");
        if (result.Error == Error.Success && response != null)
            Debug.WriteLine("[OD]   " + response);
    }
}