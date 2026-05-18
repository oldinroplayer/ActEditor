using ActEditor.ApplicationConfiguration;
using ErrorManager;
using GRF.FileFormats.ActFormat;
using System;
using System.Diagnostics;
using System.Threading;

namespace ActEditor.Core.WPF.EditorControls.ActSelectorComponents {
	public static class ActAnimation {
		public static void DoThread(IActIndexSelector selector, Act act, ISoundPlayer soundPlayer = null) {
			if (act == null) {
				selector.Stop();
				selector.OnAnimationPlaying(AnimationState.Stopped);
				return;
			}

			if (act[selector.SelectedAction].NumberOfFrames <= 1) {
				selector.Stop();
				selector.OnAnimationPlaying(AnimationState.Stopped);
				return;
			}

			if (act[selector.SelectedAction].AnimationSpeed < ActIndexSelector.MaxAnimationSpeed) {
				selector.Stop();
				selector.OnAnimationPlaying(AnimationState.Stopped);
				ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
				return;
			}

			Stopwatch watch = new Stopwatch();
			int startFrame = selector.SelectedFrame;
			int frameInterval = ActEditorConfiguration.FrameInterval;
			int oldInterval = Int32.MinValue;
			long idx = startFrame;

			try {
				selector.OnAnimationPlaying(AnimationState.StartThread);

				while (selector.IsPlaying) {
					var interval = (int)(act[selector.SelectedAction].AnimationSpeed * frameInterval);

					if (oldInterval != interval) {
						oldInterval = interval;
						watch.Restart();
						idx = startFrame = selector.SelectedFrame;
					}

					if (act[selector.SelectedAction].AnimationSpeed < ActIndexSelector.MaxAnimationSpeed) {
						selector.Stop();
						ErrorHandler.HandleException("The animation speed is too fast and might cause issues. The animation will not be displayed.", ErrorLevel.NotSpecified);
						return;
					}

					selector.SelectedFrame++;

					if (soundPlayer!= null)
						soundPlayer.PlaySound(act.TryGetSoundFile(selector.SelectedAction, selector.SelectedFrame));

					if (!selector.IsPlaying)
						return;

					long expectedNextFrame = (idx + 1 - startFrame) * interval - watch.ElapsedMilliseconds;
					idx++;

					Thread.Sleep((int)Math.Max(20, Math.Min(interval, expectedNextFrame)));

					// Reset timer if we're skipping frames
					if (expectedNextFrame < 0)
						oldInterval = -1;
				}
			}
			catch (Exception err) {
				selector.Stop();
				selector.OnAnimationPlaying(AnimationState.Stopped);
				ErrorHandler.HandleException(err);
			}
			finally {
				selector.Stop();
				selector.OnAnimationPlaying(AnimationState.Stopped);
			}
		}
	}
}
