namespace Smop.PulseGen.OdorDisplay
{

	/// <summary>
	/// Sample record/vector; all the measured values for one second
	/// </summary>
	public struct DeviceSample : ISample
	{
		/// <summary>
		/// Sample time (milliseconds from start)
		/// </summary>
		public long Time { get; set; }
		/// <summary>
		/// PID in the system (volts)
		/// </summary>
		public float SystemPID { get; }
		/// <summary>
		/// PID in the mask (volts)
		/// </summary>
		public float MaskPID { get; }
		/// <summary>
		/// Air temperature inside the device (°C, converted from volts)
		/// </summary>
		public float SystemTemperature { get; }
		/// <summary>
		/// Air temperature inside the mask (°C, converted from ohms)
		/// </summary>
		public float MaskTemperature { get; }
		/// <summary>
		/// Air humidity (%, converted from volts)
		/// </summary>
		public float Humidity { get; }

		public double MainValue => SystemPID;

		public MessageSample Raw { get; }

		public DeviceSample(MessageSample sample)
		{
			Raw = sample;

			/*Time = sample.Time; //Utils.Timestamp.Ms;
			SystemPID = 1000f * sample.PID0;
			MaskPID = 1000f * sample.PID1;
			SystemTemperature = sample.IC0 > 0 ? sample.IC0 * 100 : float.PositiveInfinity;
			MaskTemperature = float.IsFinite(sample.Thermistor0) ? 25.0f - (sample.Thermistor0 - 100_000) / 4500 : sample.Thermistor0;
			Humidity = sample.IC1 > 0 ? (sample.IC1 - 0.794579f) / 0.03003766f : float.PositiveInfinity;
			*/
		}

		public override string ToString() => string.Join('\t', new string[] {
		Time.ToString(),
		SystemPID.ToString("F2"),
		float.IsFinite(MaskPID) ? MaskPID.ToString("F2") : "0",
		float.IsFinite(SystemTemperature) ? SystemTemperature.ToString("F2") : "0",
		float.IsFinite(MaskTemperature) ? MaskTemperature.ToString("F2") : "0",
		float.IsFinite(Humidity) ? Humidity.ToString("F2") : "0",
	});

		public string ToCSV(int precision = 6) => string.Join(',', new string[] {
		Time.ToString(),
		SystemPID.ToString(),
		float.IsFinite(MaskPID) ? MaskPID.ToString($"F{precision}") : "0",
		float.IsFinite(SystemTemperature) ? SystemTemperature.ToString($"F{precision}") : "0",
		float.IsFinite(MaskTemperature) ? MaskTemperature.ToString($"F{precision}") : "0",
		float.IsFinite(Humidity) ? Humidity.ToString($"F{precision}") : "0",
	});

		public static string[] Header => new string[] {
		"Time",
		"sPID",
		"mPID",
		"sTemp",
		"mTemp",
		"Humid",
	};
	}
}