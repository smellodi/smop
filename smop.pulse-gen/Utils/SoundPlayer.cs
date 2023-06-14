using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;

namespace Smop.PulseGen.Utils;

/// <summary>
/// Plays an MP3 resource file
/// </summary>
public class SoundPlayer : IDisposable
{
	/// <summary>
	/// Fires when the sound stops playing
	/// </summary>
	public event EventHandler? Finished;

	/// <summary>
	/// True if the sound is playing or paused
	/// </summary>
	public bool IsPlaying => _player.PlaybackState != PlaybackState.Stopped;

	/// <summary>
	/// Name, for debugging purposes
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="mp3SoundData">MP3 resource file</param>
	/// <param name="name">Sound name (useful for debugging purposes)</param>
	/// <param name="deviceName">Device to play the sound on</param>
	public SoundPlayer(byte[] mp3SoundData, string name = "", string deviceName = "")
	{
		_mp3 = new Mp3FileReader(new MemoryStream(mp3SoundData));
		_mp3.Seek(0, SeekOrigin.Begin);

		Name = name;

		if (!string.IsNullOrEmpty(deviceName))
		{
			SetDevice(deviceName);
		}

		_player.Init(_mp3);
		_player.PlaybackStopped += Player_Stopped;
	}

	/// <summary>
	/// Plays the sound
	/// </summary>
	/// <param name="cyclic">True if the sound will play in a loop and will be stopped externally, otherwise it will stop automatically after playing the sound once</param>
	/// <returns>Self to chain with <see cref="Chain(Action)"/> if needed</returns>
	public SoundPlayer Play(bool cyclic = false)
	{
		_cyclic = cyclic;
		_player.Volume = 1;
		DispatchOnce.Do(0.001, () => _player.Play());

		return this;
	}

	public SoundPlayer Play(float volume)
	{
		_player.Volume = volume;
		DispatchOnce.Do(0.001, () => _player.Play());

		return this;
	}

	public SoundPlayer PlayASAP()
	{
		_cyclic = false;
		_player.Volume = 1;
		_player.Play();

		return this;
	}

	/// <summary>
	/// Registers a callback to execute after the playback stops automatically.
	/// </summary>
	/// <param name="onFinished">A callback function to run when playing is finished</param>
	public SoundPlayer Chain(Action onFinished)
	{
		_onFinished.Add(onFinished);
		return this;
	}

	/// <summary>
	/// Stops playing, cancels all chaining actions
	/// </summary>
	public void Stop()
	{
		_onFinished.Clear();
		_cyclic = false;

		if (IsPlaying)
		{
			_player.Stop();
		}
	}

	public void Dispose()
	{
		Stop();

		_mp3.Dispose();
		_player.Dispose();
	}

	// Internal

	readonly WaveOut _player = new();
	readonly List<Action> _onFinished = new();

	readonly Mp3FileReader _mp3;

	bool _cyclic = false;

	private void Player_Stopped(object? sender, StoppedEventArgs e)
	{
		_mp3.Seek(0, SeekOrigin.Begin);

		if (_cyclic)
		{
			Play(true);
		}
		else
		{
			Finished?.Invoke(this, new EventArgs());

			if (_onFinished.Count > 0)
			{
				_onFinished[0]();
				_onFinished.RemoveAt(0);
			}
		}
	}

	private void SetDevice(string deviceName)
	{
		int deviceID = -1;
		for (int i = 0; i < WaveOut.DeviceCount; i++)
		{
			var caps = WaveOut.GetCapabilities(i);
			if (caps.ProductName.Contains("Speakers") && caps.ProductName.Contains(deviceName))
			{
				deviceID = i;
				break;
			}
		}

		if (deviceID < 0)
		{
			throw new ArgumentException($"Device '{deviceName}' is not connected");
		}
		else
		{
			_player.DeviceNumber = deviceID;
		}
	}
}
