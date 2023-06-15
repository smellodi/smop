namespace Smop.PulseGen.Comm;

public interface IOdorDisplaySample
{
	public long Time { get; }
	public double MainValue { get; }
}

public class OdorDisplayMessageSample : IOdorDisplaySample
{
	public long Time { get; }
	public double MainValue { get; }
	public OdorDisplayMessageSample()
	{
		Time = 0;
		MainValue = 0;
	}
}
