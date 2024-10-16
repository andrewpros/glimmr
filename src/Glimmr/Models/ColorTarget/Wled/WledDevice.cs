﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models.ColorSource.Udp;
using Glimmr.Models.Util;
using Glimmr.Services;
using Kevsoft.WLED;
using Serilog;

#endregion

namespace Glimmr.Models.ColorTarget.Wled;

public class WledDevice : ColorTarget, IColorTarget, IDisposable {
	private string IpAddress { get; set; }
	private const int Port = 21324;
	private readonly UdpClient _udpClient;
	private readonly WLedClient _wLedClient;

	private int _brightness;
	private WledData _data;

	private bool _disposed;
	private IPEndPoint? _ep;
	private int _ledCount;
	private float _multiplier;
	private int _offset;
	private int _protocol = 2;
	private WledSegment[] _segments;
	private StripMode _stripMode;
	private int _targetSector;

	public WledDevice(WledData wd, ColorService cs) : base(cs) {
		_segments = Array.Empty<WledSegment>();
		cs.ControlService.RefreshSystemEvent += RefreshSystem;
		_udpClient = cs.ControlService.UdpClient;
		_data = wd ?? throw new ArgumentException("Invalid WLED data.");
		Id = _data.Id;
		IpAddress = _data.IpAddress;
		_brightness = _data.Brightness;
		_multiplier = _data.LedMultiplier;
		_wLedClient = new WLedClient("http://" + IpAddress + "/");
		ReloadData();
		ColorService.ColorSendEventAsync += SetColors;
	}

	public bool Enable { get; set; }
	public bool Streaming { get; set; }
	public string Id { get; }

	IColorTargetData IColorTarget.Data {
		get => _data;
		set => _data = (WledData)value;
	}


	public async Task StartStream(CancellationToken ct) {
		if (Streaming || !Enable) {
			return;
		}

		Log.Debug($"{_data.Tag}::Starting stream: {_data.Id}...");
		_targetSector = _data.TargetSector;
		_ep = IpUtil.Parse(IpAddress, Port);
		if (_ep == null) {
			return;
		}

		await SetLightBrightness(_brightness);
		await SetLightPower(true);
		await FlashColor(Color.Black);
		Streaming = true;
		Log.Debug($"{_data.Tag}::Stream started: {_data.Id}.");
	}
	
	

	public async Task StopStream() {
		if (!Streaming) {
			return;
		}

		Log.Debug($"{_data.Tag}::Stopping stream...{_data.Id}.");
		Streaming = false;
		await FlashColor(Color.Black);
		await _wLedClient.Post(new StateRequest { On = false });
		await Task.FromResult(true);
		await SetLightPower(false);
		Log.Debug($"{_data.Tag}::Stream stopped: {_data.Id}.");
	}


	public async Task FlashColor(Color color) {
		try {
			var colors = ColorUtil.FillArray(color, _ledCount);
			var cp = new ColorPacket(colors, (UdpStreamMode)_protocol);
			var packet = cp.Encode();
			await _udpClient.SendAsync(packet.ToArray(), packet.Length, _ep).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Exception flashing color: " + e.Message);
		}
	}



	public Task ReloadData() {
		var oldBrightness = _brightness;
		var dev = DataUtil.GetDevice<WledData>(Id);
		if (dev != null) {
			_data = dev;
		}

		_protocol = _data.Protocol;
		_offset = _data.Offset;
		_brightness = _data.Brightness;
		IpAddress = _data.IpAddress;
		Enable = _data.Enable;
		_stripMode = _data.StripMode;
		_targetSector = _data.TargetSector;
		_multiplier = _data.LedMultiplier;
		if (_multiplier == 0) {
			_multiplier = 1;
		}

		if (oldBrightness != _brightness) {
			SetLightBrightness(_brightness).ConfigureAwait(false);
		}

		_segments = _data.Segments;
		_ledCount = _data.LedCount;
		return Task.CompletedTask;
	}


	public void Dispose() {
		Dispose(true).ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}


	public async Task SetColors(IReadOnlyList<Color> ledColors, IReadOnlyList<Color> sectorColors) {
		if (!Streaming || !Enable) {
			return;
		}

		var toSend = ledColors.ToArray();
		switch (_stripMode) {
			case StripMode.Single when _targetSector > sectorColors.Count || _targetSector == -1:
				Log.Debug("OOR: " + _targetSector + " vs " + sectorColors.Count);
				return;
			case StripMode.Single:
				toSend = ColorUtil.FillArray(sectorColors[_targetSector - 1], _ledCount).ToArray();
				break;
			case StripMode.Sectored: {
				var output = new Color[_ledCount];
				foreach (var seg in _segments) {
					var cols = ColorUtil.TruncateColors(toSend, seg.Offset, seg.LedCount, seg.Multiplier);
					if (seg.ReverseStrip) {
						cols = cols.Reverse().ToArray();
					}

					var start = seg.Start;
					foreach (var col in cols) {
						if (start >= _ledCount) {
							Log.Warning($"Error, dest color idx is greater than led count: {start}/{_ledCount}");
							continue;
						}

						output[start] = col;
						start++;
					}
				}
				toSend = output;
				break;
			}
			case StripMode.Loop: {
				toSend = ColorUtil.TruncateColors(toSend, _offset, _ledCount, _multiplier);
				toSend = ShiftColors(toSend, _data.ReverseStrip);
				break;
			}
			case StripMode.Normal: {
				toSend = ColorUtil.TruncateColors(toSend, _offset, _ledCount, _multiplier);
				if (_data.ReverseStrip) {
					toSend = toSend.Reverse().ToArray();
				}

				break;
			}
			default:
				Log.Debug("Well, this is weird...");
				break;
		}

		if (_ep == null) {
			Log.Debug("No endpoint.");
			return;
		}

		try {
			var cp = new ColorPacket(toSend, (UdpStreamMode)_protocol);
			var packet = cp.Encode(255);
			await _udpClient.SendAsync(packet.ToArray(), packet.Length, _ep).ConfigureAwait(false);
		} catch (Exception e) {
			Log.Debug("Exception: " + e.Message + " at " + e.StackTrace);
		}
	}

	private Task SetColors(object sender, ColorSendEventArgs args) {
		return SetColors(args.LedColors, args.SectorColors);
	}


	private static Color[] ShiftColors(IReadOnlyList<Color> input, bool reverse) {
		var output = new Color[input.Count];
		var il = output.Length - 1;
		if (!reverse) {
			for (var i = 0; i < input.Count / 2; i++) {
				output[i] = input[i];
				output[il - i] = input[i];
			}
		} else {
			var l = 0;
			for (var i = (input.Count - 1) / 2; i >= 0; i--) {
				output[i] = input[l];
				output[il - i] = input[l];
				l++;
			}
		}


		return output;
	}

	private void RefreshSystem() {
		ReloadData();
	}

	private async Task SetLightPower(bool on) {
		var res = await _wLedClient.GetState();
		var oc = 0;
		while (res.On != on && oc < 5) {
			await _wLedClient.Post(new StateRequest { On = on });
			// If on is false, send a manual post directly to the device with a JSON body of "live":false
			if (!on) {
				var client = new WebClient();
				client.Headers.Add("Content-Type", "application/json");
				client.UploadString("http://" + IpAddress + "/json/state", "{\"live\":false}");
			}
			res = await _wLedClient.GetState();
			oc++;
		}
	}

	private async Task SetLightBrightness(int brightness) {
		var scaledBright = brightness / 100f * 255f;
		if (scaledBright > 255) {
			scaledBright = 255;
		}

		await _wLedClient.Post(new StateRequest { Brightness = (byte)scaledBright });
	}

	protected virtual async Task Dispose(bool disposing) {
		if (_disposed) {
			return;
		}

		if (disposing) {
			if (Streaming) {
				await StopStream();
			}
		}

		_disposed = true;
	}
}