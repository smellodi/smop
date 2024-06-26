﻿using System.Collections.Generic;
using System.Linq;

namespace Smop.OdorDisplay.Packets;

public class Devices : Response
{
    public static int MaxOdorModuleCount => 9;

    public bool HasBaseModule => _payload![0] != 0;
    public bool HasDilutionModule => _payload![10] != 0;

    public bool HasOdorModule(int index) => index < MaxOdorModuleCount && _payload![index + 1] != 0;

    public static Devices? From(Response response)
    {
        if (response?.Type != Type.Devices || response?.Payload.Length != MaxOdorModuleCount + 2)
        {
            return null;
        }

        return new Devices(response.Payload);
    }

    public Devices(byte[] payload) : base(Type.Devices, payload) { }

    public override string ToString()
    {
        var flags = new List<string>()
        {
            $"Base={HasBaseModule.AsFlag()}",
        };
        for (int i = 0; i < MaxOdorModuleCount; i++)
        {
            flags.Add($"Odor{i + 1}={HasOdorModule(i).AsFlag()}");
        }
        flags.Add($"Dilution={HasDilutionModule.AsFlag()}");
        return $"{_type} {string.Join(" ", flags)}";
    }

    // Internal

    internal Devices(bool[] flag) : base(Type.Devices, flag.Select(f => (byte)(f ? 1 : 0)).ToArray()) { }
}
