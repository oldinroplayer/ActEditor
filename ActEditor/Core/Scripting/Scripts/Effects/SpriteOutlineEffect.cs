using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using System;
using System.Linq;
using Utilities;

namespace ActEditor.Core.Scripting.Scripts.Effects {
	public class SpriteOutlineEffect : ImageProcessingEffect {
		#region IActScript Members

		public class EffectOptions {
			public GrfColor Color;
			public int Thickness;
			public int Offset;
		}

		private EffectOptions _options = new EffectOptions();
		private int _paletteInsertIndex = -1;

		public SpriteOutlineEffect() : base("Sprite outline") {
		}

		public override void OnAddProperties(EffectConfiguration effect) {
			base.OnAddProperties(effect);
			effect.AddProperty("Color", new GrfColor(255, 255, 255, 255), default, default);
			effect.AddProperty("Thickness", 1, 1, 10);
			effect.AddProperty("Offset", 0, 0, 10);
			_animationComponent.SetEditType(AnimationEditTypes.TargetOnly);
			_animationComponent.DefaultSaveData.AllAnimations = true;
			_animationComponent.DefaultSaveData.AllLayers = true;
			_animationComponent.DefaultSaveData.LoopFrames = false;
			_animationComponent.DefaultSaveData.AddEmptyFrame = false;
			_animationComponent.LoadProperty();
		}

		public override void OnPreviewApplyEffect(EffectConfiguration effect) {
			base.OnPreviewApplyEffect(effect);
			_options.Color = effect.GetProperty<GrfColor>("Color");
			_options.Thickness = effect.GetProperty<int>("Thickness");
			_options.Offset = effect.GetProperty<int>("Offset");

			_generateBgra32Images = false;
			_paletteInsertIndex = -1;

			if (_actInput.Sprite.Palette != null) {
				var colors = _actInput.Sprite.Palette.Colors.ToList();

				for (int i = 1; i < colors.Count; i++) {
					if (_options.Color.Equals(colors[i])) {
						_paletteInsertIndex = i;
						break;
					}
				}

				if (_paletteInsertIndex == -1) {
					var unused = _actInput.Sprite.GetUnusedPaletteIndexes();

					if (unused.Count == 0) {
						_generateBgra32Images = true;
					}
					else {
						_paletteInsertIndex = unused.First();
						_actInput.Sprite.Palette.SetColor(_paletteInsertIndex, _options.Color);
					}
				}
			}
		}

		public override void ProcessLayer(Act act, Layer layer, int step, int animLength) {
			var sprIndex = layer.SprSpriteIndex;

			if (sprIndex.Valid) {
				SpriteIndex newSpriteIndex;

				if (!_transformedSprites.TryGetValue((sprIndex, 0), out newSpriteIndex)) {
					var image = act.Sprite.GetImage(sprIndex).Copy();

					if (_generateBgra32Images)
						image.Convert(GrfImageType.Bgra32);

					newSpriteIndex = act.Sprite.InsertAny(image);
					ProcessImage(image, 0, animLength);
					_transformedSprites[(sprIndex, 0)] = newSpriteIndex;
				}

				layer.SprSpriteIndex = newSpriteIndex;

				PostProcessLayer(act, layer, 0, animLength);
			}
		}

		public override void ProcessImage(GrfImage img, int step, int totalSteps) {
			if (_options.Thickness < 0)
				return;

			_options.Offset = Methods.Clamp(_options.Offset, 0, 1000);

			int totalLayers = _options.Offset + _options.Thickness;
			img.Crop(-totalLayers);

			int[,] distance = new int[img.Width, img.Height];
			const int Inf = 10000;

			// Pass 1: Initialize table
			// Sprite pixels = 0 (the source). Transparent pixels = Inf.
			for (int y = 0; y < img.Height; y++) {
				for (int x = 0; x < img.Width; x++) {
					if (img.IsPixelTransparent(x, y)) {
						distance[x, y] = Inf;
					}
				}
			}

			// Pass 2: Find distance from top-left to bottom-right
			for (int y = 0; y < img.Height; y++) {
				for (int x = 0; x < img.Width; x++) {
					if (distance[x, y] == 0) continue;

					int left = x > 0 ? distance[x - 1, y] + 1 : Inf;
					int top = y > 0 ? distance[x, y - 1] + 1 : Inf;

					distance[x, y] = Math.Min(distance[x, y], Math.Min(left, top));
				}
			}

			// Pass 3: Find distance from bottom-right to top-left
			for (int y = img.Height - 1; y >= 0; y--) {
				for (int x = img.Width - 1; x >= 0; x--) {
					if (distance[x, y] == 0) continue;

					int right = x < img.Width - 1 ? distance[x + 1, y] + 1 : Inf;
					int bottom = y < img.Height - 1 ? distance[x, y + 1] + 1 : Inf;

					distance[x, y] = Math.Min(distance[x, y], Math.Min(right, bottom));
				}
			}

			// Pass 4: Apply the outline using the distance table "mask" to the image, using the offset and thickness
			int minDistance = _options.Offset + 1;
			int maxDistance = _options.Offset + _options.Thickness;

			for (int x = 0; x < img.Width; x++) {
				for (int y = 0; y < img.Height; y++) {
					int d = distance[x, y];

					if (d >= minDistance && d <= maxDistance) {
						if (img.GrfImageType == GrfImageType.Indexed8)
							img.Pixels[y * img.Width + x] = (byte)_paletteInsertIndex;
						else
							img.SetColor(x, y, _options.Color);
					}
				}
			}
		}

		public override string Group => "Effects/Global";
		public override string InputGesture => "{Dialog.SpriteOutline}";
		public override string Image => "effect_outline.png";

		#endregion
	}
}
