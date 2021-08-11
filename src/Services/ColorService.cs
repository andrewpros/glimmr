﻿#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Enums;
using Glimmr.Models;
using Glimmr.Models.ColorSource;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace Glimmr.Services {
	// Handles capturing and sending color data
	public class ColorService : BackgroundService {
		public ControlService ControlService { get; }
		public readonly FrameCounter Counter;
		public DeviceMode DeviceMode;
		private bool _autoDisabled;
		private int _autoDisableDelay;
		private CaptureMode _captureMode;
		private readonly TimeSpan _frameSpan;
		private readonly Stopwatch _frameWatch;
		private bool _enableAutoDisable;

		private Task _streamTask;
		// Figure out how to make these generic, non-callable

		private IColorTarget[] _sDevices;

		// Token for the color target
		private CancellationTokenSource _targetTokenSource;
		
		// Token for the color source
		private CancellationTokenSource _streamTokenSource;

		
		private readonly Dictionary<string, IColorSource> _streams;
		private bool _streamStarted;

		
		//private float _fps;
		private SystemData _systemData;
		private readonly Stopwatch _watch;
		public FrameSplitter Splitter;
		

		public ColorService(ControlService controlService) {
			controlService.ColorService = this;
			_watch = new Stopwatch();
			_frameWatch = new Stopwatch();
			_streamTokenSource = new CancellationTokenSource();
			_targetTokenSource = new CancellationTokenSource();
			_sDevices = Array.Empty<IColorTarget>();
			_frameSpan = TimeSpan.FromMilliseconds(1000f / 60);
			_systemData = DataUtil.GetSystemData();
			_enableAutoDisable = _systemData.EnableAutoDisable;
			_streams = new Dictionary<string, IColorSource>();
			ControlService = controlService;
			Counter = new FrameCounter(this);
			ControlService.ColorService = this;
			ControlService.TriggerSendColorEvent += SendColors;
			ControlService.SetModeEvent += Mode;
			ControlService.DeviceReloadEvent += RefreshDeviceData;
			ControlService.RefreshLedEvent += ReloadLedData;
			ControlService.RefreshSystemEvent += ReloadSystemData;
			ControlService.TestLedEvent += LedTest;
			ControlService.FlashDeviceEvent += FlashDevice;
			ControlService.FlashSectorEvent += FlashSector;
			ControlService.DemoLedEvent += Demo;
			Splitter = new FrameSplitter(controlService);
			LoadServices();
			
		}

		private void LoadServices() {
			var classes = SystemUtil.GetClasses<IColorSource>();
			foreach (var c in classes) {
				try {
					var tag = c.Replace("Glimmr.Models.ColorSource.", "");
					tag = tag.Split(".")[0];
					var args = new object[] {this};
					dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
					if (obj == null) {
						continue;
					}
					
					var dObj = (IColorSource) obj;
					Log.Debug("Adding color source: " + tag);
					_streams[tag] = dObj;
					
				} catch (InvalidCastException e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}
			}
		}

		public event Action<List<Color>, List<Color>, int, bool> ColorSendEvent = delegate { };
		
		public event Action FrameSaveEvent = delegate {  };

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			Initialize().ConfigureAwait(true);
			return Task.Run(async () => {
				var fc = 0;
				while (!stoppingToken.IsCancellationRequested) {
					await CheckAutoDisable();
					if (fc >= 5) {
						fc = 0;
						FrameSaveEvent.Invoke();
					}
					await Task.Delay(1000, stoppingToken);
					fc++;
				}
				DataUtil.Dispose();				
			}, CancellationToken.None);
		}

		private async Task Initialize() {
			Log.Information("Starting color service...");
			LoadData();
			if (!_systemData.SkipDemo) {
				Log.Information("Executing demo...");
				await Demo(this, null);
				Log.Information("Demo complete.");
			} else {
				Log.Information("Skipping demo.");	
			}
			await Mode(this, new DynamicEventArgs(DeviceMode)).ConfigureAwait(true);
			Log.Information($"Color service started, device mode is {DeviceMode}.");

		}

		public override async Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Stopping color service...");
			await StopServices();
			DataUtil.Dispose();
			Log.Information("Color service stopped.");
			await base.StopAsync(cancellationToken);
		}

		public BackgroundService? GetStream(string name) {
			if (_streams.ContainsKey(name)) {
				return (BackgroundService) _streams[name];
			}

			return null;
		}

		private async Task FlashDevice(object o, DynamicEventArgs dynamicEventArgs) {
			var devId = dynamicEventArgs.P1;
			var disable = false;
			var ts = new CancellationTokenSource();
			try {
			} catch (Exception) {
				// Ignored
			}

			var bColor = Color.FromArgb(0, 0, 0, 0);
			var rColor = Color.FromArgb(255, 255, 0, 0);
			foreach (var t in _sDevices) {
				if (t.Id != devId) {
					continue;
				}

				var sd = t;
				sd.Testing = true;
				Log.Information("Flashing device: " + devId);
				if (!sd.Streaming) {
					disable = true;
					await sd.StartStream(ts.Token);
				}

				await sd.FlashColor(rColor);
				Thread.Sleep(500);
				await sd.FlashColor(bColor);
				Thread.Sleep(500);
				await sd.FlashColor(rColor);
				Thread.Sleep(500);
				await sd.FlashColor(bColor);
				sd.Testing = false;
				if (!disable) {
					continue;
				}

				await sd.StopStream();
				ts.Cancel();
				ts.Dispose();
			}
		}

		private async Task FlashSector(object o, DynamicEventArgs dynamicEventArgs) {
			var sector = dynamicEventArgs.P1;
			var col = Color.FromArgb(255, 255, 0, 0);
			Color[] colors = ColorUtil.AddLedColor(new Color[_systemData.LedCount], sector, col, _systemData);
			var sectorCount = (_systemData.HSectors + _systemData.VSectors) * 2 - 4;
			var sectors = ColorUtil.EmptyColors(new Color[sectorCount]);
			if (sector < sectorCount) {
				sectors[sector] = col;
			}

			var black = ColorUtil.EmptyList(_systemData.LedCount);
			var blackSectors = ColorUtil.EmptyList(sectorCount);
			var devices = new List<IColorTarget>();
			foreach (var t in _sDevices) {
				if (t.Enable) {
					devices.Add(t);
				}
			}

			foreach (var strip in devices.Where(strip => strip != null)) {
				strip.Testing = true;
			}

			foreach (var strip in devices) {
				strip?.SetColor(colors.ToList(), sectors.ToList(), 0);
			}

			await Task.Delay(500);

			foreach (var strip in devices) {
				strip?.SetColor(black, blackSectors, 0);
			}

			foreach (var strip in devices) {
				strip?.SetColor(colors.ToList(), sectors.ToList(), 0);
			}

			await Task.Delay(1000);

			foreach (var strip in devices.Where(strip => strip != null)) {
				strip.SetColor(black, blackSectors, 0);
				strip.Testing = false;
			}
		}

		private async Task CheckAutoDisable() {
			// Don't do anything if auto-disable isn't enabled
			if (!_enableAutoDisable) {
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
				return;
			}
			bool sourceActive;
			// If we're in video or audio mode, check the source is active...
			if (DeviceMode == DeviceMode.Video && _streams[DeviceMode.Video.ToString()] != null) {
				sourceActive = _streams[DeviceMode.Video.ToString()].SourceActive;
			} else { // Otherwise, just keep it enabled for now.
				//todo: Add proper source checks for other media. 
				sourceActive = true;
			}
			
			if (sourceActive) {
				// If our source is active, but not auto-disabled, do nothing
				if (_autoDisabled) {
					Log.Information("Auto-enabling stream.");
					_autoDisabled = false;
					DataUtil.SetItem("AutoDisabled", _autoDisabled);
					ControlService.SetModeEvent -= Mode;
					await ControlService.SetMode((int) DeviceMode);
					ControlService.SetModeEvent += Mode;
				}
				_watch.Reset();
			} else {
				if (_autoDisabled || DeviceMode != DeviceMode.Video) {
					return;
				}
				if (!_watch.IsRunning) {
					_watch.Restart();
				}

				if (_watch.ElapsedMilliseconds >= _autoDisableDelay * 1000f) {
					_autoDisabled = true;
					DataUtil.SetItem("AutoDisabled", _autoDisabled);
					ControlService.SetModeEvent -= Mode;
					await ControlService.SetMode(0);
					ControlService.SetModeEvent += Mode;
					Log.Information(
						$"Auto-disabling stream {_watch.ElapsedMilliseconds} vs {_autoDisableDelay * 1000}.");
					_watch.Reset();
				}
			}
		}


		private async Task LedTest(object o, DynamicEventArgs dynamicEventArgs) {
			int led = dynamicEventArgs.P1;
			var colors = ColorUtil.EmptyList(_systemData.LedCount);
			var blackColors = colors;
			colors[led] = Color.FromArgb(255, 0, 0);
			var sectors = ColorUtil.LedsToSectors(colors, _systemData);
			var blackSectors = ColorUtil.EmptyList(_systemData.SectorCount);
			foreach (var dev in _sDevices) {
				dev.Testing = true;
				dev.SetColor(colors, sectors, 0, true);
			}

			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(blackColors, blackSectors, 0, true);
			}

			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(colors, sectors, 0, true);
			}

			await Task.Delay(500);
			foreach (var dev in _sDevices) {
				dev.SetColor(blackColors, blackSectors, 0, true);
				dev.Testing = false;
			}
		}

		private void LoadData() {
			var sd = DataUtil.GetSystemData();
			// Reload main vars
			DeviceMode = (DeviceMode) sd.DeviceMode;
			_captureMode = (CaptureMode) (DataUtil.GetItem<int>("CaptureMode") ?? 2);
			_targetTokenSource = new CancellationTokenSource();
			_systemData = DataUtil.GetSystemData();
			_enableAutoDisable = _systemData.EnableAutoDisable;
			_autoDisableDelay = _systemData.AutoDisableDelay;
			// Create new lists
			var sDevs = new List<IColorTarget>();
			var classes = SystemUtil.GetClasses<IColorTarget>();
			var deviceData = DataUtil.GetDevices();
			foreach (var c in classes) {
				try {
					var tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					foreach (var device in deviceData.Where(device => device.Tag == tag).Where(device => tag != "Led" || device.Id != "2")) {
						var args = new object[] {device, this};
						dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
						if (obj == null) {
							continue;
						}

						var dObj = (IColorTarget) obj;
						sDevs.Add(dObj);
					}
				} catch (InvalidCastException e) {
					Log.Warning("Exception: " + e.Message + " at " + e.StackTrace);
				}
			}

			_sDevices = sDevs.ToArray();
			Log.Debug($"Loaded {_sDevices.Length} devices.");
		}

		private async Task Demo(object o, DynamicEventArgs? dynamicEventArgs) {
			await StartStream();
			var ledCount = 300;
			var sectorCount = _systemData.SectorCount;
			try {
				ledCount = _systemData.LedCount;
			} catch (Exception) {
				// ignored 
			}

			var i = 0;
			var cols = ColorUtil.EmptyList(ledCount);
			var secs = ColorUtil.EmptyList(sectorCount);
			try {
				while (i < ledCount) {
					var pi = i * 1.0f;
					var progress = pi / ledCount;
					var sector = (int) Math.Round(progress * sectorCount);
					var rCol = ColorUtil.Rainbow(progress);
					cols[i] = rCol;
					if (sector < secs.Count) {
						secs[sector] = rCol;
					}

					try {
						SendColors(cols, secs, 0, true);
					} catch (Exception e) {
						Log.Warning("SEND EXCEPTION: " + JsonConvert.SerializeObject(e));
					}

					i++;
				}
			} catch (Exception f) {
				Log.Warning("Outer demo exception: " + f.Message);
			}

			await Task.Delay(500);
		}


		private async Task RefreshDeviceData(object o, DynamicEventArgs dynamicEventArgs) {
			var id = dynamicEventArgs.P1;
			if (string.IsNullOrEmpty(id)) {
				Log.Warning("Can't refresh null device: " + id);
			}

			foreach (var dev in _sDevices) {
				if (dev.Data.Id != id) {
					continue;
				}

				await dev.ReloadData().ConfigureAwait(false);
				if (DeviceMode != DeviceMode.Off && dev.Data.Enable && !dev.Streaming) {
					await dev.StartStream(_targetTokenSource.Token).ConfigureAwait(false);
				}

				if (DeviceMode == DeviceMode.Off || dev.Data.Enable || !dev.Streaming) {
					return;
				}

				Log.Information("Stopping disabled device: " + dev.Id);
				await dev.StopStream().ConfigureAwait(false);

				return;
			}

			var sda = DataUtil.GetDevice(id);

			// If our device is a real boy, start it and add it
			if (sda == null) {
				return;
			}

			var newDev = CreateDevice(sda);
			await newDev.StartStream(_targetTokenSource.Token).ConfigureAwait(false);

			var sDevs = _sDevices.ToList();
			sDevs.Add(newDev);
			_sDevices = sDevs.ToArray();
		}

		private dynamic? CreateDevice(dynamic devData) {
			var classes = SystemUtil.GetClasses<IColorTarget>();
			foreach (var c in classes) {
				try {
					string tag = c.Replace("Glimmr.Models.ColorTarget.", "");
					tag = tag.Split(".")[0];
					if (devData.Tag != tag) {
						continue;
					}

					Log.Debug($"Creating {devData.Tag}: {devData.Id}");
					var args = new object[] {devData, this};
					dynamic? obj = Activator.CreateInstance(Type.GetType(c)!, args);
					return obj;
				} catch (Exception e) {
					Log.Warning("Exception creating device: " + e.Message + " at " + e.StackTrace);
				}
			}

			return null;
		}

		private Task ReloadLedData(object o, DynamicEventArgs dynamicEventArgs) {
			string ledId = dynamicEventArgs.P1;
			foreach (var dev in _sDevices) {
				if (dev.Id == ledId) {
					dev.ReloadData();
				}
			}

			return Task.CompletedTask;
		}


		private void ReloadSystemData() {
			var sd = _systemData;
			_systemData = DataUtil.GetSystemData();
			foreach (var stream in _streams.Values) {
				stream.Refresh(_systemData);
			}
			
			_autoDisableDelay = sd.AutoDisableDelay;
			_enableAutoDisable = _systemData.EnableAutoDisable;

			if (_autoDisableDelay < 1) {
				_autoDisableDelay = 10;
			}

			_captureMode = (CaptureMode) sd.CaptureMode;
		}

		private async Task Mode(object o, DynamicEventArgs dynamicEventArgs) {
			var sd = DataUtil.GetSystemData();
			var newMode = (DeviceMode) dynamicEventArgs.P1;
			DeviceMode = newMode;
			if (newMode != 0 && _autoDisabled) {
				_autoDisabled = false;
				DataUtil.SetItem("AutoDisabled", _autoDisabled);
			}

			_streamTokenSource.Cancel();

			if (_streamStarted && newMode == 0) {
				//if (_streamTask != null && !_streamTask.IsCompleted) _streamTask.Wait();
				await StopStream();
			}
			_streamTokenSource = new CancellationTokenSource();
			if (_streamTokenSource.IsCancellationRequested) {
				Log.Warning("Token source has cancellation requested.");
				//return;
			}
			IColorSource? stream = null;
			if (newMode == DeviceMode.Udp) {
				stream = (StreamMode) sd.StreamMode == StreamMode.DreamScreen ? _streams["DreamScreen"] : _streams["UDP"];
			} else if (newMode != DeviceMode.Off) {
				stream = _streams[newMode.ToString()];
			}

			if (newMode == DeviceMode.Ambient || newMode == DeviceMode.Udp || newMode == DeviceMode.DreamScreen) {
				Splitter.DoSend = true;
			} else {
				Splitter.DoSend = false;
			}

			if (stream != null) {
				Log.Information("Starting stream on " + newMode);
				_streamTask = stream.ToggleStream(_streamTokenSource.Token);
			} else {
				Log.Warning("Unable to acquire stream.");
			}
			
			if (newMode != 0 && !_streamStarted) {
				await StartStream();
			}

			DeviceMode = newMode;
			Log.Information($"Device mode updated to {newMode}.");
		}


		private Task StartStream() {
			if (!_streamStarted) {
				_streamStarted = true;
				Log.Information("Starting streaming targets...");
				foreach (var sDev in _sDevices) {
					try {
						if (sDev.Enable) sDev.StartStream(_targetTokenSource.Token);
					} catch (Exception e) {
						Log.Warning("Exception starting stream: " + e.Message);
					}
				}
				_streamStarted = true;
			}

			if (_streamStarted) {
				Log.Information("Streaming started on all devices.");
			}
			return Task.CompletedTask;
		}

		private Task StopStream() {
			if (!_streamStarted) {
				return Task.CompletedTask;
			}
			Log.Information("Stopping device stream(s)...");
			_streamStarted = false;
			foreach (var dev in _sDevices) {
				if (dev.Enable) dev.StopStream();
			}
			Log.Information("Stream(s) stopped on all devices.");
			return Task.CompletedTask;
		}


		public void SendColors(List<Color> colors, List<Color> sectors, int fadeTime, bool force = false) {
			if (!_streamStarted) {
				return;
			}
			
			Counter.Tick("source");
			ColorSendEvent(colors, sectors, fadeTime, force);
		}


		private static void CancelSource(CancellationTokenSource target, bool dispose = false) {
			if (target == null) {
				return;
			}

			if (!target.IsCancellationRequested) {
				target.CancelAfter(0);
			}

			if (dispose) {
				target.Dispose();
			}
		}

		private async Task StopServices() {
			Log.Information("Stopping color services...");
			await StopStream().ConfigureAwait(false);
			_watch.Stop();
			//_frameWatch.Stop();
			_streamTokenSource.Cancel();
			CancelSource(_targetTokenSource, true);
			foreach (var s in _sDevices) {
				try {
					s.Dispose();
				} catch (Exception e) {
					Log.Warning("Caught exception: " + e.Message);
				}
			}

			Log.Information("All services have been stopped.");
		}

		public void StopDevice(string id, bool remove = false) {
			var devs = new List<IColorTarget>();
			foreach (var dev in _sDevices) {
				if (dev.Id == id) {
					if (dev.Enable && dev.Streaming) {
						dev.StopStream();
					}

					dev.Enable = false;
					if (!remove) {
						devs.Add(dev);
					} else {
						dev.Dispose();
					}
				} else {
					devs.Add(dev);
				}
			}

			_sDevices = devs.ToArray();
		}
	}
}