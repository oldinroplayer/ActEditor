using GRF.Graphics;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public static class Noise2D {
		private static Random _random = new Random();
		private static int[] _permutation;

		private static TkVector2[] _gradients;

		static Noise2D() {
			CalculatePermutation(out _permutation);
			CalculateGradients(out _gradients);
		}

		private static void CalculatePermutation(out int[] p) {
			p = Enumerable.Range(0, 256).ToArray();

			for (var i = 0; i < p.Length; i++) {
				var source = _random.Next(p.Length);

				var t = p[i];
				p[i] = p[source];
				p[source] = t;
			}
		}

		public static void Reseed() {
			CalculatePermutation(out _permutation);
		}

		public static void Reseed(int seed) {
			_random = new Random(seed);
			CalculatePermutation(out _permutation);
		}

		private static void CalculateGradients(out TkVector2[] grad) {
			grad = new TkVector2[256];

			for (var i = 0; i < grad.Length; i++) {
				TkVector2 gradient;

				do {
					gradient = new TkVector2((float)(_random.NextDouble() * 2 - 1), (float)(_random.NextDouble() * 2 - 1));
				}
				while (gradient.LengthSquared >= 1);

				gradient.Normalize();

				grad[i] = gradient;
			}
		}

		private static float Drop(float t) {
			t = Math.Abs(t);
			return 1f - t * t * t * (t * (t * 6 - 15) + 10);
		}

		private static float Q(float u, float v) {
			return Drop(u) * Drop(v);
		}

		public static float Noise(float x, float y) {
			var cell = new TkVector2((float)Math.Floor(x), (float)Math.Floor(y));

			var total = 0f;

			var corners = new[] { new TkVector2(0, 0), new TkVector2(0, 1), new TkVector2(1, 0), new TkVector2(1, 1) };

			foreach (var n in corners) {
				var ij = cell + n;
				var uv = new TkVector2(x - ij.X, y - ij.Y);

				var index = _permutation[(int)ij.X % _permutation.Length];
				index = _permutation[(index + (uint)ij.Y) % _permutation.Length];

				var grad = _gradients[index % _gradients.Length];

				total += Q(uv.X, uv.Y) * TkVector2.Dot(grad, uv);
			}

			return Math.Max(Math.Min(total, 1f), -1f);
		}

		public static float[,] GenerateNoiseMap(int width, int height, int octaves, float scale, int seed = -1) {
			var data = new float[width, height];
			var min = float.MaxValue;
			var max = float.MinValue;

			if (seed == -1)
				Noise2D.Reseed();
			else
				Noise2D.Reseed(seed);

			var frequency = 0.5f;
			var amplitude = 1f;
			object syncRoot = new object();

			for (var octave = 0; octave < octaves; octave++) {
				Parallel.For(0
					, width * height
					, () => (LocalMin: float.MaxValue, LocalMax: float.MinValue)
					, (offset, state, local) => {
						var i = offset % width;
						var j = offset / width;
						var noise = Noise2D.Noise(
							i / scale * frequency, 
							j / scale * frequency
						);
						noise = data[i, j] += noise * amplitude;

						return (Math.Min(local.LocalMin, noise), Math.Max(local.LocalMax, noise));
					}
					, (finalLocal) => { // Merge locals into the global min/max
						lock (syncRoot) {
							min = Math.Min(min, finalLocal.LocalMin);
							max = Math.Max(max, finalLocal.LocalMax);
						}
					}
				);

				frequency *= 2;
				amplitude /= 2;
			}

			for (int x = 0; x < width; x++) {
				for (int y = 0; y < height; y++) {
					data[x, y] = (data[x, y] - min) / (max - min);
				}
			}

			return data;
		}
	}
}
