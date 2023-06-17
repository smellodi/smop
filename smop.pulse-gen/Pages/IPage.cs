using System;

namespace Smop.PulseGen.Pages;

internal interface IPage<T>
{
	event EventHandler<T> Next;
}

public enum Navigation
{
	Exit,
	Setup,
	Test,
	Finished,
}