﻿using System;
using System.ComponentModel;
using Glimmr.Models.Util;
using LifxNet;
using LiteDB;
using Newtonsoft.Json;

namespace Glimmr.Models.ColorTarget.LIFX {
	public class LifxData : StreamingData {
		[BsonCtor] [JsonProperty] public string HostName { get; internal set; }
        
		[JsonProperty] public byte Service { get; internal set; }
		[JsonProperty] public int Port { get; internal set; }
		[JsonProperty] internal DateTime LastSeen { get; set; }
		[JsonProperty] public byte[] MacAddress { get; internal set; }

		[JsonProperty] public string MacAddressString { get; internal set; }

		[JsonProperty] public ushort Hue { get; set; }
		[JsonProperty] public ushort Saturation { get; set; }
		[JsonProperty] public ushort Kelvin { get; set; }
		[JsonProperty] public bool Power { get; set; }

		[DefaultValue(-1)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int TargetSector { get; set; } = -1;

		[DefaultValue(100)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MaxBrightness { get; set; } = 255;
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool HasMultiZone { get; set; }
		
		[DefaultValue(8)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MultiZoneCount { get; set; }
		
		[DefaultValue(0)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ProductId { get; set; }
		
		[DefaultValue(false)]
		[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool MultiZoneV2 { get; set; }
		public LifxData() {
			Tag = "Lifx";
			if (Id == null && MacAddressString != null) {
				Id = MacAddressString;
			}
			Name ??= Tag;
			if (Id != null && Id.Length > 4) Name = "Lifx - " + Id.Substring(0, 4);
		}

		public LifxData(LightBulb b) {
			if (b == null) throw new ArgumentException("Invalid bulb data.");
			Tag = "Lifx";
			Name ??= Tag;
			HostName = b.HostName;
			IpAddress = IpUtil.GetIpFromHost(HostName).ToString();
			Service = b.Service;
			Port = (int) b.Port;
			MacAddress = b.MacAddress;
			MacAddressString = b.MacAddressName;
			Id = MacAddressString;
			if (Id != null) Name = "Lifx - " + Id.Substring(0, 4);
		}

		

	}
}