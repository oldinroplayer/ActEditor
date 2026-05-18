using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.Image;
using GRF.Image.Decoders;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;

namespace Scripts {
	public class Script : IActScript {
		private GrfImage _guide;

		public object DisplayName {
			get { return "Generate sprite from selection"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "Ctrl-Shift-K"; }
		}

		public string Image {
			get { return "arrowdown.png"; }
		}

		public Script() {
			byte[] palette = new byte[1024];
			var pixels = new byte[3 * 6] {
				0, 0, 0, 0, 0, 0,
				0, 1, 2, 3, 4, 0,
				0, 0, 0, 0, 0, 0
			};
			GrfImage imageGuide = new GrfImage(pixels, 6, 3, GrfImageType.Indexed8, palette);
			imageGuide.SetPaletteColor(1, new GrfColor(255, 67, 59, 249));
			imageGuide.SetPaletteColor(2, new GrfColor(255, 33, 114, 46));
			imageGuide.SetPaletteColor(3, new GrfColor(255, 229, 88, 97));
			imageGuide.SetPaletteColor(4, new GrfColor(255, 12, 213, 109));

			_guide = imageGuide;
		}

		public void Execute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			if (act == null) return;

			var frame = act[selectedActionIndex, selectedFrameIndex];

			if (selectedLayerIndexes.Length == 0) {
				selectedLayerIndexes = new int[frame.NumberOfLayers];

				for (int i = 0; i < selectedLayerIndexes.Length; i++) {
					selectedLayerIndexes[i] = i;
				}
			}

			if (selectedLayerIndexes.Length == 0) {
				ErrorHandler.HandleException("No layers found.", ErrorLevel.Warning);
			}

			List<Layer> layers = act[selectedActionIndex, selectedFrameIndex].Layers;
			List<Layer> selected = selectedLayerIndexes.Select(index => layers[index]).ToList();

			try {
				act.Commands.ActEditBegin("Generate single sprite");

				Action action = new Action();
				Frame frameSelection = new Frame();
				action.Frames.Add(frameSelection);
				frameSelection.Layers.AddRange(selected);

				var box = ActImaging.Imaging.GenerateBoundingBox(act, selected, ceilingAwayFromZero: false);
				var imageGuide = frameSelection.Render(act, _guide);
				var image = frameSelection.Render(act);

				if (selected.All(p => p.IsIndexed8())) {
					image.Convert(new Indexed8FormatConverter { ExistingPalette = act.Sprite.Palette.BytePalette, Options = Indexed8FormatConverter.PaletteOptions.UseExistingPalette }, null);
				}

				SpriteIndex sprIndex = SpriteIndex.Null;

				for (int i = 0; i < act.Sprite.Images.Count; i++) {
					if (image.Equals(act.Sprite.Images[i])) {
						sprIndex = SpriteIndex.FromAbsoluteIndex(i, act.Sprite, act.Sprite.Images[i]);
					}
				}

				if (!sprIndex.Valid) {
					sprIndex = act.Sprite.InsertAny(image);
				}

				int offsetX = (int)Math.Round(box.Center.X + 0.5f);
				int offsetY = (int)Math.Round(box.Center.Y + 0.5f);

				_adjustOffsetFromMarker(box, imageGuide, ref offsetX, ref offsetY);

				var layer = new Layer(sprIndex);

				layer.OffsetX = offsetX;
				layer.OffsetY = offsetY;

				frame.Layers.Add(layer);
			}
			catch (Exception err) {
				act.Commands.ActCancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
				return;
			}
			finally {
				act.Commands.ActEditEnd();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		private void _adjustOffsetFromMarker(GRF.Graphics.BoundingBox box, GrfImage imageGuide, ref int offsetX, ref int offsetY) {
			int markerCenterX = (int)box.Center.X + (imageGuide.Width % 2);
			int markerCenterY = (int)box.Center.Y + (imageGuide.Height % 2);

			for (int y = -1; y <= 1; y++) {
				for (int x = -3; x <= -1; x++) {
					int markerX = imageGuide.Width / 2 + x;
					int markerY = imageGuide.Height / 2 + y;

					bool found = true;

					for (int k = 0; k < 4; k++) {
						if (imageGuide.GetColor(markerX + k, markerY) != _guide.GetColor(7 + k)) {
							found = false;
							break;
						}
					}

					if (found) {
						int computedMarkerX = markerX - imageGuide.Width / 2;
						int computedMarkerY = markerY - imageGuide.Height / 2;

						// Marker center diff:
						computedMarkerX += offsetX - markerCenterX;
						computedMarkerY += offsetY - markerCenterY;

						if (computedMarkerX < -2)
							offsetX++;
						if (computedMarkerX > -2)
							offsetX--;
						if (computedMarkerY < -1)
							offsetY++;
						if (computedMarkerY > -1)
							offsetY--;
						break;
					}
				}
			}
		}
	}
}
