using ActEditor.Core.WPF.EditorControls;
using ActEditor.Core.WPF.EditorControls.ActSelectorComponents;

namespace ActEditor.Core.WPF.Dialogs {
	public class HeadEditorActIndexSelector : IActIndexSelector {
		private readonly HeadEditorDialog _editor;

		public HeadEditorActIndexSelector(HeadEditorDialog editor) {
			_editor = editor;
		}

		public void OnFrameChanged(int actionIndex) {
			FrameChanged?.Invoke(actionIndex);
		}

		public bool IsPlaying { get { return false; } }
		public event ActIndexSelector.IndexChangedDelegate ActionChanged;

		public void OnActionChanged(int actionIndex) {
			ActionChanged?.Invoke(actionIndex);
		}

		public event ActIndexSelector.IndexChangedDelegate FrameChanged;
		public event ActIndexSelector.IndexChangedDelegate SpecialFrameChanged;
		public event ActIndexSelector.AnimationStateEventHandler AnimationPlaying;

		public void OnSpecialFrameChanged(int frameIndex) {
			SpecialFrameChanged?.Invoke(frameIndex);
		}

		public void OnAnimationPlaying(AnimationState state) {
			AnimationPlaying?.Invoke(state);
		}

		public void SetAction(int index) {
			_editor._listViewHeads.SelectedIndex = index;
		}

		public void SetFrame(int index) {
			// Nothing to do
		}

		public int SelectedAction { get; set; }
		public int SelectedFrame { get; set; }

		public void Play() {
			// Nothing to do
		}

		public void Stop() {
			// Nothing to do
		}

		public void Init(IFrameRendererEditor editor, int actionIndex, int selectedAction) {
			// Nothing to do
		}
	}
}
