﻿using System;

namespace Smop.MainApp.Pages;

internal interface IPage<T>
{
    event EventHandler<T> Next;
}

public enum Navigation
{
    Exit,
    PulseGeneratorSetup,
    OdorReproductionSetup,
    PulseGenerator,
    OdorReproduction,
    Finished,
}