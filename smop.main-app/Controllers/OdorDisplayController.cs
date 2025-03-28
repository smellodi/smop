﻿using Smop.MainApp.Logging;
using Smop.OdorDisplay;
using Smop.OdorDisplay.Packets;
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
                KeyValuePair.Create(Device.Controller.OdorantFlow, channel.Flow)
            )));
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result OpenValves(int[] channelIds, float durationSec = 0)
    {
        var actuators = new List<Actuator>();
        foreach (var id in channelIds)
        {
            if (durationSec <= 0)
                actuators.Add(new Actuator((Device.ID)id, new ActuatorCapabilities(
                    ActuatorCapabilities.OdorantValveOpenPermanently
                )));
            else
                actuators.Add(new Actuator((Device.ID)id, new ActuatorCapabilities()
                {
                    { Device.Controller.OdorantValve, durationSec * 1000 },
                }));
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result SetFlowsAndValves(OdorChannels channels)
    {
        var actuators = channels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
            .Select(odorChannel => new Actuator(odorChannel.ID, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, odorChannel.Flow),
                odorChannel.Flow > 0 ? ActuatorCapabilities.OdorantValveOpenPermanently : ActuatorCapabilities.OdorantValveClose
            )));

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result SetFlowsAndValves(Actuator[] actuators)
    {
        return Send(new SetActuators(actuators));
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

    public Comm.Result CloseValves(int[] channelIds)
    {
        var actuators = new List<Actuator>();
        foreach (var id in channelIds)
        {
            actuators.Add(new Actuator((Device.ID)id, new ActuatorCapabilities(
                ActuatorCapabilities.OdorantValveClose
            )));
        }

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result ShutdownChannels(OdorChannels channels)
    {
        var actuators = channels
            .Where(odorChannel => !string.IsNullOrWhiteSpace(odorChannel.Name))
            .Select(odorChannel => new Actuator(odorChannel.ID, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.OdorantFlow, 0f),
                ActuatorCapabilities.OdorantValveClose
            )));

        return Send(new SetActuators(actuators.ToArray()));
    }

    public Comm.Result SetChassisHeater(float value, params Device.ID[] devices)
    {
        return Send(new SetActuators(devices.Select(device =>
            new Actuator(device, new ActuatorCapabilities(
                KeyValuePair.Create(Device.Controller.ChassisTemperature, value)
            ))).ToArray()
        ));
    }

    public Comm.Result SetExternalValveState(bool isOpened)
    {
        var actuator = new Actuator(Device.ID.Odor5, new ActuatorCapabilities(
                isOpened ? ActuatorCapabilities.OutputValveOpenPermanently : ActuatorCapabilities.OutputValveClose
            ));
        return Send(new SetActuators([actuator]));
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