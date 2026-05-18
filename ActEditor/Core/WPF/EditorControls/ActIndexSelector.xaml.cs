using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ActEditor.ApplicationConfiguration;
using ActEditor.Core.WPF.EditorControls.ActSelectorComponents;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.ActFormat.Commands;
using GRF.Threading;
using GrfToWpfBridge.ActRenderer.ActSelectorComponents;
using TokeiLibrary;
using Utilities;
using Utilities.Commands;
using Utilities.Extension;

namespace ActEditor.Core.WPF.EditorControls {
	/// <summary>
	/// Interaction logic for FrameSelector.xaml
	/// </summary>
	public partial class ActIndexSelector : UserControl, IActIndexSelector, ISoundPlayer {
		#region Delegates

		public delegate void IndexChangedDelegate(int index);
		public delegate void AnimationStateEventHandler(AnimationState index);

		#endregion

		public static float MaxAnimationSpeed = 0.1f;
		private readonly SoundEffect _se = new SoundEffect();
		private IFrameRendererEditor _editor;
		private bool _firstInitDone;

		public ActIndexSelector() {
			InitializeComponent();

			_setupSoundUI();
			_setupRenderModeUI();

			WpfUtilities.AddFocus(_interval);
		}

		private void _setupRenderModeUI() {
			((TextBlock)_buttonRenderMode.FindName("_tbIdentifier")).Margin = new Thickness(3, 0, 0, 3);
			((Grid)((Grid)((Border)_buttonRenderMode.FindName("_border")).Child).Children[2]).HorizontalAlignment = HorizontalAlignment.Left;
			((Grid)((Grid)((Border)_buttonRenderMode.FindName("_border")).Child).Children[2]).Margin = new Thickness(2, 0, 0, 0);

			ActIndexSelectorHelper.UpdatePlayButtonUI(_play);

			_buttonRenderMode.Click += delegate {
				ActEditorConfiguration.ActEditorScalingMode.Set(ActEditorConfiguration.ActEditorScalingMode.Get() == BitmapScalingMode.NearestNeighbor ? BitmapScalingMode.Fant : BitmapScalingMode.NearestNeighbor);
				_updateRenderModeUI();
			};

			_updateRenderModeUI();
		}

		private void _updateRenderModeUI() {
			bool nearestNeighbor = ActEditorConfiguration.ActEditorScalingMode.Get() == BitmapScalingMode.NearestNeighbor;

			_buttonRenderMode.ImagePath = nearestNeighbor ? "editor.png" : "ingame.png";
			_buttonRenderMode.TextHeader = nearestNeighbor ? "Editor" : "Ingame";
			_buttonRenderMode.IsPressed = !nearestNeighbor;
			_buttonRenderMode.ToolTip = nearestNeighbor ? "Render mode is currently set to \"Editor\"." : "Render mode is currently set to \"Ingame\".";
		}

		private void _setupSoundUI() {
			_cbSoundEnable.IsPressed = !ActEditorConfiguration.ActEditorPlaySound;

			_cbSoundEnable.Click += delegate {
				_cbSoundEnable.IsPressed = !_cbSoundEnable.IsPressed;
				_updateSoundUI();
			};

			_updateSoundUI();
		}

		private void _updateSoundUI() {
			ActEditorConfiguration.ActEditorPlaySound = !_cbSoundEnable.IsPressed;
			_cbSoundEnable.ImagePath = ActEditorConfiguration.ActEditorPlaySound ? "soundOn.png" : "soundOff.png";
			_cbSoundEnable.ToolTip = ActEditorConfiguration.ActEditorPlaySound ? "Sounds are currently enabled." : "Sounds are currenty disabled.";
		}

		private int _selectedFrame;
		private int _selectedAction;

		public int SelectedAction {
			get => _selectedAction;
			set {
				if (value == _selectedAction)
					return;

				int max = _editor.Act.NumberOfActions;
				_selectedAction = (value % max + max) % max;

				// This should always be done on the main UI thread
				this.Dispatch(_ => {
					//if (SelectedFrame >= _editor.Act[_selectedAction].Frames.Count)
					//	SelectedFrame = 0;

					OnActionChanged(_selectedAction);
				});
			}
		}

		public int SelectedFrame {
			get => _selectedFrame;
			set {
				if (value == _selectedFrame)
					return;

				int max = _editor.Act[SelectedAction].NumberOfFrames;
				_selectedFrame = (value % max + max) % max;

				// This should always be done on the main UI thread
				this.Dispatch(_ => {
					OnFrameChanged(_selectedFrame);
				});
			}
		}

		public bool IsPlaying { get; private set; }
		public event IndexChangedDelegate ActionChanged;
		public event IndexChangedDelegate FrameChanged;
		public event IndexChangedDelegate SpecialFrameChanged;
		public event AnimationStateEventHandler AnimationPlaying;
		public void OnSpecialFrameChanged(int frameIndex) => SpecialFrameChanged?.Invoke(frameIndex);
		public void OnFrameChanged(int frameIndex) => FrameChanged?.Invoke(frameIndex);
		public void OnAnimationPlaying(AnimationState state) => AnimationPlaying?.Invoke(state);
		public void OnActionChanged(int actionIndex) => ActionChanged?.Invoke(actionIndex);

		private void _play_Click(object sender, RoutedEventArgs e) {
			if (IsPlaying)
				Stop();
			else
				Play();
		}

		public void PlaySound(string soundFile) {
			if (soundFile != null && ActEditorConfiguration.ActEditorPlaySound) {
				if (soundFile.GetExtension() == null)
					soundFile = soundFile + ".wav";

				byte[] file = ActEditorWindow.Instance.MetaGrf.GetData("data\\wav\\" + soundFile);

				if (file != null) {
					try {
						_se.Play(file);
					}
					catch (Exception err) {
						_cbSoundEnable.Dispatch(p => p.OnClick(null));
						ErrorHandler.HandleException(err);
					}
				}
			}
		}

		public void Init(IFrameRendererEditor editor, int actionIndex, int selectedAction) {
			_editor = editor;
			_editor.ActLoaded += new ActEditorWindow.ActEditorEventDelegate(_actEditor_ActLoaded);
			_cbSound.Init(this, editor);
			_frameTbSelect.Init(this, editor);
			_sbFrameIndex.Init(this, editor);
			if (!_firstInitDone)
				ActSelectorComponents.ActSelectorHelper.InitSelectorComboBox(_editor, _comboBoxActionIndex, _comboBoxAnimationIndex);
			if (editor.Act != null)
				editor.IndexSelector.OnActionChanged(SelectedAction);
			_firstInitDone = true;
		}

		private void _updateAction() {
			if (_editor.Act == null) return;

			if (SelectedAction >= _editor.Act.NumberOfActions) {
				SelectedAction = _editor.Act.NumberOfActions - 1;
			}

			if (_editor.SelectedFrame >= _editor.Act[_editor.SelectedAction].NumberOfFrames && _editor.SelectedFrame > 0) {
				SelectedFrame = Math.Max(0, _editor.Act[_editor.SelectedAction].NumberOfFrames - 1);
			}
		}

		private void _actEditor_ActLoaded(object sender) {
			ActionChanged -= _actIndexSelector_ActionChanged;
			ActionChanged += _actIndexSelector_ActionChanged;
			_directionalControl.Init(this, _editor);
			_updateInterval();
			_editor.IndexSelector.OnActionChanged(SelectedAction);
			_editor.Act.VisualInvalidated += s => _editor.FrameRenderer.Update();
			_editor.Act.RenderInvalidated += s => _editor.FrameRenderer.Update();
			_editor.Act.Commands.CommandIndexChanged += new AbstractCommand<IActCommand>.AbstractCommandsEventHandler(_commands_CommandUndo);
		}

		private void _actIndexSelector_ActionChanged(int index) {
			_updateInterval();
		}

		private void _commands_CommandUndo(object sender, IActCommand command) {
			try {
				Stop();
				_updateAction();
				_updateInterval();
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		public void Play() {
			if (IsPlaying) return;

			_play.Dispatch(delegate {
				_play.IsPressed = true;
				_sbFrameIndex.SetEnabled(false);
				IsPlaying = true;
				ActIndexSelectorHelper.UpdatePlayButtonUI(_play);
			});

			GrfThread.Start(() => ActSelectorComponents.ActAnimation.DoThread(this, _editor.Act, this));
		}

		public void Stop() {
			if (!IsPlaying) return;

			_play.Dispatch(delegate {
				_play.IsPressed = false;
				_sbFrameIndex.SetEnabled(true);
				IsPlaying = false;
				ActIndexSelectorHelper.UpdatePlayButtonUI(_play);
			});
		}

		public void RefreshIntervalDisplay() {
			_disableEvents();
			_updateInterval();
			_enableEvents();
		}

		private void _updateInterval() {
			_interval.Text = (_editor.Act[SelectedAction].AnimationSpeed * ActEditorConfiguration.FrameInterval).ToString(CultureInfo.InvariantCulture);
		}

		public void Update() => _editor.FrameRenderer.Update();

		private void _disableEvents() {
			_interval.TextChanged -= _interval_TextChanged;
		}

		private void _enableEvents() {
			_interval.TextChanged += _interval_TextChanged;
		}

		private void _interval_TextChanged(object sender, TextChangedEventArgs e) {
			if (_editor.Act == null) return;
			if (_editor.Act.Commands.IsLocked) return;

			float fval = FormatConverters.SingleConverterNoThrow(_interval.Text);

			if (fval > 0) {
				_editor.Act.Commands.SetInterval(SelectedAction, fval / ActEditorConfiguration.FrameInterval);
			}
		}
	}
}