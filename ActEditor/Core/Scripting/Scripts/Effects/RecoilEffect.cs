using GRF.FileFormats.ActFormat;
using GRF.Graphics;
using System;

namespace ActEditor.Core.Scripting.Scripts.Effects {

	public class RecoilEffect : ImageProcessingEffect {
		#region IActScript Members
		public class EffectOptions {
			public float Angle;
			public TkVector2 Pivot;
			public int Ease;
			public bool ReverseAnimation;
		}

		private EffectOptions _options = new EffectOptions();
		private Func<float, float> _easeMethod;

		public RecoilEffect() : base("Recoil effect") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Angle", 10f, 0f, 90f);
			effect.AddProperty("Pivot", new TkVector2(0, 0), new TkVector2(-100, 100), new TkVector2(-100, 100));
			effect.AddProperty("Ease", 50, -50, 50);
			effect.AddProperty("ReverseAnimation", false, false, true);

			_animationComponent.DefaultSaveData.AnimLength = 4;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.SetAnimation(2);
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Angle = effect.GetProperty<float>("Angle");
			_options.Pivot = effect.GetProperty<TkVector2>("Pivot");
			_options.Ease = effect.GetProperty<int>("Ease");
			_options.ReverseAnimation = effect.GetProperty<bool>("ReverseAnimation");

			_easeMethod = InterpolationAnimation.GetEaseMethod(_options.Ease);
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			float t = (float)step / (animLength - 1);

			if (_options.ReverseAnimation) {
				if (t > 0.5f)
					t = (1 - t) * 2;
				else
					t *= 2;
			}

			var angle = _options.Angle;

			if (_status.Aid % 8 >= 4)
				angle *= -1;

			t = _easeMethod(t);
			angle *= t;

			TkVector2 pivot = _options.Pivot * new TkVector2(1f, -1f);
			TkVector2 centerSprite = new TkVector2(layer.OffsetX, layer.OffsetY);

			centerSprite.RotateZ(-angle, pivot);

			layer.OffsetX = (int)centerSprite.X;
			layer.OffsetY = (int)centerSprite.Y;
			layer.Rotation += (int)angle;
		}

		public override string InputGesture => "{Dialog.AnimationRecoil}";
		public override string Image => "empty.png";
		public override string Group => "Effects/Hit";

		#endregion
	}
}
