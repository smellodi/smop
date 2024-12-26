﻿using Smop.MainApp.Logging;
using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Smop.MainApp.Controllers;

internal class OdorDisplayController
{
    public Comm.Result Init()
    {
        return Send(new SetSystem(true, true));
    }

    public Comm.Result StartMeasurements()
    {
        return Send(new SetMeasurements(SetMeasurements.Command.Start));
    }

    public Comm.Result StopMeasurements()
    {
        return Send(new SetMeasurements(SetMeasurements.Command.Stop));
    }

    public Comm.Result QueryDevices(out Devices? devices)
    {
        var result = Send(new QueryDevices(), out Response? resp);
        devices = resp == null ? null : Devices.From(resp);
        return result;
    }

    public Comm.Result SetHumidity(float value)
    {
        var humidifierFlow = Device.MaxBaseAirFlowRate * value / 100; // l/min
        var dilutionAirFlow = Device.MaxBaseAirFlowRate - humidifierFlow;  // l/min

        return Send(new SetActuators(new Actuator[]
        {
            new(Device.ID.Base, new ActuatorCapabilities(
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

    public Comm.Result StopFlows(Device.ID[] channels)
    {
        var actuators = new List<Actuator>();
        // TODO use proper IDs
        foreach (var channelID in channels)
        {
            actuators.Add(new Actuator(channelID, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
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

    public Comm.Result OpenChannels(OdorChannels channels)
    {
        var actuators = channels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
            .Select(odorChannel => new Actuator(odorChannel.ID, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, odorChannel.Flow),
                odorChannel.Flow > 0 ? ActuatorCapabilities.OdorantValveOpenPermanently : ActuatorCapabilities.OdorantValveClose
            )));

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result OpenChannels(Actuator[] actuators)
    {
        return Send(new SetActuators(actuators));
    }

    public Comm.Result CloseChannels(OdorChannels channels)
    {
        var actuators = channels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
            .Select(odorChannel => new Actuator(odorChannel.ID, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            )));

        return Send(new SetActuators(actuators.ToArray()));
    }

    // Internal

    static readonly NLog.Logger _nlog = NLog.LogManager.GetLogger(nameof(OdorDisplayController));

    readonly CommPort _odorDisplay = CommPort.Instance;

    private Comm.Result Send(Request request) =>
        Send(request, out Response? _);

    private Comm.Result Send(Request request, out Response? resp)
    {
        var reqText = request switch
        {
            SetActuators reqActuators => $"SetActuators {string.Join(" ", reqActuators.Actuators.Select(a => a.ToString()))}",
            SetSystem reqSystem => $"SetSystem Fans {reqSystem.Fans} PIDs {reqSystem.PIDs}",
            _ => request.ToString()
        };
        _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "OD", "Sent", reqText));

        var result = _odorDisplay.Request(request, out Ack? ack, out Response? response);
        resp = response;

        /*if (ack != null)
            _nlog.Info(LogExt.Text(Utils.Timestamp.Ms, "OD", "ACK", ack));
        */
        if (result.Error == Comm.Error.Success && response != null)
            _nlog.Info(LogIO.Text(Utils.Timestamp.Ms, "OD", "Received", response));

        return result;
    }
}