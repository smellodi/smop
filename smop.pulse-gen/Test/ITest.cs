using System;

namespace Smop.PulseGen.Test;

public interface ITest : IDisposable
{
	void Start(PulseSetup setup);
}
