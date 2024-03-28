using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Queues;

namespace ExtronXtpEpi
{
	public partial class ExtronXtpController
	{
		private void LinkBaseFeedbacksToApi(BasicTriList trilist, ExtronXtpBridgeJoinMap joinMap)
		{
			trilist.SetString(joinMap.Name.JoinNumber, Name);

			CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnlineXtpLegacy.JoinNumber]);
			SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.SocketStatus.JoinNumber]);
		}

		private void LinkInputsToApi(BasicTriList trilist, ExtronXtpBridgeJoinMap joinMap)
		{
			// input names
			foreach (var item in InputNameFeedbacks)
			{
				var join = (ushort)(joinMap.InputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// video input names
			foreach (var item in InputVideoNameFeedbacks)
			{
				var join = (ushort)(joinMap.InputVideoNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// audio input names
			foreach (var item in InputAudioNameFeedbacks)
			{
				var join = (ushort)(joinMap.InputAudioNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// input video syncs
			foreach (var item in VideoInputSyncFeedbacks)
			{
				var join = (ushort)(joinMap.VideoSyncStatus.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.BooleanInput[join]);
			}
		}

		private void LinkOutputsToApi(BasicTriList trilist, ExtronXtpBridgeJoinMap joinMap)
		{
			// output names
			foreach (var item in OutputNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// video output names
			foreach (var item in OutputVideoNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputVideoNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// audio output names
			foreach (var item in OutputAudioNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputAudioNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// video routes
			foreach (var item in VideoOutputFeedbacks)
			{
				var join = (ushort)(joinMap.OutputVideo.JoinNumber + item.Key - 1);

				var output = item.Key;
				trilist.SetUShortSigAction(join, input => ExecuteSwitch(input, output, eRoutingSignalType.Video));

				var feedback = item.Value;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.UShortInput[join]);
			}

			// audio routes
			foreach (var item in AudioOutputFeedbacks)
			{
				var join = (ushort)(joinMap.OutputAudio.JoinNumber + item.Key - 1);

				var output = item.Key;
				trilist.SetUShortSigAction(join, input => ExecuteSwitch(input, output, eRoutingSignalType.Audio));

				var feedback = item.Value;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.UShortInput[join]);
			}

			// video routed names
			foreach (var item in OutputVideoNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputCurrentVideoInputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// audio routed names
			foreach (var item in OutputAudioNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputCurrentAudioInputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// output volumes
			foreach (var item in OutputAudioLevelFeedbacks)
			{
				var volIncJoin = (ushort)(joinMap.OutputVolumeUp.JoinNumber + item.Key - 1);
				var volDecJoin = (ushort)(joinMap.OutputVolumeDown.JoinNumber + item.Key - 1);
				var volSetJoin = (ushort)(joinMap.OutputVolume.JoinNumber + item.Key - 1);
				var output = item.Key;

				trilist.SetUShortSigAction(volIncJoin, (b) => OutputVolumeIncrement(output));
				trilist.SetUShortSigAction(volDecJoin, (b) => OutputVolumeDecrement(output));
				trilist.SetUShortSigAction(volSetJoin, (v) => OutputVolumeSet(output, v));

				var feedback = item.Value.Feedback;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.UShortInput[volSetJoin]);
			}

			// output mutes
			foreach (var item in OutputAudioMuteFeedbacks)
			{
				var muteTogJoin = (ushort)(joinMap.OutputMuteToggle.JoinNumber + item.Key - 1);
				var output = item.Key;

				trilist.SetSigFalseAction(muteTogJoin, () => OutputMuteToggle(output));

				var feedback = item.Value.Feedback;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.BooleanInput[muteTogJoin]);
			}
		}
	}
}