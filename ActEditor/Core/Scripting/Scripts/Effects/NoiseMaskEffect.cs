using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using GRF.Image;
using System;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class NoiseMaskEffect : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public float Threshold;
			public float PerlinScale;
			public GrfColor BackColor;
			public bool Animate;
			public int RngSeed;
			public Random Rng;
		}

		private EffectOptions _options = new EffectOptions();
		private BoundingBox _actionBox;
		private bool[,] _cutMask;
		private float[,] _noise;

		public NoiseMaskEffect() : base("Noise mask") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Threshold", 0.5f, 0f, 1f);
			effect.AddProperty("PerlinScale", 20f, 0f, 200f);
			effect.AddProperty("BackColor", new GrfColor("#902947AE"), default, default);
			effect.AddProperty("Animate", false, false, true);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Threshold = effect.GetProperty<float>("Threshold");
			_options.PerlinScale = effect.GetProperty<float>("PerlinScale");
			_options.BackColor = effect.GetProperty<GrfColor>("BackColor");
			_options.Animate = effect.GetProperty<bool>("Animate");
			_options.RngSeed = effect.GetProperty<int>("RngSeed");
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));

			_noise = new float[0, 0];
		}

		public override void OnPreviewProcessFrame(int step, int animLength) {
			base.OnPreviewProcessFrame(step, animLength);

			if (_options.Animate) {
				int w = _cutMask.GetLength(0);
				int h = _cutMask.GetLength(1);
				float t = (float)step / animLength;

				for (int x = 0; x < w; x++) {
					for (int y = 0; y < h; y++) {
						float v = _noise[x, y] + t;

						if (v > 1f)
							v--;

						_cutMask[x, y] = v < _options.Threshold;
					}
				}
			}
		}

		public override void OnPreviewProcessAction(Act act, GRF.FileFormats.ActFormat.Action action, int aid) {
			_actionBox = ActImaging.Imaging.GenerateBoundingBox(act, aid, enableScaling: false);

			int wC = (int)(_actionBox.Max.X - _actionBox.Min.X) + 1;
			int hC = (int)(_actionBox.Max.Y - _actionBox.Min.Y) + 1;

			int w = wC + 50;
			int h = hC + 50;

			if (_noise.GetLength(0) < wC || _noise.GetLength(1) < hC) {
				_cutMask = new bool[w, h];
				_noise = Noise2D.GenerateNoiseMap(w, h, 20, Math.Max(0.01f, _options.PerlinScale), _options.RngSeed);

				if (!_options.Animate) {
					for (int x = 0; x < w; x++)
						for (int y = 0; y < h; y++)
							_cutMask[x, y] = _noise[x, y] < _options.Threshold;
				}
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			var box = new BoundingBox();
			var layerT = new Layer(_status.Layer);
			layerT.ScaleX = 1;
			layerT.ScaleY = 1;
			box.Add(layerT.ToPlane(_status.OriginalAct));

			var startX = (int)(box.Min.X - _actionBox.Min.X);
			var startY = (int)(box.Min.Y - _actionBox.Min.Y);

			var imgCopy = img.Clone();

			for (int x = 0; x < img.Width; x++) {
				for (int y = 0; y < img.Height; y++) {
					int xx = x + startX;
					int yy = y + startY;

					if (_cutMask[xx, yy] && !imgCopy.IsPixelTransparent(x, y)) {
						img.SetPixelTransparent(x, y);
					}
				}
			}
		}

		public override void PreviewProcessLayer(Act act, Layer layer, int step, int animLength) {
			base.PreviewProcessLayer(act, layer, step, animLength);

			if (_options.BackColor.A == 0)
				return;

			Layer copy = new Layer(_status.Layer);
			copy.Color = _options.BackColor;
			_layersToInsert.Add((_status.ActIndex.FrameIndex, _status.ActIndex.LayerIndex, copy));
		}

		public override string Group => "Effects/Global";
		public override string InputGesture => "{Dialog.AnimationNoiseMask}";
		public override string Image => "effect_noisemask.png";

		#endregion
	}
}
