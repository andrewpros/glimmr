﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Glimmr.Models.Util;
using Glimmr.Services;
using LifxNetPlus;
using Microsoft.VisualBasic.CompilerServices;
using Serilog;
using Color = System.Drawing.Color;

namespace Glimmr.Models.ColorTarget.Lifx {
    public class LifxDevice : ColorTarget, IColorTarget {
        public bool Enable { get; set; }
        public bool Online { get; set; }

        IColorTargetData IColorTarget.Data {
            get => Data;
            set => Data = (LifxData) value;
        }

        public LifxData Data { get; set; }
        private LightBulb B { get; }
        public bool Streaming { get; set; }
        public bool Testing { get; set; }

        private int _targetSector;
        private bool _hasMulti;
        private int _multizoneCount;
        private int _offset;
        private bool _reverseStrip;
        public int Brightness { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string Tag { get; set; }

        private readonly LifxClient _client;
        
        public LifxDevice(LifxData d, ColorService colorService) : base(colorService) {
            DataUtil.GetItem<int>("captureMode");
            Data = d ?? throw new ArgumentException("Invalid Data");
            _hasMulti = d.HasMultiZone;
            _offset = d.Offset;
            _reverseStrip = d.ReverseStrip;
            if (_hasMulti) _multizoneCount = d.MultiZoneCount;
            _client = colorService.ControlService.GetAgent("LifxAgent");
            colorService.ColorSendEvent += SetColor;
            B = new LightBulb(d.HostName, d.MacAddress, d.Service, (uint)d.Port);
            _targetSector = d.TargetSector - 1;
            Brightness = d.Brightness;
            Id = d.Id;
            IpAddress = d.IpAddress;
            Enable = Data.Enable;
            Online = SystemUtil.IsOnline(IpAddress);
        }

        public async Task StartStream(CancellationToken ct) {
            if (!Enable) return;
            Log.Debug("Lifx: Starting stream.");
            var col = new LifxColor(0, 0, 0);
            //var col = new LifxColor {R = 0, B = 0, G = 0};
            _client.SetLightPowerAsync(B, true);
            _client.SetColorAsync(B, col, 2700);
            Log.Debug($"Lifx: Streaming is active, {_hasMulti} {_multizoneCount}");
            Streaming = true;
            await Task.FromResult(Streaming);
        }

        public async Task FlashColor(Color color) {
            var nC = new LifxColor(color);
            //var nC = new LifxColor {R = color.R, B = color.B, G = color.G};
            await _client.SetColorAsync(B, nC).ConfigureAwait(false);
        }

        public bool IsEnabled() {
            return Enable;
        }

        
        public async Task StopStream() {
            if (!Enable || !Online) return;
            Streaming = false;
            if (_client == null) throw new ArgumentException("Invalid lifx client.");
            FlashColor(Color.FromArgb(0, 0, 0)).ConfigureAwait(false);
            _client.SetLightPowerAsync(B, Data.Power).ConfigureAwait(false);
            await Task.FromResult(true);
            Log.Debug("Lifx: Stream stopped.");
        }

        public Task ReloadData() {
            var newData = DataUtil.GetCollectionItem<LifxData>("Dev_Lifx", Id);
            DataUtil.GetItem<int>("captureMode");
            Data = newData;
            _hasMulti = Data.HasMultiZone;
            _offset = Data.Offset;
            _reverseStrip = Data.ReverseStrip;
            if (_hasMulti) _multizoneCount = Data.MultiZoneCount;

            IpAddress = Data.IpAddress;
            var targetSector = newData.TargetSector;
            _targetSector = targetSector - 1;
            var oldBrightness = Brightness;
            Brightness = newData.MaxBrightness;
            if (oldBrightness != Brightness) {
                var bri = Brightness / 100 * 255;
                _client.SetBrightnessAsync(B, (ushort) bri).ConfigureAwait(false);
            }
            Id = newData.Id;
            Enable = Data.Enable;
            return Task.CompletedTask;
        }

        public void Dispose() {
            
        }

        public void SetColor(List<Color> colors, List<Color> list, int arg3, bool force=false) {
            if (!Streaming || !Enable || Testing && !force) return;
            if (_hasMulti) {
                SetColorMulti(colors);
            } else {
                SetColorSingle(list);
            }
            ColorService.Counter.Tick(Id);
        }

        private void SetColorMulti(List<Color> colors) {
            if (colors == null || _client == null) {
                Log.Warning("Null client or no colors!");
                return;
            }

            var output = ColorUtil.TruncateColors(colors, _offset, _multizoneCount);
            if (_reverseStrip) output.Reverse();
            

            var cols = output.Select(col => new LifxColor(col)).ToList();
            _client.SetExtendedColorZonesAsync(B, cols).ConfigureAwait(false);
        }

        private void SetColorSingle(List<Color> list) {
            
            var sectors = list;
            if (sectors == null || _client == null) {
                return;
            }

            if (_targetSector >= sectors.Count) return;
            
            var input = sectors[_targetSector];
            
            var nC = new LifxColor(input);
            //var nC = new LifxColor {R = input.R, B = input.B, G = input.G};

            _client.SetColorAsync(B, nC).ConfigureAwait(false);
            ColorService.Counter.Tick(Id);
        }
        
    }
}