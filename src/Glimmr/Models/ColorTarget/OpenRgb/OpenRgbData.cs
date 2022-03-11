﻿#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Glimmr.Enums;
using Glimmr.Models.Util;
using Newtonsoft.Json;
using OpenRGB.NET.Enums;
using OpenRGB.NET.Models;

#endregion

namespace Glimmr.Models.ColorTarget.OpenRgb;

public class OpenRgbData : IColorTargetData {
	/// <summary>
	///     Reverse the order of data sent to leds.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool ReverseStrip { get; set; }

	/// <summary>
	///     The order of the color values for the particular device.
	/// </summary>
	[DefaultValue(ColorOrder.Rgb)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public ColorOrder ColorOrder { get; set; } = ColorOrder.Rgb;

	/// <summary>
	///     The OpenRGB device type.
	/// </summary>
	[DefaultValue(DeviceType.Ledstrip)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public DeviceType Type { get; set; }

	/// <summary>
	///     Scale factor for LED counts related to master grid.
	/// </summary>
	[JsonProperty]
	public float LedMultiplier { get; set; } = 1.0f;

	/// <summary>
	///     The index of the active device mode.
	/// </summary>

	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int ActiveModeIndex { get; set; }

	/// <summary>
	///     Device brightness.
	/// </summary>
	[DefaultValue(255)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Brightness { get; set; }

	/// <summary>
	///     OpenRGB Device ID.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int DeviceId { get; set; }

	/// <summary>
	///     Number of LEDs in strip.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public int LedCount { get; set; }


	/// <summary>
	///     Offset of leds from lower-right corner of master grid.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Offset { get; set; }

	/// <summary>
	///     Device rotation.
	/// </summary>
	[DefaultValue(0)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int Rotation { get; set; }

	/// <summary>
	///     Target sector, if using sectored StripMode.
	/// </summary>
	[DefaultValue(-1)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public int TargetSector { get; set; }

	/// <summary>
	///     Device description.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public string Description { get; set; }

	/// <summary>
	///     Device location.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Location { get; set; }

	/// <summary>
	///     Device serial.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Serial { get; set; }


	/// <summary>
	///     Device vendor.
	/// </summary>
	[DefaultValue("Unknown")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Vendor { get; set; }

	/// <summary>
	///     Device version.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Version { get; set; }

	/// <summary>
	///     LED Strip mode.
	///     Normal = 0,
	///     Loop = 2,
	///     Single = 3
	/// </summary>
	[JsonProperty]
	public StripMode StripMode { get; set; }


	public OpenRgbData() {
		Tag = "OpenRgb";
		Name ??= Tag;
		Id = "";
		IpAddress = "";
		Description = "";
		Location = Serial = Vendor = Version = "";
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
		Name = StringUtil.UppercaseFirst(Id);
	}

	public OpenRgbData(Device dev, int index, string ip) {
		Id = "OpenRgb" + index;
		DeviceId = index;
		IpAddress = ip;
		Name = dev.Name;
		Vendor = dev.Vendor;
		Type = dev.Type;
		Description = dev.Description;
		Version = dev.Version;
		Serial = dev.Serial;
		Location = dev.Location;
		ActiveModeIndex = dev.ActiveModeIndex;
		LedCount = dev.Leds.Length;
		Tag = "OpenRgb";
		Brightness = 255;
		LastSeen = DateTime.Now.ToString(CultureInfo.InvariantCulture);
	}


	/// <summary>
	///     Device tag.
	/// </summary>
	[DefaultValue("OpenRgb")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public string Tag { get; set; }


	/// <summary>
	///     Device ID.
	/// </summary>
	[JsonProperty]
	public string Id { get; set; }

	/// <summary>
	///     Device name.
	/// </summary>
	[DefaultValue("")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
	public string Name { get; set; }

	/// <summary>
	///     Device IP Address.
	/// </summary>
	[DefaultValue("127.0.0.1")]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public string IpAddress { get; set; }

	/// <summary>
	///     Enable device for streaming.
	/// </summary>
	[DefaultValue(false)]
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]

	public bool Enable { get; set; }

	/// <summary>
	///     Last time the device was seen during discovery.
	/// </summary>
	[JsonProperty]
	public string LastSeen { get; set; }


	/// <summary>
	///     UI Properties.
	/// </summary>
	public SettingsProperty[] KeyProperties {
		get => Kps();
		set { }
	}

	public void UpdateFromDiscovered(IColorTargetData data) {
		var dev = (OpenRgbData)data;
		IpAddress = dev.IpAddress;
		Name = dev.Name;
		Vendor = dev.Vendor;
		Type = dev.Type;
		Description = dev.Description;
		Version = dev.Version;
		Serial = dev.Serial;
		Location = dev.Location;
		ActiveModeIndex = dev.ActiveModeIndex;
		LedCount = dev.LedCount;
		DeviceId = dev.DeviceId;
	}

	private SettingsProperty[] Kps() {
		var multiplier = new SettingsProperty("LedMultiplier", "ledMultiplier", "");
		var output = StripMode switch {
			StripMode.Single => new[] {
				new SettingsProperty("sectormap", "sectormap", ""),
				new("StripMode", "select", "Strip Mode",
					new Dictionary<string, string> { ["0"] = "Normal", ["3"] = "Single Color" })
			}.ToList(),
			_ => new[] {
				new("ledmap", "ledmap", ""),
				new("StripMode", "select", "Strip Mode",
					new Dictionary<string, string> { ["0"] = "Normal", ["3"] = "Single Color" }),
				new("Offset", "number", "Strip Offset"),
				multiplier,
				new("ReverseStrip", "check", "Reverse Strip") {
					ValueHint = "Reverse the order of the leds to clockwise (facing screen)."
				}
			}.ToList()
		};
		output.Add(new SettingsProperty("ColorOrder", "select", "Color Order", new Dictionary<string, string> {
			["0"] = "RGB",
			["1"] = "RBG",
			["2"] = "GBR",
			["3"] = "GRB",
			["4"] = "BGR",
			["5"] = "BRG"
		}) { ValueHint = "The order in which RGB values are sent to the LED strip." });
		return output.ToArray();
	}

	public bool Matches(Device dev) {
		if (dev.Name != Name) {
			return false;
		}

		if (dev.Vendor != Vendor) {
			return false;
		}

		if (dev.Type != Type) {
			return false;
		}

		if (dev.Description != Description) {
			return false;
		}

		if (dev.Version != Version) {
			return false;
		}

		if (dev.Serial != Serial) {
			return false;
		}

		return dev.Location == Location;
	}
}