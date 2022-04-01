﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using Glimmr.Models.Data;

#endregion

namespace Glimmr.Models.Util;

public static class ColorUtil {
	/// <summary>
	///     Take a n-color list, and convert down to 12 for DS
	/// </summary>
	/// <param name="input">The colors from anywhere else</param>
	/// <returns>12 colors averaged from those, or something.</returns>
	public static Color[] TruncateColors(Color[] input) {
		var indices = input.Length / 12;
		var output = EmptyColors(12);
		for (var i = 0; i < 12; i++) {
			double idx = i * indices;
			idx = Math.Floor(idx);
			if (idx >= input.Length) {
				idx = input.Length - 1;
			}

			output[i] = input[(int)idx];
		}

		return output;
	}

	public static Color[] TruncateColors(Color[] input, int offset, int len, float multiplier = 1f) {
		if (offset >= input.Length) {
			while (offset >= input.Length) {
				offset -= input.Length;
			}
		}

		if (offset < 0 && Math.Abs(offset) >= input.Length) {
			offset += input.Length;
		}

		if (offset < 0) {
			offset = input.Length - Math.Abs(offset);
		}

		var output = new Color[len];
		var total = Convert.ToInt32((len + offset) * multiplier);

		var doubled = new Color[total];
		var dIdx = 0;
		while (dIdx < total) {
			foreach (var t in input) {
				doubled[dIdx] = t;
				dIdx++;
				if (dIdx >= total) {
					break;
				}
			}
		}

		for (var i = 0; i < len; i++) {
			var tgt = Convert.ToInt32(offset + i * multiplier);
			if (tgt >= total) {
				tgt = total - 1;
			}

			output[i] = doubled[tgt];
		}

		return output;
	}


	/// <summary>
	///     Return the average of inputted colors
	/// </summary>
	/// <param name="colors"></param>
	/// <returns></returns>
	private static Color AverageColors(params Color[] colors) {
		var inputCount = colors.Length;
		if (inputCount == 0) {
			return Color.FromArgb(0, 0, 0, 0);
		}

		var avgG = 0;
		var avgB = 0;
		var avgR = 0;
		var avgA = 0;
		foreach (var t in colors) {
			avgG += t.G * t.G;
			avgB += t.B * t.B;
			avgR += t.R * t.R;
			avgA += t.A * t.A;
		}

		avgG /= inputCount;
		avgB /= inputCount;
		avgR /= inputCount;
		avgA /= inputCount;
		return Color.FromArgb((int)Math.Sqrt(avgA), (int)Math.Sqrt(avgR), (int)Math.Sqrt(avgB),
			(int)Math.Sqrt(avgG));
	}

	public static Color ClampAlpha(Color tCol) {
		var rI = tCol.R;
		var gI = tCol.G;
		var bI = tCol.B;
		float tM = Math.Max(rI, Math.Max(gI, bI));
		float tm = Math.Min(rI, Math.Min(gI, bI));
		//If the maximum value is 0, immediately return pure black.
		if (tM == 0) {
			return Color.FromArgb(0, 0, 0, 0);
		}

		if (tm >= 255) {
			return Color.FromArgb(255, 0, 0, 0);
		}

		//This section serves to figure out what the color with 100% hue is
		var multiplier = 255.0f / tM;
		var hR = rI * multiplier;
		var hG = gI * multiplier;
		var hB = bI * multiplier;

		//This calculates the Whiteness (not strictly speaking Luminance) of the color
		var maxWhite = Math.Max(hR, Math.Max(hG, hB));
		var minWhite = Math.Min(hR, Math.Min(hG, hB));
		var luminance = ((maxWhite + minWhite) / 2.0f - 127.5f) * (255.0f / 127.5f) / multiplier;

		//Calculate the output values
		var wO = Convert.ToInt32(luminance);
		var bO = Convert.ToInt32(bI - luminance);
		var rO = Convert.ToInt32(rI - luminance);
		var gO = Convert.ToInt32(gI - luminance);

		//Trim them so that they are all between 0 and 255
		if (wO < 0) {
			wO = 0;
		}

		if (bO < 0) {
			bO = 0;
		}

		if (rO < 0) {
			rO = 0;
		}

		if (gO < 0) {
			gO = 0;
		}

		if (wO > 255) {
			wO = 255;
		}

		if (bO > 255) {
			bO = 255;
		}

		if (rO > 255) {
			rO = 255;
		}

		if (gO > 255) {
			gO = 255;
		}

		return Color.FromArgb(wO, rO, gO, bO);
	}


	/// <summary>
	///     Convert HSV values to color
	/// </summary>
	/// <param name="hue">0-360</param>
	/// <param name="saturation">0-1</param>
	/// <param name="value">0-1</param>
	/// <returns></returns>
	public static Color HsvToColor(double hue, double saturation, double value) {
		var hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
		var f = hue / 60 - Math.Floor(hue / 60);

		value *= 255;
		var v = Convert.ToInt32(value);
		var p = Convert.ToInt32(value * (1 - saturation));
		var q = Convert.ToInt32(value * (1 - f * saturation));
		var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

		return hi switch {
			0 => Color.FromArgb(255, v, t, p),
			1 => Color.FromArgb(255, q, v, p),
			2 => Color.FromArgb(255, p, v, t),
			3 => Color.FromArgb(255, p, q, v),
			4 => Color.FromArgb(255, t, p, v),
			_ => Color.FromArgb(255, v, p, q)
		};
	}

	public static Color SetBrightness(Color color, float brightness) {
		// var hsb = ColorToHsb(input);
		if (brightness == 0) {
			return Color.FromArgb(0, 0, 0);
		}

		// return HsbToColor(hsb[0], hsb[1], brightness);
		var red = (float)color.R;
		var green = (float)color.G;
		var blue = (float)color.B;

		var existing = color.GetBrightness();
		if (existing > brightness) {
			var diff = existing - brightness;
			red -= diff;
			green -= diff;
			blue -= diff;
			red = Math.Max(red, 0);
			green = Math.Max(green, 0);
			blue = Math.Max(blue, 0);
		}

		if (!(existing < brightness)) {
			return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
		}

		{
			var diff = brightness - existing;
			red += diff;
			green += diff;
			blue += diff;
			red = Math.Min(red, 255);
			green = Math.Min(green, 255);
			blue = Math.Min(blue, 255);
		}

		return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
	}


	public static Color[] FillArray(Color input, int len) {
		var output = new Color[len];
		for (var i = 0; i < len; i++) {
			output[i] = input;
		}

		return output;
	}

	public static Color Rainbow(float progress) {
		var div = Math.Abs(progress % 1) * 6;
		var ascending = (int)(div % 1 * 255);
		var descending = 255 - ascending;
		const int alpha = 0;
		return (int)div switch {
			0 => Color.FromArgb(alpha, 255, ascending, 0),
			1 => Color.FromArgb(alpha, descending, 255, 0),
			2 => Color.FromArgb(alpha, 0, 255, ascending),
			3 => Color.FromArgb(alpha, 0, descending, 255),
			4 => Color.FromArgb(alpha, ascending, 0, 255),
			_ => Color.FromArgb(alpha, 255, 0, descending)
		};
	}

	public static Color[] EmptyColors(int len) {
		var output = new Color[len];
		var empty = Color.FromArgb(0, 0, 0, 0);
		for (var i = 0; i < len; i++) {
			output[i] = empty;
		}

		return output;
	}

	public static List<Color> EmptyList(int size) {
		var output = new List<Color>(size);
		for (var i = 0; i < size; i++) {
			output.Add(Color.FromArgb(0, 0, 0, 0));
		}

		return output;
	}

	public static byte[] GammaTable(float gamma) {
		var GammaCorrection = new byte[256];
		var logBS = new int[256];
		for (var i = 0; i < 256; i++) {
			GammaCorrection[i] = (byte)i;
			logBS[i] = i;
		}

		if (!(gamma > 1.0f)) {
			return GammaCorrection;
		}

		{
			for (var i = 0; i < 256; i++) {
				GammaCorrection[i] = (byte)(Math.Pow(i / (float)255, gamma) * 255 + 0.5);
				logBS[i] = GammaCorrection[i];
			}
		}

		return GammaCorrection;
	}

	public static float HueFromFrequency(float frequency, int octave) {
		var start = new[] { 16.35f, 32.7f, 65.41f, 130.81f, 261.63f, 523.25f, 1046.5f, 2093f, 4186.01f };
		var end = new[] { 30.87f, 61.74f, 123.47f, 246.94f, 493.88f, 987.77f, 1975.53f, 3951.07f, 7902.13f };
		var minFrequency = start[octave];
		var maxFrequency = end[octave];

		var range = maxFrequency - minFrequency;
		var location = frequency / range;
		return location;
	}

	public static List<Color> LedsToSectors(List<Color> ledColors, SystemData sd) {
		var rightColors = ledColors.GetRange(0, sd.RightCount);
		var topColors = ledColors.GetRange(sd.RightCount - 1, sd.TopCount);
		var leftColors = ledColors.GetRange(sd.TopCount - 1, sd.LeftCount);
		var bottomColors = ledColors.GetRange(sd.LeftCount - 1, sd.BottomCount);
		var rStep = (float)rightColors.Count / sd.VSectors;
		var tStep = (float)topColors.Count / sd.HSectors;
		var lStep = (float)leftColors.Count / sd.VSectors;
		var bStep = (float)bottomColors.Count / sd.HSectors;
		var output = new List<Color>();
		var toAvg = new List<Color>();
		// Add the last range of colors from the bottom to sector 0
		for (var i = bottomColors.Count - 1 - bStep; i < bottomColors.Count; i++) {
			toAvg.Add(bottomColors[(int)i]);
		}

		var idx = 0;
		while (idx < rightColors.Count && output.Count <= sd.VSectors) {
			toAvg.AddRange(rightColors);

			// On the last sector, don't average it so we can add the bit from the next corner
			if (idx % rStep == 0 && output.Count < sd.VSectors) {
				output.Add(AverageColors(toAvg.ToArray()));
				toAvg = new List<Color>();
			}

			idx++;
		}

		idx = 0;
		while (idx < topColors.Count && output.Count < sd.VSectors + sd.HSectors - 1) {
			toAvg.AddRange(topColors);

			if (idx % tStep == 0) {
				output.Add(AverageColors(toAvg.ToArray()));
				toAvg = new List<Color>();
			}

			idx++;
		}

		idx = 0;
		while (idx < leftColors.Count && output.Count < sd.VSectors + sd.HSectors + sd.VSectors - 2) {
			toAvg.AddRange(leftColors);

			if (idx % lStep == 0) {
				output.Add(AverageColors(toAvg.ToArray()));
				toAvg = new List<Color>();
			}

			idx++;
		}

		idx = 0;
		while (idx < bottomColors.Count && output.Count < sd.SectorCount) {
			toAvg.AddRange(bottomColors);

			if (idx % bStep == 0) {
				output.Add(AverageColors(toAvg.ToArray()));
				toAvg = new List<Color>();
			}

			idx++;
		}

		return output;
	}


	/// <summary>
	///     Adjust the brightness of a list of colors
	/// </summary>
	/// <param name="toSend">Input colors</param>
	/// <param name="max">A float from 0-1, representing the max percentage brightness can be represented.</param>
	/// <returns></returns>
	public static Color[] AdjustBrightness(Color[] toSend, float max) {
		var output = new Color[toSend.Length];
		var mc = (byte)(max * 255f);
		for (var i = 0; i < toSend.Length; i++) {
			var color = toSend[i];
			// Max value is the brightness
			var colM = Math.Max(color.R, Math.Max(color.G, color.B));
			// Divide by 255 and multiply by max we can have...
			var cMax = (byte)(colM / 255f * mc);
			var diff = 0;
			if (colM > cMax) {
				diff = colM - cMax;
			}

			var r = Math.Max(0, color.R - diff);
			var g = Math.Max(0, color.G - diff);
			var b = Math.Max(0, color.B - diff);
			output[i] = Color.FromArgb(r, g, b);
		}

		return output;
	}

	public static Color ClampBrightness(Color col, int dataBrightness) {
		var max = Math.Max(col.R, Math.Max(col.G, col.B));
		if (max <= dataBrightness) {
			return col;
		}

		var diff = max - dataBrightness;
		var r = Math.Max(col.R - diff, 0);
		var g = Math.Max(col.G - diff, 0);
		var b = Math.Max(col.B - diff, 0);
		return Color.FromArgb(r, g, b);
	}
}