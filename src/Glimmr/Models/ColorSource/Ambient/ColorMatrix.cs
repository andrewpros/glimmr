﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Serilog;

namespace Glimmr.Models.ColorSource.Ambient; 

public class ColorMatrix {
	
	public int Width { get; }
	public int Height { get; }

	private readonly int _step;
	public int Size => Width * Height;

	private Color[][] Colors { get; set; }

	public IEnumerable<Color> ColorArray() {
		var index = 0;
		var output = new Color[Size];
		for (var r = Colors.Length - 1; r >= 0; r--) {
			var row = Colors[r];
			for (var c = row.Length - 1; c >= 0; c--) {
				output[index] = row[c];
				index++;
			}
		}
		return output;
	}

	private readonly AmbientStream.MatrixDirection _matrixDirection;
	private AmbientStream.MatrixDirection _currentDirection;

	
	private int _rotationStep;

	private int _randH;
	private int _randV;

	private readonly Random _random;
	

	public ColorMatrix(AmbientScene scene) {
		var matrix = scene.ColorMatrix;
		var dir = scene.MatrixDirection;
		if (matrix == null || dir == null) throw new ArgumentNullException(nameof(scene));
		_matrixDirection = Enum.Parse<AmbientStream.MatrixDirection>(dir);
		_currentDirection = _matrixDirection;
		Colors = matrix.Select(m => m.Select(ColorTranslator.FromHtml).ToArray()).ToArray();
		_step = scene.MatrixStep;
		Height = Colors.Length;
		Width = Colors[0].Length;
		_random = new Random();
		
		if (_matrixDirection != AmbientStream.MatrixDirection.CW && _matrixDirection != AmbientStream.MatrixDirection.CCW) {
			return;
		}

		_rotationStep = 1;
		if (_matrixDirection == AmbientStream.MatrixDirection.CCW) _rotationStep = 4;
	}

	
	
	public void Update() {
		var rotate = true;
		var step = _step;
		var hStep = 0;
		var vStep = 0;
			switch (_matrixDirection) {
				case AmbientStream.MatrixDirection.CCW:
					if (_matrixDirection == _currentDirection || rotate) {
						_rotationStep++;
						if (_rotationStep > 4) _rotationStep = 1;
						_currentDirection = (AmbientStream.MatrixDirection)_rotationStep;
					}
					break;
				case AmbientStream.MatrixDirection.CW:
					if (_matrixDirection == _currentDirection || rotate) {
						_rotationStep--;
						if (_rotationStep < 1) _rotationStep = 4;
						_currentDirection = (AmbientStream.MatrixDirection)_rotationStep;
					}
					break;
				default:
					_currentDirection = _matrixDirection;
					break;
			}
			switch(_currentDirection) {
				case AmbientStream.MatrixDirection.RTL:
					hStep -= step;
					break;
				case AmbientStream.MatrixDirection.TTB:
					vStep -= step;
					break;
				case AmbientStream.MatrixDirection.LTR:
					hStep += step;
					break;
				case AmbientStream.MatrixDirection.BTT:
					vStep += step;
					break;
				case AmbientStream.MatrixDirection.Random:
					if (_matrixDirection == _currentDirection || rotate) {
						_randH = _random.Next(step * -1, step);
						_randV = _random.Next(step * -1, step);
					}

					hStep = _randH;
					vStep = _randV;
					break;
			}

			try {
				ShiftMatrix(hStep, vStep);
			} catch (Exception e) {
				Log.Warning("Exception shifting Matrix: " + e.Message);
			}
			
	}

	private void ShiftMatrix(int hStep, int vStep) {
		var output = new List<Color[]>();
		for (var i = 0; i < Height; i++) {
			var row = i + vStep;
			if (row >= Height) {
				row -= Height;
			}

			if (row < 0) {
				row += Height;
			}

			var iCols = Colors[row];
			var shifted = new List<Color>();
			for (var r = 0; r < Width; r++) {
				var col = r + hStep;
				if (col >= Width) {
					col -= Width;
				}

				if (col < 0) {
					col += Width;
				}
				shifted.Add(iCols[col]);
			}
			output.Add(shifted.ToArray());
		}
		Colors = output.ToArray();
	}
}