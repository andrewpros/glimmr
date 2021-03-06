﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using HueDream.Models.DreamScreen.Scenes;
using HueDream.Models.Util;
using Newtonsoft.Json;
using static HueDream.Models.DreamScreen.Scenes.SceneBase;

namespace HueDream.Models.DreamScreen {
    public class DreamScene {
        private double animationTime;
        private string[] colors;
        private AnimationMode mode;
        private int startInt;

        public void LoadScene(int sceneNo) {
            SceneBase scene;
            switch (sceneNo) {
                case 0:
                    scene = new SceneRandom();
                    break;
                case 1:
                    scene = new SceneFire();
                    break;
                case 2:
                    scene = new SceneTwinkle();
                    break;
                case 3:
                    scene = new SceneOcean();
                    break;
                case 4:
                    scene = new SceneRainbow();
                    break;
                case 5:
                    scene = new SceneJuly();
                    break;
                case 6:
                    scene = new SceneHoliday();
                    break;
                case 7:
                    scene = new ScenePop();
                    break;
                case 8:
                    scene = new SceneForest();
                    break;
                default:
                    scene = null;
                    break;
            }

            if (scene == null) return;
            colors = scene.GetColors();
            animationTime = scene.AnimationTime;
            mode = scene.Mode;
            startInt = 0;
            RefreshColors(colors);
        }

        public void BuildColors(DreamClient dc, CancellationToken ct) {
            if (dc == null) throw new ArgumentNullException(nameof(dc));
            startInt = 0;
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            LogUtil.WriteInc($@"Color builder started, animation time is {animationTime}...");
            while (!ct.IsCancellationRequested) {
                var curTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                var dTime = curTime - startTime;
                // Check and set colors if time is greater than animation int, then reset time count...
                if (!(dTime > animationTime * 1000)) continue;
                startTime = curTime;
                var cols = RefreshColors(colors);
                dc.SendColors(cols, cols, animationTime);
            }

            LogUtil.WriteDec($@"DreamScene: Color Builder canceled. {startTime}");
        }

        private List<Color> RefreshColors(string[] input) {
            var output = new List<Color>();
            var maxColors = input.Length - 1;
            var colorCount = startInt;
            var col1 = startInt;
            var allRand = new Random().Next(0, maxColors);
            for (var i = 0; i < 12; i++) {
                col1 = i + col1;
                if (mode == AnimationMode.Random) col1 = new Random().Next(0, maxColors);
                while (col1 > maxColors) col1 -= maxColors;
                if (mode == AnimationMode.RandomAll) col1 = allRand;
                output[i] = ColorTranslator.FromHtml("#" + input[col1]);
            }

            if (mode == AnimationMode.LinearAll) {
                for (var i = 0; i < 12; i++) {
                    output.Add(ColorTranslator.FromHtml("#" + input[colorCount]));
                }

                startInt++;
            }

            if (mode == AnimationMode.Linear) startInt++;

            if (mode == AnimationMode.Reverse) startInt--;
            if (startInt > maxColors) startInt = 0;

            if (startInt < 0) startInt = maxColors;
            Console.WriteLine($@"Returning refreshed colors: {JsonConvert.SerializeObject(output)}.");
            return output;
        }
    }
}