﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging.Configuration;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.ColorTarget.Hue;
using Glimmr.Models.ColorTarget.Led;
using Glimmr.Models.ColorTarget.Nanoleaf;
using Glimmr.Models.Util;
using Makaretu.Dns;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Services {
	public class ControlService : BackgroundService {
		public HttpClient HttpSender { get; }
		public UdpClient UdpClient { get; }
		public MulticastService MulticastService { get; }
		private readonly IHubContext<SocketServer> _hubContext;
		private readonly Dictionary<string,dynamic> _agents;

		public ControlService(IHubContext<SocketServer> hubContext) {
			_hubContext = hubContext;
			// Init nano HttpClient
			HttpSender = new HttpClient {Timeout = TimeSpan.FromSeconds(5)};
			// Init UDP client
			UdpClient = new UdpClient {Ttl = 5};
			UdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			UdpClient.Client.Blocking = false;
			UdpClient.DontFragment = true;
			
			MulticastService = new MulticastService();
			
			// Dynamically load agents
			_agents = new Dictionary<string, dynamic>();
			LoadAgents();
		}

		public AsyncEvent<DynamicEventArgs> DeviceReloadEvent;
		public AsyncEvent<DynamicEventArgs> RefreshLedEvent;
		public event Action RefreshSystemEvent = delegate { };
		public AsyncEvent<DynamicEventArgs> DeviceRescanEvent;
		public event ArgUtils.Action DreamSubscribeEvent = delegate { };
		
		public AsyncEvent<DynamicEventArgs> SetModeEvent;

		public AsyncEvent<DynamicEventArgs> TestLedEvent;
		public event Action<CancellationToken> RefreshDreamscreenEvent = delegate { };
		public event Action<string> AddSubscriberEvent = delegate { };
		
		public AsyncEvent<DynamicEventArgs> FlashDeviceEvent;
		
		public AsyncEvent<DynamicEventArgs> FlashSectorEvent;
		
		public AsyncEvent<DynamicEventArgs> DemoLedEvent;
		
		public event Action<List<Color>, List<Color>, int, bool> TriggerSendColorEvent = delegate { };

		public dynamic GetAgent(string classType) {
			foreach (var agentData in _agents) {
				Log.Debug($"Checking {agentData.Key} against {classType}");
				if (agentData.Key == classType) {
					Log.Debug($"Returning {classType}.");
					return agentData.Value;
				}
			}
			Log.Warning($"Error finding agent of type {classType}.");
			return null;
		}

		private void LoadAgents() {
			var types = SystemUtil.GetClasses<IColorTargetAgent>();
			foreach (var c in types) {
				var parts = c.Split(".");
				var shortClass = parts[^1];
				Log.Debug("Creating agent: " + c);
				var agentMaker = (IColorTargetAgent) Activator.CreateInstance(Type.GetType(c)!);
				if (agentMaker == null) {
					Log.Debug("Agent is null!");
				} else {
					var agent = agentMaker?.CreateAgent(this);
					if (agent != null) {
						Log.Debug($"Adding agent: {c}, {shortClass}");
						_agents[shortClass] = agent;
					} else {
						Log.Debug("Agent is null!");
					}
				}
				
			}
		}

		public async Task EnableDevice(string devId) {
			var dev = DataUtil.GetDevice(devId);
			dev.Enable = true;
			await DataUtil.AddDeviceAsync(dev);
			await DeviceReloadEvent.InvokeAsync(this, new DynamicEventArgs(devId));
		}

		public async Task ScanDevices() {
			await DeviceRescanEvent.InvokeAsync(this,null);
		}

		public async Task SetMode(int mode) {
			Log.Information("Setting mode: " + mode);
			if (mode != 0) {
				DataUtil.SetItem("PreviousMode", mode);
			}
			await _hubContext.Clients.All.SendAsync("mode", mode);
			DataUtil.SetItem("DeviceMode", mode);
			await SetModeEvent.InvokeAsync(null, new DynamicEventArgs(mode));
		}

		public async Task AuthorizeHue(string id) {
			Log.Debug("AuthHue called, for real (socket): " + id);
			HueData bd;
			if (!string.IsNullOrEmpty(id)) {
				await _hubContext.Clients.All.SendAsync("hueAuth", "start");
				bd = DataUtil.GetCollectionItem<HueData>("Dev_Hue", id);
				Log.Debug("BD: " + JsonConvert.SerializeObject(bd));
				if (bd == null) {
					Log.Debug("Null bridge retrieved.");
					await _hubContext.Clients.All.SendAsync("hueAuth", "stop");
					return;
				}

				if (bd.Key != null && bd.User != null) {
					Log.Debug("Bridge is already authorized.");
					await _hubContext.Clients.All.SendAsync("hueAuth", "authorized");
					await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					return;
				}
			} else {
				Log.Warning("Null value.");
				await _hubContext.Clients.All.SendAsync("hueAuth", "stop");
				return;
			}

			Log.Debug("Trying to retrieve app key...");
			var count = 0;
			while (count < 30) {
				count++;
				try {
					var appKey = HueDiscovery.CheckAuth(bd.IpAddress).Result;
					Log.Debug("App key retrieved! " + JsonConvert.SerializeObject(appKey));
					if (appKey != null) {
						if (!string.IsNullOrEmpty(appKey.StreamingClientKey)) {
							Log.Debug("Updating bridge?");
							bd.Key = appKey.StreamingClientKey;
							bd.User = appKey.Username;
							Log.Debug("Creating new bridge...");
							// Need to grab light group stuff here
							var nhb = new HueDevice(bd);
							bd = nhb.RefreshData().Result;
							nhb.Dispose();
							await DataUtil.InsertCollection<HueData>("Dev_Hue", bd);
							await _hubContext.Clients.All.SendAsync("hueAuth", "authorized");
							await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
							return;
						}

						Log.Debug("App key is null?");
					}

					Log.Debug("Waiting for app key.");
				} catch (NullReferenceException) {
					Log.Error("Null exception.");
				}

				await _hubContext.Clients.All.SendAsync("hueAuth", count);
				Thread.Sleep(1000);
			}

			Log.Debug("We should be authorized, returning.");
		}

		public void TriggerImageUpdate() {
			_hubContext.Clients.All.SendAsync("loadPreview");
		}

		public async Task TestLights(int led) {
			await TestLedEvent.InvokeAsync(this, new DynamicEventArgs(led));
		}

		public void AddSubscriber(string ip) {
			AddSubscriberEvent(ip);
		}


		/// <summary>
		///     Call this to trigger device refresh
		/// </summary>
		public async Task RefreshDevice(string id) {
			await DeviceReloadEvent.InvokeAsync(this, new DynamicEventArgs(id));
		}


		private async Task RefreshLedData(string ldId) {
			await RefreshLedEvent.InvokeAsync(this, new DynamicEventArgs(ldId));
		}

		// We call this one to send colors to everything, including the color service
		public void SendColors(List<Color> c1, List<Color> c2, int fadeTime = 0, bool force=false) {
			TriggerSendColorEvent(c1, c2, fadeTime, force);
		}

		
		public void TriggerDreamSubscribe() {
			DreamSubscribeEvent();
		}


		public async Task NotifyClients() {
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			return Task.Run(async () => {
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(1, stoppingToken);
				}
				return Task.CompletedTask;
			}, stoppingToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Debug("Stopping control service...");
			HttpSender?.Dispose();
			UdpClient?.Dispose();
			MulticastService?.Dispose();
			foreach (dynamic agent in _agents) {
				try {
					var type = agent.GetType();
					if (type.GetMethod("Dispose") != null) {
						agent.Dispose();
					}
				} catch {
					// Ignored
				}
			}
			Log.Debug("Control service stopped.");
			return base.StopAsync(cancellationToken);
		}

		
		public void RefreshDreamscreen(in CancellationToken csToken) {
			RefreshDreamscreenEvent(csToken);
		}

		public async Task AddDevice(IColorTargetData data) {
			await DataUtil.AddDeviceAsync(data);
			Log.Debug("Device added...");
		}

		public async Task AuthorizeNano(string id) {
			var leaf = DataUtil.GetCollectionItem<NanoleafData>("Dev_Nano", id);
			bool doAuth = leaf.Token == null;
			if (doAuth) {
				await _hubContext.Clients.All.SendAsync("nanoAuth", "authorized");
				await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
				return;
			}

			var panel = new NanoleafDevice(leaf, this);
			var count = 0;
			while (count < 30) {
				var appKey = panel.CheckAuth().Result;
				if (appKey != null) {
					leaf.Token = appKey.Token;
					DataUtil.InsertCollection<NanoleafData>("Dev_Nanoleaf", leaf);
					await _hubContext.Clients.All.SendAsync("nanoAuth", "authorized");
					await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized());
					panel.Dispose();
					return;
				}

				await _hubContext.Clients.All.SendAsync("nanoAuth", count);
				Thread.Sleep(1000);
				count++;
			}

			await _hubContext.Clients.All.SendAsync("nanoAuth", "stop");

			panel.Dispose();
		}

		public async Task UpdateLed(LedData ld) {
			Log.Debug("Got LD from post: " + JsonConvert.SerializeObject(ld));
			await DataUtil.InsertCollection<LedData>("LedData", ld);
			await RefreshLedData(ld.Id);
			await NotifyClients();
		}

		public async Task UpdateSystem(SystemData sd) {
			SystemData oldSd = DataUtil.GetObject<SystemData>("SystemData");
			if (oldSd.LedCount != sd.LedCount) {
				var leds = DataUtil.GetDevices<LedData>("Led");
				
				foreach (var colorTargetData in leds.Where(led => led.LedCount == 0)) {
					var led = colorTargetData;
					led.LedCount = sd .LedCount;
					await DataUtil.InsertCollection<LedData>("LedData", led);
				}
			}
			
			DataUtil.SetObject<SystemData>("SystemData", sd);
			RefreshSystemEvent();
		}

		public static Task SystemControl(string action) {
			Log.Debug("Action triggered: " + action);
			switch (action) {
				case "restart":
					SystemUtil.Restart();
					break;
				case "shutdown":
					SystemUtil.Shutdown();
					break;
				case "reboot":
					SystemUtil.Reboot();
					break;
				case "update":
					SystemUtil.Update();
					break;
			}
			return Task.CompletedTask;
		}

		public async Task UpdateDevice(dynamic device) {
			Log.Debug("Update device called: " + JsonConvert.SerializeObject(device));
			await DataUtil.AddDeviceAsync(device);
			await RefreshDevice(device.Id);
			
		}

		public async Task FlashDevice(string deviceId) {
			await FlashDeviceEvent.InvokeAsync(this, new DynamicEventArgs(deviceId));
		}

		public async Task FlashSector(int sector) {
			await FlashSectorEvent.InvokeAsync(this, new DynamicEventArgs(sector));
		}

		public async Task DemoLed(string id) {
			await DemoLedEvent.InvokeAsync(this, new DynamicEventArgs(id));
		}
	}
}