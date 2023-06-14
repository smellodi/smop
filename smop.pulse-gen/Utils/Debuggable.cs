using System;
using System.Linq;
using System.Reflection;

namespace Smop.PulseGen.Utils;

public class Debuggable : IDisposable
{
	public Debuggable() { }

	public string EventsInvocations => GetEventsInvocations(this);

	public static string GetEventsInvocations(object obj)
	{
		Func<EventInfo, FieldInfo?> ei2fi =
			ei => obj.GetType().GetField(ei.Name,
				BindingFlags.Public |
				BindingFlags.NonPublic |
				BindingFlags.Instance |
				BindingFlags.GetField);

		return "[DBG] " + obj.GetType().Name + " events:\n" + string.Join('\n',
			obj.GetType()
				.GetEvents(BindingFlags.Instance | BindingFlags.Public)
				.Select(evt =>
				{
					var eventFieldInfo = ei2fi(evt);
					var eventFieldValue = eventFieldInfo?.GetValue(obj) as Delegate;
					var subsCount = eventFieldValue?.GetInvocationList().Length ?? 0;
					return $"   {evt.Name} = {subsCount}";
				})
			);
	}

	public void Dispose()
	{
		_makeTheObjectLookBig = null;
	}

	// Internal

	int[]? _makeTheObjectLookBig = new int[15_000_000];
}
