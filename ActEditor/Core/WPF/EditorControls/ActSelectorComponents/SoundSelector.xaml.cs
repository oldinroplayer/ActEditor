using ActEditor.Core.WPF.GenericControls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using TokeiLibrary;
using TokeiLibrary.WPF;

namespace ActEditor.Core.WPF.EditorControls.ActSelectorComponents {
	/// <summary>
	/// Interaction logic for SoundSelector.xaml
	/// </summary>
	public partial class SoundSelector : UserControl {
		private List<string> _previousSoundFiles;
		private IFrameRendererEditor _editor;
		private IActIndexSelector _selector;
		private bool _eventsEnabled = true;

		public SoundSelector() {
			InitializeComponent();

			_cbSound.SelectionChanged += _cbSound_SelectionChanged;
		}

		public void Init(IActIndexSelector selector, IFrameRendererEditor editor) {
			CleanupEvents();

			_editor = editor;
			_selector = selector;

			_editor.ActLoaded += _editor_ActLoaded;
			_selector.FrameChanged += _frameChanged;
			_selector.ActionChanged += _frameChanged;
			_selector.SpecialFrameChanged += _frameChanged;
		}

		private void _editor_ActLoaded(object sender) {
			_reloadSound();
			_editor.Act.Commands.CommandIndexChanged += _commands_CommandIndexChanged;
		}

		private void _commands_CommandIndexChanged(object sender, GRF.FileFormats.ActFormat.Commands.IActCommand command) {
			_reloadSound();
			_frameChanged(_selector.SelectedFrame);
		}

		private void CleanupEvents() {
			if (_selector != null) {
				_selector.FrameChanged -= _frameChanged;
				_selector.ActionChanged -= _frameChanged;
				_selector.SpecialFrameChanged -= _frameChanged;
			}

			if (_editor != null) {
				_editor.ActLoaded -= _editor_ActLoaded;

				if (_editor.Act != null)
					_editor.Act.Commands.CommandIndexChanged -= _commands_CommandIndexChanged;
			}
		}

		private void _frameChanged(int frameIndex) {
			int index = _editor.SelectedFrame;
			if (index >= _editor.Act[_editor.SelectedAction].Frames.Count)
				return;
			int selectedSoundId = _editor.Act[_editor.SelectedAction, index].SoundId;

			if (selectedSoundId >= _editor.Act.SoundFiles.Count)
				selectedSoundId = -1;

			_cbSound.SelectedIndex = selectedSoundId + 1;
		}

		private void _cbSound_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (!_eventsEnabled) return;

			if (_cbSound.SelectedIndex == _cbSound.Items.Count - 1) {
				InputDialog dialog = new InputDialog("New sound file name", "New sound", "atk", false, false);
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					if (dialog.Input == "") return;

					_editor.Act.Commands.InsertSoundId(dialog.Input, _editor.Act.SoundFiles.Count);

					_reloadSound();
					_cbSound.SelectedIndex = _editor.Act.SoundFiles.Count;
				}
				else {
					_cbSound.SelectedIndex = _editor.Act[_editor.SelectedAction, _editor.SelectedFrame].SoundId + 1;
				}
			}
			else {
				_editor.Act.Commands.SetSoundId(_editor.SelectedAction, _editor.SelectedFrame, _cbSound.SelectedIndex - 1);
			}
		}

		private void EnableEvents() {
			_eventsEnabled = true;
		}

		private void DisableEvents() {
			_eventsEnabled = false;
		}

		private void _reloadSound() {
			try {
				DisableEvents();

				if (_previousSoundFiles == null)
					_previousSoundFiles = new List<string>();
				else if (_previousSoundFiles.Count == _editor.Act.SoundFiles.Count) {
					bool sameSounds = true;

					for (int i = 0; i < _previousSoundFiles.Count; i++) {
						if (_previousSoundFiles[i] != _editor.Act.SoundFiles[i]) {
							sameSounds = false;
							break;
						}
					}

					if (sameSounds)
						return;
				}

				List<DummyStringView> items = new List<DummyStringView>();
				items.Add(new DummyStringView("None"));
				items.AddRange(_editor.Act.SoundFiles.Select(p => new DummyStringView(p)));
				//_editor.Act.SoundFiles.ForEach(p => items.Add(new DummyStringView(p)));
				items.Add(new DummyStringView("Add new..."));
				_cbSound.ItemsSource = items;
			}
			finally {
				EnableEvents();
			}
		}
	}
}
