namespace Smop.PulseGen.OdorDisplay;

public interface ISample
{
	public long Time { get; }
	public double MainValue { get; }
}

public class MessageSample : ISample
{
	public long Time { get; }
	public double MainValue { get; }
	public MessageSample()
	{
		Time = 0;
		MainValue = 0;
	}
}
