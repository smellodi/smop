using Smop.OdorDisplay;
using System.Collections.Generic;

namespace Smop.MainApp.Reproducer;

public record class ProcedureSettings(
    ML.Communicator MLComm,
    KeyValuePair<Device.ID, float>[] TargetFlows
);
