using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.FrameEditor;
using System;
using System.Windows;
using System.Windows.Media;
using Utilities;

namespace ActEditor.Core.DrawingComponents {
	public class GridDraw : DrawingComponent {
		public Pen LinePen;

		private GridRenderer _renderer = new GridRenderer();

		public GridDraw(FrameRenderer frameRenderer) {
			_onPropertyChanged();

			_renderer.SetAct(this, frameRenderer);
		}

		public override void QuickRender(FrameRenderer renderer) {
			_renderer.InvalidateVisual();
		}

		public override void Render(FrameRenderer renderer) {
			if (!renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Add(_renderer);

			_renderer.InvalidateVisual();
		}

		public override void Remove(FrameRenderer renderer) {
			if (renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Remove(_renderer);

			_renderer.InvalidateVisual();
		}

		public override void Unload(FrameRenderer renderer) {
			if (renderer.Canvas.Children.Contains(_renderer))
				renderer.Canvas.Children.Remove(_renderer);
		}

		private void _onPropertyChanged() {
			LinePen = new Pen(Brushes.White, 1);
		}
	}

	public class GridRenderer : FrameworkElement {
		private GridDraw _gridDraw;
		private FrameRenderer _frameRenderer;
		private VisualCollection _visuals;
		public VisualCollection Visuals => _visuals;

		protected override int VisualChildrenCount => _visuals.Count;
		protected override Visual GetVisualChild(int index) => _visuals[index];

		private DrawingVisual _gridVisual = new DrawingVisual();

		public void SetAct(GridDraw lineDraw, FrameRenderer frameRenderer) {
			_gridDraw = lineDraw;
			_frameRenderer = frameRenderer;
			_visuals = new VisualCollection(frameRenderer.Canvas);
		}

		protected override void OnRender(DrawingContext drawingContext) {
			if (_visuals.Count == 0) {
				_visuals.Add(_gridVisual);
			}

			drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, _frameRenderer.Canvas.ActualWidth, _frameRenderer.Canvas.ActualHeight));

			using (var dc = _gridVisual.RenderOpen()) {
				double unitScaled = _frameRenderer.ZoomEngine.Scale;

				int minX = 0;
				int minY = 0;
				int maxX = (int)Math.Ceiling(_frameRenderer.Canvas.ActualWidth / unitScaled);
				int maxY = (int)Math.Ceiling(_frameRenderer.Canvas.ActualHeight / unitScaled);

				while (maxX > 50 || maxY > 50) {
					unitScaled *= 5;

					maxX = (int)Math.Ceiling(_frameRenderer.Canvas.ActualWidth / unitScaled);
					maxY = (int)Math.Ceiling(_frameRenderer.Canvas.ActualHeight / unitScaled);
				}

				double baseY = (_frameRenderer.RelativeCenter.Y * _frameRenderer.Canvas.ActualHeight) % unitScaled;
				double posY = Math.Round(_frameRenderer.RelativeCenter.Y * _frameRenderer.Canvas.ActualHeight) - 0.5d;

				for (int y = minY; y < maxY; y++) {
					double yy = baseY + y * unitScaled;
					dc.DrawLine(_gridDraw.LinePen, new Point(0, yy), new Point(_frameRenderer.Canvas.ActualWidth, yy));
				}

				double baseX = (_frameRenderer.RelativeCenter.X * _frameRenderer.Canvas.ActualWidth) % unitScaled;
				double posX = Math.Round(_frameRenderer.RelativeCenter.X * _frameRenderer.Canvas.ActualWidth) - 0.5d;
				for (int x = minX; x < maxX; x++) {
					double xx = baseX + x * unitScaled;
					dc.DrawLine(_gridDraw.LinePen, new Point(xx, 0), new Point(xx, _frameRenderer.Canvas.ActualHeight));
				}
			}
		}
	}
}
