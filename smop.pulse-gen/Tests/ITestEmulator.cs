namespace Smop.PulseGen.Tests;

public enum EmulationCommand
{
	ForceToFinishWithResult,
}

public interface ITestEmulator
{
	/// <summary>
	/// Tells the test if should stop as soon as possible, and lets emulate data that are still missing (not collected)
	/// then exit the test procedure
	/// </summary>
	void EmulationFinilize();
}
