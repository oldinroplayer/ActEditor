using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilities;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class BubbleEffect : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public int BubbleCount;
			public float Pressure;
			public float Radius;
			public bool Animate;
			public bool SmoothTransform;
			public int RngSeed;
			public Random Rng;
		}

		private EffectOptions _options = new EffectOptions();
		private BoundingBox _actionBox;
		private WarpField _warpField;
		private TkVector2 _warpFieldCenter;
		private List<Bubble> _bubbles;
		private HashSet<ActIndex> _processedActIndexes;

		public BubbleEffect() : base("Bubble") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("BubbleCount", 10, 0, 20);
			effect.AddProperty("Pressure", 0.04f, -1f, 1f);
			effect.AddProperty("Radius", 70f, 1f, 100f);
			effect.AddProperty("Animate", false, false, true);
			effect.AddProperty("SmoothTransform", false, false, true);
			effect.AddProperty("RngSeed", 1234, 0, 10000);

			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.BubbleCount = effect.GetProperty<int>("BubbleCount");
			_options.Pressure = effect.GetProperty<float>("Pressure");
			_options.Radius = effect.GetProperty<float>("Radius");
			_options.Animate = effect.GetProperty<bool>("Animate");
			_options.SmoothTransform = effect.GetProperty<bool>("SmoothTransform");
			_options.RngSeed = effect.GetProperty<int>("RngSeed");
			_options.Rng = new Random(effect.GetProperty<int>("RngSeed"));
		}

		public override void OnPreviewProcessAction(Act act, GRF.FileFormats.ActFormat.Action action, int aid) {
			_actionBox = ActImaging.Imaging.GenerateBoundingBox(act, aid, enableScaling: false);

			int w = (int)(_actionBox.Max.X - _actionBox.Min.X) + 1;
			int h = (int)(_actionBox.Max.Y - _actionBox.Min.Y) + 1;

			int extraX = h > w ? h - w : 0;
			int extraY = w > h ? w - h : 0;

			extraX += 50;
			extraY += 50;

			_warpField = new WarpField(w + extraX, h + extraY);
			_warpFieldCenter = new TkVector2(_warpField.Width / 2, _warpField.Height / 2);

			_bubbles = new List<Bubble>();

			for (int i = 0; i < _options.BubbleCount; i++) {
				_bubbles.Add(new Bubble { Center = new TkVector2(w * _options.Rng.NextDouble(), h * _options.Rng.NextDouble()), Radius = 10f, Time = _options.Rng.NextDouble() });
			}
		}

		struct Bubble {
			public TkVector2 Center;
			public float Radius;
			public double Time;
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			var box = new BoundingBox();
			var layerT = new Layer(_status.Layer);
			layerT.ScaleX = 1;
			layerT.ScaleY = 1;
			box.Add(layerT.ToPlane(_status.OriginalAct));

			var startX = (int)(box.Min.X - _actionBox.Min.X);
			var startY = (int)(box.Min.Y - _actionBox.Min.Y);

			_warpField.Offset = new TkVector2(startX, startY);

			img.Margin(20);
			float t = (float)step / totalSteps;

			for (int i = 0; i < _bubbles.Count; i++) {
				var bubble = _bubbles[i];
				var time = bubble.Time;

				if (_options.Animate)
					time += t;

				if (time > 1f)
					time--;

				if (time >= 0.5f) {
					time = 2 * (1f - time);
				}
				else {
					time = time * 2f;
				}

				_warpField.ApplyRadial(bubble.Center, (float)(_options.Pressure * time), _options.Radius, Curves.Bell);
			}

			_warpField.UseClosestNearbyPixel = !_options.SmoothTransform;
			_warpField.ApplyAndReset(img);
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			base.ProcessLayer(act, layer, step, animLength);

			_processedActIndexes.Add(new ActIndex { ActionIndex = _status.Aid, FrameIndex = _status.Fid, LayerIndex = _status.Lid });
		}

		public override void OnBackupCommand(EffectConfiguration effect) {
			_processedActIndexes = new HashSet<ActIndex>();
		}

		public override void OnPostBackupCommand() {
			// Cleanup images...
			ActHelper.TrimImages(_actInput, _processedActIndexes.ToList(), 0x10, keepPerfectAlignment: true);

			for (int i = _actInput.Sprite.Images.Count - 1; i >= 0; i--) {
				var image = _actInput.Sprite.Images[i];

				if (image.Width <= 4 && image.Height <= 4) {
					_actInput.Sprite.Remove(i, _actInput, EditOption.AdjustIndexes);
				}
			}
		}

		public override string Group => "Effects/Global";
		public override string InputGesture => "{Dialog.AnimationBubble}";
		public override string Image => "empty.png";

		#endregion
	}
}
