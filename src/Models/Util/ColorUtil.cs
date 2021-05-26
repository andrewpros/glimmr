﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Glimmr.Enums;
using Serilog;

namespace Glimmr.Models.Util {
	public static class ColorUtil {
		private static double Tolerance
			=> 0.000000000000001;

		private static CaptureMode _captureMode;
		private static int _sectorCount;

		public static void ColorToHsv(Color color, out double hue, out double saturation, out double value) {
			double max = Math.Max(color.R, Math.Max(color.G, color.B));
			double min = Math.Min(color.R, Math.Min(color.G, color.B));

			hue = color.GetHue();
			saturation = max == 0 ? 0 : 1d - min / max;
			value = max / 255d;
		}

		/// <summary>
		///     Take a n-color list, and convert down to 12 for DS
		/// </summary>
		/// <param name="input">The colors from anywhere else</param>
		/// <returns>12 colors averaged from those, or something.</returns>
		public static List<Color> TruncateColors(List<Color> input) {
			var indices = input.Count / 12;
			var output = new List<Color>();
			for (var i = 0; i < 12; i++) {
				double idx = i * indices;
				idx = Math.Floor(idx);
				if (idx >= input.Count) {
					idx = input.Count - 1;
				}

				output.Add(input[(int) idx]);
			}

			return output;
		}

		public static List<Color> SectorToSquare(List<Color> input) {
			return input;
		}

		public static Color[] TruncateColors(List<Color> input, int offset, int len) {
			var output = new Color[len];

			// Instead of doing dumb crap, just make our list of colors loop around
			var total = len + offset;
			var doubled = new Color[total];
			var c = 0;
			while (c < total) {
				foreach (var col in input) {
					if (c < total) {
						doubled[c] = col;
					} else {
						break;
					}
					c++;
				}
			}

			var idx = 0;
			for (var i = offset; i < total; i++) {
				output[idx] = doubled[i];
				idx++;
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
			return Color.FromArgb((int) Math.Sqrt(avgA), (int) Math.Sqrt(avgR), (int) Math.Sqrt(avgB),
				(int) Math.Sqrt(avgG));
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

			value = value * 255;
			var v = Convert.ToInt32(value);
			var p = Convert.ToInt32(value * (1 - saturation));
			var q = Convert.ToInt32(value * (1 - f * saturation));
			var t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

			switch (hi) {
				case 0:
					return Color.FromArgb(255, v, t, p);
				case 1:
					return Color.FromArgb(255, q, v, p);
				case 2:
					return Color.FromArgb(255, p, v, t);
				case 3:
					return Color.FromArgb(255, p, q, v);
				case 4:
					return Color.FromArgb(255, t, p, v);
				default:
					return Color.FromArgb(255, v, p, q);
			}
		}

		public static Color SetBrightness(Color color, float brightness) {
			// var hsb = ColorToHsb(input);
			if (brightness == 0) {
				return Color.FromArgb(0, 0, 0);
			}

			// return HsbToColor(hsb[0], hsb[1], brightness);
			var red = (float) color.R;
			var green = (float) color.G;
			var blue = (float) color.B;

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

			if (existing < brightness) {
				var diff = brightness - existing;
				red += diff;
				green += diff;
				blue += diff;
				red = Math.Min(red, 255);
				green = Math.Min(green, 255);
				blue = Math.Min(blue, 255);
			}

			return Color.FromArgb(color.A, (int) red, (int) green, (int) blue);
		}

		public static double[] ColorToHsb(Color rgb) {
			// normalize red, green and blue values
			var r = rgb.R / 255.0;
			var g = rgb.G / 255.0;
			var b = rgb.B / 255.0;

			var max = Math.Max(r, Math.Max(g, b));
			var min = Math.Min(r, Math.Min(g, b));

			var h = 0.0;
			if (max <= r && g >= b) {
				h = 60 * (g - b) / (max - min);
			} else if (max <= r && g < b) {
				h = 60 * (g - b) / (max - min) + 360;
			} else if (max >= g) {
				h = 60 * (b - r) / (max - min) + 120;
			} else if (max >= b) {
				h = 60 * (r - g) / (max - min) + 240;
			}

			var s = max <= 0.0000001 ? 0.0 : 1.0 - min / max;
			return new[] {
				h,
				s,
				max
			};
		}

		/// <summary>
		///     Converts HSB to RGB, with a specified output Alpha.
		///     Arguments are limited to the defined range:
		///     does not raise exceptions.
		/// </summary>
		/// <param name="h">Hue, must be in [0, 360].</param>
		/// <param name="s">Saturation, must be in [0, 1].</param>
		/// <param name="b">Brightness, must be in [0, 1].</param>
		/// <param name="a">Output Alpha, must be in [0, 255].</param>
		public static Color HsbToColor(double h, double s, double b, int a = 255) {
			h = Math.Max(0D, Math.Min(360D, h));
			s = Math.Max(0D, Math.Min(1D, s));
			b = Math.Max(0D, Math.Min(1D, b));
			a = Math.Max(0, Math.Min(255, a));

			var r = 0D;
			var g = 0D;
			var bl = 0D;

			if (Math.Abs(s) < Tolerance) {
				r = g = bl = b;
			} else {
				// the argb wheel consists of 6 sectors. Figure out which sector
				// you're in.
				var sectorPos = h / 60D;
				var sectorNumber = (int) Math.Floor(sectorPos);
				// get the fractional part of the sector
				var fractionalSector = sectorPos - sectorNumber;

				// calculate values for the three axes of the argb.
				var p = b * (1D - s);
				var q = b * (1D - s * fractionalSector);
				var t = b * (1D - s * (1D - fractionalSector));

				// assign the fractional colors to r, g, and b based on the sector
				// the angle is in.
				switch (sectorNumber) {
					case 0:
						r = b;
						g = t;
						bl = p;
						break;
					case 1:
						r = q;
						g = b;
						bl = p;
						break;
					case 2:
						r = p;
						g = b;
						bl = t;
						break;
					case 3:
						r = p;
						g = q;
						bl = b;
						break;
					case 4:
						r = t;
						g = p;
						bl = b;
						break;
					case 5:
						r = b;
						g = p;
						bl = q;
						break;
				}
			}

			return Color.FromArgb(
				a,
				Math.Max(0,
					Math.Min(255, Convert.ToInt32(double.Parse($"{r * 255D:0.00}", CultureInfo.InvariantCulture)))),
				Math.Max(0,
					Math.Min(255, Convert.ToInt32(double.Parse($"{g * 255D:0.00}", CultureInfo.InvariantCulture)))),
				Math.Max(0,
					Math.Min(255, Convert.ToInt32(double.Parse($"{bl * 250D:0.00}", CultureInfo.InvariantCulture)))));
		}


		public static IEnumerable<Color> FillArray(Color input, int len) {
			var output = new Color[len];
			for (var i = 0; i < len; i++) {
				output[i] = input;
			}

			return output;
		}

		public static bool IsBlack(Color color, int min = 5) {
			return color.R < min && color.G < min && color.B < min;
		}

		public static Color AdjustBrightness(Color input, float boost) {
			ColorToHsv(input, out var h, out var s, out var v);
			if (v + boost <= 1.0) {
				v += boost;
				//s -= boost;
			} else {
				v = 1.0;
			}

			if (v < 0) {
				v = 0;
			}

			return HsvToColor(h, s, v);
		}

		public static Color Rainbow(float progress) {
			var div = Math.Abs(progress % 1) * 6;
			var ascending = (int) (div % 1 * 255);
			var descending = 255 - ascending;
			var alpha = 0;
			return (int) div switch {
				0 => Color.FromArgb(alpha, 255, ascending, 0),
				1 => Color.FromArgb(alpha, descending, 255, 0),
				2 => Color.FromArgb(alpha, 0, 255, ascending),
				3 => Color.FromArgb(alpha, 0, descending, 255),
				4 => Color.FromArgb(alpha, ascending, 0, 255),
				_ => Color.FromArgb(alpha, 255, 0, descending)
			};
		}

		public static Color[] EmptyColors(Color[] input) {
			for (var i = 0; i < input.Length; i++) {
				input[i] = Color.FromArgb(0, 0, 0, 0);
			}

			return input;
		}

		public static List<Color> EmptyList(int size) {
			var output = new List<Color>(size);
			for (var i = 0; i < size; i++) {
				output.Add(Color.FromArgb(0, 0, 0, 0));
			}

			return output;
		}


		public static Color FixGamma(Color input) {
			int[] gammas = {
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
				1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2,
				2, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5,
				5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10,
				10, 10, 11, 11, 11, 12, 12, 13, 13, 13, 14, 14, 15, 15, 16, 16,
				17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 24, 24, 25,
				25, 26, 27, 27, 28, 29, 29, 30, 31, 32, 32, 33, 34, 35, 35, 36,
				37, 38, 39, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 50,
				51, 52, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 66, 67, 68,
				69, 70, 72, 73, 74, 75, 77, 78, 79, 81, 82, 83, 85, 86, 87, 89,
				90, 92, 93, 95, 96, 98, 99, 101, 102, 104, 105, 107, 109, 110, 112, 114,
				115, 117, 119, 120, 122, 124, 126, 127, 129, 131, 133, 135, 137, 138, 140, 142,
				144, 146, 148, 150, 152, 154, 156, 158, 160, 162, 164, 167, 169, 171, 173, 175,
				177, 180, 182, 184, 186, 189, 191, 193, 196, 198, 200, 203, 205, 208, 210, 213,
				215, 218, 220, 223, 225, 228, 231, 233, 236, 239, 241, 244, 247, 249, 252, 255
			};

			return Color.FromArgb(gammas[input.A], gammas[input.R], gammas[input.G], gammas[input.B]);
		}

		public static float HueFromFrequency(int frequency) {
			var start = 16;
			if (frequency < start) {
				frequency = start;
			}

			var end = start * 2;
			const int max = 10000;
			while (end < frequency && start < max) {
				start *= 2;
				end *= 2;
			}

			if (frequency >= start) {
				var pct = (float) (frequency - start) / start;
				return pct;
			}

			return 1;
		}

		public static List<Color> SectorsToleds(List<Color> ints, int hSectors = -1, int vSectors = -1) {
			var sd = DataUtil.GetSystemData();
			var output = new List<Color>();
			if (ints.Count == 12) {
				hSectors = 5;
				vSectors = 3;
			}

			if (ints.Count < hSectors + hSectors + vSectors + vSectors - 4) {
				Log.Warning("Error, can't convert sectors to LEDs, we have less sectors than we should.");
				return EmptyList(hSectors + hSectors + vSectors + vSectors);
			}

			// Right sectors
			var col = Color.FromArgb(0, 0, 0);
			var count = sd.RightCount / vSectors;
			var start = 0;
			for (var i = start; i < vSectors; i++) {
				col = ints[i];
				for (var c = 0; c < count; c++) {
					output.Add(col);
				}

				start++;
			}

			// Check that we have the right amount, else add more because division
			var diff = sd.RightCount - output.Count;
			if (diff > 0) {
				for (var d = 0; d < diff; d++) {
					output.Add(col);
				}
			}

			// Decrement one since our sectors share corners
			start -= 1;

			// Top sectors
			count = sd.TopCount / hSectors;
			for (var i = start; i < hSectors; i++) {
				col = ints[i];
				for (var c = 0; c < count; c++) {
					output.Add(col);
				}

				start++;
			}

			// Decrement one since our sectors share corners
			start -= 1;

			// Check that we have the right amount, else add more because division
			diff = sd.RightCount + sd.TopCount - output.Count;
			if (diff > 0) {
				for (var d = 0; d < diff; d++) {
					output.Add(col);
				}
			}

			// Left sectors
			count = sd.LeftCount / vSectors;
			for (var i = start; i < vSectors; i++) {
				col = ints[i];
				for (var c = 0; c < count; c++) {
					output.Add(col);
				}

				start++;
			}

			// Decrement one since our sectors share corners
			start -= 1;

			// Check that we have the right amount, else add more because division
			diff = sd.RightCount + sd.TopCount + sd.LedCount - output.Count;
			if (diff > 0) {
				for (var d = 0; d < diff; d++) {
					output.Add(col);
				}
			}


			// Bottom sectors - Skip one sector at the end, because that's the first one.
			count = sd.BottomCount / hSectors;
			for (var i = start; i < hSectors - 1; i++) {
				col = ints[i];
				for (var c = 0; c < count; c++) {
					output.Add(col);
				}

				start++;
			}

			// Check that we have the right amount, else add more because division
			diff = sd.LedCount - output.Count;
			if (diff > 0) {
				for (var d = 0; d < diff; d++) {
					output.Add(col);
				}
			}

			if (output.Count < sd.LedCount) {
				Log.Warning($"Warning, count mismatch: {output.Count} vs {sd.LedCount}");
			}

			return output;
		}

		public static Color[] AddLedColor(Color[] colors, int sector, Color color, SystemData systemData) {
			int s0;
			int e0;

			var vs = systemData.VSectors;
			var hs = systemData.HSectors;
			var count = systemData.LeftCount + systemData.RightCount + systemData.TopCount + systemData.BottomCount;
			var rCount = systemData.RightCount / vs;
			var tCount = systemData.TopCount / hs;
			var lCount = systemData.LeftCount / vs;
			var bCount = systemData.BottomCount / hs;

			var rightLimit = vs;
			var topLimit = vs + hs - 1;
			var leftLimit = vs + hs + vs - 2;
			var bottomLimit = vs + hs + vs + hs - 3;

			if (sector >= 1 && sector <= rightLimit) {
				e0 = sector * rCount;
				s0 = e0 - rCount;
				for (var i = s0; i < e0; i++) {
					if (i < colors.Length) {
						colors[i] = color;
					}
				}
			}

			// Top leds
			if (sector >= leftLimit && sector <= topLimit) {
				var sec = sector - leftLimit;
				e0 = sec * tCount;
				e0 += systemData.LeftCount;
				s0 = e0 - tCount;
				for (var i = s0; i < e0; i++) {
					if (i < colors.Length) {
						colors[i] = color;
					}
				}
			}

			// Left leds
			if (sector >= topLimit && sector <= rightLimit) {
				var sec = sector - topLimit;
				e0 = sec * lCount;
				e0 += systemData.RightCount + systemData.TopCount;
				s0 = e0 - lCount;
				for (var i = s0; i < e0; i++) {
					if (i < colors.Length) {
						colors[i] = color;
					}
				}
			}

			// Bottom leds
			if (sector >= rightLimit && sector <= bottomLimit) {
				var sec = sector - rightLimit;
				e0 = sec * bCount;
				e0 += systemData.RightCount + systemData.LeftCount + systemData.TopCount;
				s0 = e0 - bCount;
				for (var i = s0; i < e0; i++) {
					if (i < colors.Length) {
						colors[i] = color;
					}
				}
			}

			// Also bottom
			if (sector == 1) {
				s0 = count - bCount;
				e0 = count;
				for (var i = s0; i < e0; i++) {
					if (i < colors.Length) {
						colors[i] = color;
					}
				}
			}

			return colors;
		}

		public static List<Color> LedsToSectors(List<Color> ledColors, SystemData sd) {
			var rightColors = ledColors.GetRange(0, sd.RightCount);
			var topColors = ledColors.GetRange(sd.RightCount - 1, sd.TopCount);
			var leftColors = ledColors.GetRange(sd.TopCount - 1, sd.LeftCount);
			var bottomColors = ledColors.GetRange(sd.LeftCount - 1, sd.BottomCount);
			var rStep = (float) rightColors.Count / sd.VSectors;
			var tStep = (float) topColors.Count / sd.HSectors;
			var lStep = (float) leftColors.Count / sd.VSectors;
			var bStep = (float) bottomColors.Count / sd.HSectors;
			var output = new List<Color>();
			var toAvg = new List<Color>();
			// Add the last range of colors from the bottom to sector 0
			for (var i = bottomColors.Count - 1 - bStep; i < bottomColors.Count; i++) {
				toAvg.Add(bottomColors[(int) i]);
			}

			var idx = 0;
			while (idx < rightColors.Count && output.Count <= sd.VSectors) {
				foreach (var t in rightColors) {
					toAvg.Add(t);
				}

				// On the last sector, don't average it so we can add the bit from the next corner
				if (idx % rStep == 0 && output.Count < sd.VSectors) {
					output.Add(AverageColors(toAvg.ToArray()));
					toAvg = new List<Color>();
				}

				idx++;
			}

			idx = 0;
			while (idx < topColors.Count && output.Count < sd.VSectors + sd.HSectors - 1) {
				foreach (var t in topColors) {
					toAvg.Add(t);
				}

				if (idx % tStep == 0) {
					output.Add(AverageColors(toAvg.ToArray()));
					toAvg = new List<Color>();
				}

				idx++;
			}

			idx = 0;
			while (idx < leftColors.Count && output.Count < sd.VSectors + sd.HSectors + sd.VSectors - 2) {
				foreach (var t in leftColors) {
					toAvg.Add(t);
				}

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

		public static int CheckDsSectors(int target) {
			float t = target + 1;
			if (_captureMode == CaptureMode.DreamScreen && target != -1) {
				if (target != 0) {
					var tPct = t / _sectorCount;
					t = tPct * 12f;
					t = Math.Min(t, 12f);
					target = (int) t - 1;
				}
			}

			return target;
		}

		public static void SetSystemData() {
			var sd = DataUtil.GetSystemData();
			_captureMode = (CaptureMode) sd.CaptureMode;
			_sectorCount = sd.SectorCount;
		}
	}
}