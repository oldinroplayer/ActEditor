using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Graphics;
using GRF.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using Utilities;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class SwirlEffect : ImageProcessingEffect {
		#region IActScript Members

		public struct EffectOptions {
			public float SwirlFactor;
			public float CollapseFactor;
			public float TwirlFactor;
			public float AnimRadius;
			public int Ease;
			public bool SmoothTransform;
		}

		private EffectOptions _options = new EffectOptions();
		private BoundingBox _actionBox;
		private Func<float, float> _easeMethod;
		private WarpField _warpField;
		private TkVector2 _warpFieldCenter;
		private HashSet<ActIndex> _processedActIndexes;

		public SwirlEffect() : base("Swirl") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("SwirlFactor", 1f, -3f, 3f);
			effect.AddProperty("CollapseFactor", 0.8f, 0f, 1f);
			effect.AddProperty("TwirlFactor", 0f, -360f, 360f);
			effect.AddProperty("AnimRadius", 1f, 0f, 2f);
			effect.AddProperty("Ease", -20, -50, 50);
			effect.AddProperty("SmoothTransform", true, false, true);

			_animationComponent.DefaultSaveData.SetAnimation(4);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = true;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.SwirlFactor = effect.GetProperty<float>("SwirlFactor");
			_options.CollapseFactor = effect.GetProperty<float>("CollapseFactor");
			_options.TwirlFactor = effect.GetProperty<float>("TwirlFactor");
			_options.AnimRadius = effect.GetProperty<float>("AnimRadius");
			_options.Ease = effect.GetProperty<int>("Ease");
			_options.SmoothTransform = effect.GetProperty<bool>("SmoothTransform");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
		}

		public override void OnPreviewProcessAction(Act act, GRF.FileFormats.ActFormat.Action action, int aid) {
			_actionBox = ActImaging.Imaging.GenerateBoundingBox(act, aid, enableScaling: false);

			int w = (int)(_actionBox.Max.X - _actionBox.Min.X) + 1;
			int h = (int)(_actionBox.Max.Y - _actionBox.Min.Y) + 1;

			int extraX = h > w ? h - w : 0;
			int extraY = w > h ? w - h : 0;

			_warpField = new WarpField(w + extraX, h + extraY);
			_warpFieldCenter = new TkVector2(_warpField.Width / 2, _warpField.Height / 2);
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			var box = new BoundingBox();
			var layerT = new Layer(_status.Layer);
			layerT.ScaleX = 1;
			layerT.ScaleY = 1;
			box.Add(layerT.ToPlane(_status.OriginalAct));

			var startX = (int)(box.Min.X - _actionBox.Min.X);
			var startY = (int)(box.Min.Y - _actionBox.Min.Y);

			float t = (float)step / totalSteps;
			t = _easeMethod(t);

			int size = img.Width - img.Height;
			if (size > 0) {
				size = size / 2 + 1;
				img.Margin(0, size, 0, size);
			}
			else if (size < 0) {
				size = -size / 2 + 1;
				img.Margin(size, 0, size, 0);
			}
			
			_warpField.Offset = new TkVector2(startX, startY);

			float radius = new TkVector2(_warpField.Width, _warpField.Height).Length / 2f;

			_warpField.ApplyVortex(_warpFieldCenter, t * _options.SwirlFactor, _options.AnimRadius * radius, t * _options.CollapseFactor, Curves.Bell);
			_warpField.UseClosestNearbyPixel = !_options.SmoothTransform;
			_warpField.ApplyAndReset(img);

			if (_options.TwirlFactor * t != 0) {
				_warpField.ApplyTwirl(_warpFieldCenter, _options.TwirlFactor * t, _options.AnimRadius * radius, Curves.Bell);
				_warpField.ApplyAndReset(img);
			}
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

		public override string Group => "Effects/Dead";
		public override string InputGesture => "{Dialog.AnimationSwirl}";
		public override string Image => "effect_twirl.png";

		#endregion
	}
}
