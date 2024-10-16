#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Led;

/// <summary>
///     A wrapper for the WS281X Library.
///     Despite showing two devices in the UI, for all intents and purposes,
///     the "LED 0" device actually handles all updates for *both* strips.
///     All data is passed to the "agent" via LED 0 and updated for both
///     segments simultaneously to avoid lag and extra work.
/// </summary>
public class LedDevice : ColorTarget, IColorTarget {
	private readonly LedAgent? _agent;
	private LedData _data;

	public LedDevice(LedData ld, ColorService cs) : base(cs) {
		Id = ld.Id;
		_data = ld;

		// We only bind and create agent for LED device 0, regardless of which one is enabled.
		// The agent will handle all the color stuff for both strips.
		if (Id == "1") {
			return;
		}

		// Store the second LED strip data as well...
		DataUtil.GetDevice<LedData>("1");
		Id = _data.Id;
		_agent = cs.ControlService.GetAgent("LedAgent");
		Enable = _agent?.Enable ?? false;
		Log.Debug("LED device created.");
		cs.ColorSendEventAsync += SetColors;
	}

	public bool Streaming { get; set; }
	public string Id { get; }
	public bool Enable { get; set; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (LedData)value;
	}

	public async Task StartStream(CancellationToken ct) {
		// Raspi only.
		if (!SystemUtil.IsRaspberryPi()) {
			return;
		}

		// Set streaming to true if we're using strip 1.
		if (Enable && Id == "1") {
			Streaming = true;
			return;
		}

		// Reload all data
		_agent?.ReloadData();

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		Streaming = true;
		await Task.FromResult(Streaming);
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		if (Id == "1") {
			Streaming = false;
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		await StopLights();
		Streaming = false;
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}


	/// <summary>
	///     Flash the color of the individual strip.
	///     This one, we'll use, because it's not used frequently.
	/// </summary>
	/// <param name="color">The color to flash.</param>
	/// <returns></returns>
	public Task FlashColor(Color color) {
		_agent?.SetColor(color, Id);
		return Task.CompletedTask;
	}


	public void Dispose() {
		Streaming = false;
		_agent?.Clear();
	}

	public Task ReloadData() {
		var ld = DataUtil.GetDevice<LedData>(Id);
		if (ld != null) {
			_data = ld;
		}

		// Don't reload if not LED 0
		if (Id == "1") {
			return Task.CompletedTask;
		}

		_agent?.ReloadData();
		Enable = _agent?.Enable ?? false;
		return Task.CompletedTask;
	}


	public Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> _) {
		// Nothing to do if not LED 0.
		if (Id == "1") {
			return Task.CompletedTask;
		}

		try {
			_agent?.SetColors(ledColors.ToArray());
		} catch (Exception e) {
			Log.Debug("Exception setting colors: " + e.Message);
		}

		return Task.CompletedTask;
	}

	private async Task SetColors(object sender, ColorSendEventArgs args) {
		if (Id == "1") {
			return;
		}

		await SetColors(args.LedColors, args.SectorColors);
	}


	private async Task StopLights() {
		if (Id == "1") {
			return;
		}

		_agent?.Clear();
		await Task.FromResult(true);
	}
}