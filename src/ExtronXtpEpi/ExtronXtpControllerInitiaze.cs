using System.Linq;
using System.Text;
using PepperDash.Essentials.Core;
namespace ExtronXtpEpi
{
	public partial class ExtronXtpController
	{
		private void InitializeInputs()
		{
			foreach (var item in inputs)
			{
				var name = item.Name;
				var videoName = item.VideoName;
				var audioName = item.AudioName;
				var ioNumber = item.IoNumber;

				// initialize video routing ports
				var routingPortVideoName = new StringBuilder(Key);
				routingPortVideoName.Append("-");
				routingPortVideoName.Append(videoName);
				routingPortVideoName.Replace(" ", "");
				InputPorts.Add(new RoutingInputPort(routingPortVideoName.ToString(), eRoutingSignalType.Video, eRoutingPortConnectionType.Streaming, ioNumber, this));

				// initialize audio routing ports
				var routingPortAudioName = new StringBuilder(Key);
				routingPortAudioName.Append("-");
				routingPortAudioName.Append(audioName);
				routingPortAudioName.Replace(" ", "");
				InputPorts.Add(new RoutingInputPort(routingPortAudioName.ToString(), eRoutingSignalType.Audio, eRoutingPortConnectionType.Streaming, ioNumber, this));

				// initialize video input sync
				videoInputSync.Add(ioNumber, false);
				VideoInputSyncFeedbacks.Add(item.IoNumber, new BoolFeedback(() =>
				{
					bool result;
					return videoInputSync.TryGetValue(ioNumber, out result) && result;
				}));

				// initialize name feedbacks				
				InputNameFeedbacks.Add(ioNumber, new StringFeedback(() => name ?? string.Empty));
				InputVideoNameFeedbacks.Add(ioNumber, new StringFeedback(() => videoName ?? string.Empty));
				InputAudioNameFeedbacks.Add(ioNumber, new StringFeedback(() => audioName ?? string.Empty));
			}
		}

		private void InitializeOutputs()
		{
			foreach (var item in outputs)
			{
				var name = item.Name ?? string.Empty;
				var videoName = item.VideoName ?? string.Empty;
				var audioName = item.AudioName ?? string.Empty;
				var ioNumber = item.IoNumber;

				// initialize video routing ports
				var routingPortVideoName = new StringBuilder(Key);
				routingPortVideoName.Append("-");
				routingPortVideoName.Append(videoName);
				routingPortVideoName.Replace(" ", "");
				OutputPorts.Add(new RoutingOutputPort(routingPortVideoName.ToString(), eRoutingSignalType.Video, eRoutingPortConnectionType.Streaming, ioNumber, this));

				// initialize audio routing ports
				var routingPortAudioName = new StringBuilder(Key);
				routingPortAudioName.Append("-");
				routingPortAudioName.Append(audioName);
				routingPortAudioName.Replace(" ", "");
				OutputPorts.Add(new RoutingOutputPort(routingPortAudioName.ToString(), eRoutingSignalType.Audio, eRoutingPortConnectionType.Streaming, ioNumber, this));

				videoOutputRoutes.Add(ioNumber, 0);
				audioOutputRoutes.Add(ioNumber, 0);

				// initialize route feedbacks
				VideoOutputFeedbacks.Add(ioNumber, new IntFeedback(() =>
				{
					int result;
					return videoOutputRoutes.TryGetValue(ioNumber, out result) ? result : 0;
				}));

				// initialize route feedbacks
				AudioOutputFeedbacks.Add(ioNumber, new IntFeedback(() =>
				{
					int result;
					return audioOutputRoutes.TryGetValue(ioNumber, out result) ? result : 0;
				}));

				// initialize name feedbacks
				OutputNameFeedbacks.Add(ioNumber, new StringFeedback(() => name ?? string.Empty));
				OutputVideoNameFeedbacks.Add(ioNumber, new StringFeedback(() => videoName ?? string.Empty));
				OutputAudioNameFeedbacks.Add(ioNumber, new StringFeedback(() => audioName ?? string.Empty));

				// initialize routed name feedbacks
				OutputVideoRouteNameFeedbacks.Add(ioNumber, new StringFeedback(() =>
				{
					int result;
					if (!videoOutputRoutes.TryGetValue(ioNumber, out result))
						return "Unknown";

					var src = inputs.First(i => i.IoNumber == result);
					return src.VideoName ?? "No Source";
				}));

				// initialize routed name feedbacks
				OutputAudioRouteNameFeedbacks.Add(ioNumber, new StringFeedback(() =>
				{
					int result;
					if (!audioOutputRoutes.TryGetValue(ioNumber, out result))
						return "Unknown";

					var src = inputs.FirstOrDefault(i => i.IoNumber == result);
					return src.AudioName ?? "No Source";
				}));

				// initialize audio level/mute feedbacks
				OutputAudioLevelFeedbacks.Add(ioNumber, new IntWithFeedback());
				OutputAudioMuteFeedbacks.Add(ioNumber, new BoolWithFeedback());
			}
		}
	}
}