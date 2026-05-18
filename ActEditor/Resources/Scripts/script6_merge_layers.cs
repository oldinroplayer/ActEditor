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
using GRF.FileFormats.PalFormat;
using GRF.Image;
using GRF.Image.Decoders;
using GRF.Graphics;
using GrfToWpfBridge;
using TokeiLibrary;
using TokeiLibrary.WPF;
using Action = GRF.FileFormats.ActFormat.Action;
using Frame = GRF.FileFormats.ActFormat.Frame;
using Point = System.Windows.Point;

namespace Scripts {
	public class Script : IActScript {
		private GrfImage _guide;

		public object DisplayName {
			get { return "Merge layers (new sprites)"; }
		}

		public string Group {
			get { return "Scripts"; }
		}

		public string InputGesture {
			get { return "{Scripts.MergeLayers}"; }
		}

		public string Image {
			get { return "addgrf.png"; }
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

			try {
				act.Commands.ActEditBegin("Merge layers into new sprite");
				int count = act.GetAllFrames().Count + 1;
				int index = 0;

				TaskManager.DisplayTaskC("Rendering frames...", "Please wait...", () => index, count, new Action<Func<bool>>(isCancelling => {
					try {
						foreach (var action in act) {
							foreach (var frame in action) {
								if (frame.Layers.Count <= 1) {
									index++;
									continue;
								}
								if (isCancelling()) return;

								var box = ActImaging.Imaging.GenerateBoundingBox(act, frame, ceilingAwayFromZero: false);
								var imageGuide = frame.Render(act, _guide);
								var image = frame.Render(act);

								SpriteIndex sprIndex = SpriteIndex.Null;

								for (int i = 0; i < act.Sprite.Images.Count; i++) {
									if (image.Equals(act.Sprite.Images[i])) {
										if (isCancelling()) return;
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

								frame.Layers.Clear();
								frame.Layers.Add(layer);
								index++;
							}
						}

						//act.Sprite.RemoveUnusedImages(act);
						ActEditor.Core.ActHelper.TrimImages(act, tolerance: 0, keepPerfectAlignment: true);
					}
					finally {
						index = count;
					}
				}));
			}
			catch (Exception err) {
				act.Commands.ActCancelEdit();
				ErrorHandler.HandleException(err, ErrorLevel.Warning);
			}
			finally {
				act.Commands.ActEditEnd();
				act.InvalidateVisual();
				act.InvalidateSpriteVisual();
			}
		}

		public bool CanExecute(Act act, int selectedActionIndex, int selectedFrameIndex, int[] selectedLayerIndexes) {
			return act != null;
		}

		private void _adjustOffsetFromMarker(BoundingBox box, GrfImage imageGuide, ref int offsetX, ref int offsetY) {
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