using Smop.MainApp.Controllers.HumanTests;
using System;

namespace Smop.MainApp.Pages;

internal interface IPage<T>
{
    event EventHandler<T> Next;
}

internal interface IHumanTestPage
{
    Settings? Settings { get; }
}

public enum Navigation
{
    Exit,
    Setup,
    Test,
    Finished,
}