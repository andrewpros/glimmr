﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HueDream.Models.Util;
using LifxNet;

namespace HueDream.Models.StreamingDevice.LIFX {
    public sealed class LifxDiscovery {
        private readonly LifxClient client;
        private List<LightBulb> bulbs;

        public LifxDiscovery(LifxClient client) {
        }

        public async Task<List<LifxData>> Discover(int timeOut) {
            if (client == null) return new List<LifxData>();
            bulbs = new List<LightBulb>();
            client.DeviceDiscovered += Client_DeviceDiscovered;
            client.StartDeviceDiscovery();
            LogUtil.Write("Lifx: Discovery started.");
            await Task.Delay(timeOut * 1000);
            LogUtil.Write("Discovery completed.");
            client.StopDeviceDiscovery();
            return bulbs.Select(GetBulbInfo).ToList();
        }

        public async Task<List<LifxData>> Refresh(CancellationToken ct) {
            var foo = Task.Run(() => Discover(5), ct);
            var b = await foo;
            foreach (var bulb in b) {
                var existing = DataUtil.GetCollectionItem<LifxData>("lifxBulbs", bulb.MacAddressString);
                if (existing != null) {
                    bulb.SectorMapping = existing.SectorMapping;
                    bulb.Brightness = existing.Brightness;
                }
                DataUtil.InsertCollection<LifxData>("lifxBulbs", bulb);
            }
            return DataUtil.GetCollection<LifxData>("lifxBulbs");
        }

        private void Client_DeviceDiscovered(object sender, LifxClient.DeviceDiscoveryEventArgs e) {
            var bulb = e.Device as LightBulb;
            LogUtil.Write("Bulb discovered?");
            bulbs.Add(bulb);
        }

        public LifxData GetBulbInfo(LightBulb b) {
            var state = client.GetLightStateAsync(b).Result;
            var d = new LifxData(b) {
                Power = client.GetLightPowerAsync(b).Result,
                Hue = state.Hue,
                Saturation = state.Saturation,
                Brightness = state.Brightness,
                Kelvin = state.Kelvin,
                SectorMapping = -1
            };
            return d;
        }
    }
}