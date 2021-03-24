﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using Nanoleaf.Client;
using Serilog;

namespace Glimmr.Models.ColorTarget.Nanoleaf {
	public sealed class NanoleafDevice : ColorTarget, IColorTarget, IDisposable {
		private TileLayout _layout;
		private bool _disposed;
		public bool Enable { get; set; }
		IColorTargetData IColorTarget.Data {
			get => Data;
			set => Data = (NanoleafData) value;
		}

		public NanoleafData Data { get; set; }
		public bool Testing { get; set; }
		public int Brightness { get; set; }
		public string Id { get; set; }
		public string IpAddress { get; set; }
		public string Tag { get; set; }
		private bool _wasOn;
		private readonly NanoleafClient _nanoleafClient;
		private readonly NanoleafStreamingClient _streamingClient;

		public NanoleafDevice(NanoleafData n, ControlService cs) {
			DataUtil.GetItem<int>("captureMode");
			if (n != null) {
				SetData(n);
				var streamMode = n.Type == "NL29" ? 2 : 1;
				_nanoleafClient = new NanoleafClient(n.Hostname, n.Token, cs.HttpSender);
				_streamingClient = new NanoleafStreamingClient(n.IpAddress, streamMode, cs.UdpClient);
			}

			_disposed = false;
		}
		

		/// <summary>
		/// Use this for sending color data to the panel
		/// </summary>
		/// <param name="n"></param>
		/// <param name="colorService"></param>
		public NanoleafDevice(NanoleafData n, ColorService colorService) : base(colorService) {
			DataUtil.GetItem<int>("captureMode");
			colorService.ColorSendEvent += SetColor;
			if (n != null) {
				SetData(n);
				var streamMode = n.Type == "NL29" ? 2 : 1;
				var cs = ColorService.ControlService;
				_nanoleafClient = new NanoleafClient(n.IpAddress, n.Token);
				_streamingClient = new NanoleafStreamingClient(n.IpAddress,streamMode,cs.UdpClient);
			}

			_disposed = false;
		}
		
		public async Task StartStream(CancellationToken ct) {
			if (!Enable || Streaming) return;
			Streaming = true;

			Log.Debug($@"Nanoleaf: Starting stream at {IpAddress}...");
			_wasOn = await _nanoleafClient.GetPowerStatusAsync();
			Log.Debug("Power status: " + _wasOn);
			if (!_wasOn) await _nanoleafClient.TurnOnAsync();
			await _nanoleafClient.SetBrightnessAsync(100);
			_nanoleafClient.StartExternalAsync().ConfigureAwait(false);
			Log.Debug("Panel started.");
		}

		public async Task StopStream() {
			if (!Enable) return;
			await FlashColor(Color.FromArgb(0, 0, 0));
			Streaming = false;
			await _nanoleafClient.TurnOffAsync();
			Log.Debug($@"Nanoleaf: Stopped panel: {IpAddress}");
		}


		public void SetColor(List<Color> list, List<Color> colors, int fadeTime, bool force = false) {
			if (!Streaming || !Enable || Testing && !force) {
				return;
			}

			var cols = new Dictionary<int, Color>();
			foreach (var p in _layout.PositionData) {
				var color = Color.FromArgb(0, 0, 0);
				if (p.TargetSector != -1) {
					var target = p.TargetSector - 1;
					if (target < colors.Count) {
						color = colors[target];
					} else {
						Log.Warning($"Error, trying to map {target} when count is only {colors.Count}.");	
					}
				}
				
				cols[p.PanelId] = color;
			}

			_streamingClient.SetColorAsync(cols, fadeTime).ConfigureAwait(false);
		}



		public Task ReloadData() {
			var newData = DataUtil.GetDevice<NanoleafData>(Id);
			SetData(newData);
			return Task.CompletedTask;
		}

		private void SetData(NanoleafData n) {
			Data = n;
			DataUtil.GetItem<int>("captureMode");
			IpAddress = n.IpAddress;
			_layout = n.Layout;
			Brightness = n.Brightness;
			Enable = n.Enable;
			Id = n.Id;
		}

		public async Task FlashColor(Color color) {
			var cols = new Dictionary<int, Color>();
			foreach (var pd in _layout.PositionData) {
				cols[pd.PanelId] = color;
			}
			await _streamingClient.SetColorAsync(cols, 0);
		}

		public bool IsEnabled() {
			return Enable;
		}

        
		public bool Streaming { get; set; }

		

		
		public async Task<TileLayout> GetLayout() {
			var layout = await _nanoleafClient.GetLayoutAsync();
			return new TileLayout(layout);
		}


        public void Dispose() {
			Dispose(true);
		}

		private void Dispose(bool disposing) {
			if (_disposed) {
				return;
			}

			if (!disposing) return;
			_disposed = true;
		}
	}
}