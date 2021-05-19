﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Hubs;
using Glimmr.Models;
using Glimmr.Models.ColorTarget;
using Glimmr.Models.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Glimmr.Services {
	public class DiscoveryService : BackgroundService {
		private readonly List<IColorDiscovery> _discoverables;
		private readonly IHubContext<SocketServer> _hubContext;
		private bool _discovering;
		private int _discoveryInterval;
		private CancellationTokenSource _mergeSource;
		private CancellationToken _stopToken;
		private bool _streaming;
		private readonly CancellationTokenSource _syncSource;

		public DiscoveryService(IHubContext<SocketServer> hubContext, ColorService colorService) {
			_hubContext = hubContext;
			var controlService = colorService.ControlService;
			controlService.DeviceRescanEvent += TriggerRefresh;
			controlService.SetModeEvent += UpdateMode;
			controlService.RefreshSystemEvent += RefreshSystem;
			_discoveryInterval = DataUtil.GetItem<int>("AutoDiscoveryFrequency");
			if (_discoveryInterval < 15) {
				_discoveryInterval = 15;
			}

			var classnames = SystemUtil.GetClasses<IColorDiscovery>();
			_discoverables = new List<IColorDiscovery>();
			_syncSource = new CancellationTokenSource();
			foreach (var c in classnames) {
				var dev = (IColorDiscovery) Activator.CreateInstance(Type.GetType(c)!, colorService);
				_discoverables.Add(dev);
			}
		}

		private void RefreshSystem() {
			_syncSource.Cancel();
			_mergeSource = CancellationTokenSource.CreateLinkedTokenSource(_syncSource.Token, _stopToken);
			_discoveryInterval = DataUtil.GetItem<int>("AutoDiscoveryFrequency");
			if (_discoveryInterval < 15) {
				_discoveryInterval = 15;
			}
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) {
			_stopToken = stoppingToken;
			_mergeSource = CancellationTokenSource.CreateLinkedTokenSource(_syncSource.Token, _stopToken);
			return Task.Run(async () => {
				Log.Information("Starting discovery service loop...");
				while (!stoppingToken.IsCancellationRequested) {
					await Task.Delay(TimeSpan.FromMinutes(_discoveryInterval), _mergeSource.Token);
					Log.Information("Auto-refreshing devices...");
					if (!_streaming) {
						await TriggerRefresh(this, null);
					}
				}

				return Task.CompletedTask;
			}, stoppingToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken) {
			Log.Information("Discovery service stopped.");
			return base.StopAsync(cancellationToken);
		}

		private Task UpdateMode(object o, DynamicEventArgs dynamicEventArgs) {
			_streaming = dynamicEventArgs.P1 != 0;
			return Task.CompletedTask;
		}


		private async Task TriggerRefresh(object o, DynamicEventArgs dynamicEventArgs) {
			var cs = new CancellationTokenSource();
			var sd = DataUtil.GetSystemData();
			var timeout = sd.DiscoveryTimeout;
			if (timeout < 3) {
				timeout = 3;
			}

			cs.CancelAfter(TimeSpan.FromSeconds(timeout));
			await DeviceDiscovery(cs.Token, timeout);
			var devs = DataUtil.GetDevices();
			foreach (var dev in devs) {
				var device = (IColorTargetData) dev;
				var lastSeen = DateTime.Parse(device.LastSeen, CultureInfo.InvariantCulture);
				if (DateTime.Now - lastSeen < TimeSpan.FromDays(7)) {
					continue;
				}

				bool online = SystemUtil.IsOnline(dev.IpAddress);
				if (online) {
					dev.LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
					DataUtil.AddDeviceAsync(dev);
				}

				DataUtil.DeleteDevice(device.Id);
			}

			cs.Dispose();
		}


		private async Task DeviceDiscovery(CancellationToken token, int timeout) {
			if (_discovering) {
				return;
			}

			_discovering = true;
			var tasks = _discoverables
				.Select(disco => Task.Run(() => disco.Discover(token, timeout), CancellationToken.None)).ToList();

			try {
				await Task.WhenAll(tasks);
			} catch (Exception e) {
				Log.Warning($"Exception during discovery: {e.Message}" + e.StackTrace);
			} finally {
				foreach (var task in tasks) {
					task.Dispose();
				}
			}

			Log.Information("All devices should now be refreshed.");
			_discovering = false;
			// Notify all clients to refresh data
			await _hubContext.Clients.All.SendAsync("olo", DataUtil.GetStoreSerialized(), CancellationToken.None);
		}
	}
}