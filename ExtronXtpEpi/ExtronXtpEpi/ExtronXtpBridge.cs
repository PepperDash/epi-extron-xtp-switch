using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Bridges;

namespace ExtronXtpEpi
{
    public static class ExtronXtpBridge
    {
        public static void LinkToApiExt(this ExtronXtpChassis chassis, Crestron.SimplSharpPro.DeviceSupport.BasicTriList trilist, uint joinStart, string joinMapKey)
        {
			try
			{
				var joinMap = new ExtronControllerJoinMap();
				joinMap.OffsetJoinNumbers(joinStart);

				chassis.ConnectFb.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline]);
				chassis.OkFb.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline + 1]);


				foreach (var input in chassis.VideoInputSyncFeedbacks)
				{
					var joinActual = input.Key + joinMap.VideoSyncStatus;

					var feedback = input.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.BooleanInput[(uint)joinActual]);
				}

				foreach (var output in chassis.VideoOutputFeedbacks)
				{
					var joinActual = output.Key + joinMap.OutputVideo;
					var outputActual = output.Key;
					trilist.SetUShortSigAction((uint)joinActual, input => chassis.ExecuteSwitch(input, outputActual, eRoutingSignalType.Video));

					var feedback = output.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.UShortInput[(uint)joinActual]);
				}

				foreach (var output in chassis.AudioOutputFeedbacks)
				{
					var joinActual = output.Key + joinMap.OutputAudio;
					var outputActual = output.Key;
					trilist.SetUShortSigAction((uint)joinActual, input => chassis.ExecuteSwitch(input, outputActual, eRoutingSignalType.Audio));

					var feedback = output.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.UShortInput[(uint)joinActual]);
				}

				foreach (var input in chassis.InputNameFeedbacks)
				{
					var joinActual = input.Key + joinMap.InputNames;

					var feedback = input.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}

				foreach (var inputVideoName in chassis.InputVideoNameFeedbacks)
				{
					var joinActual = inputVideoName.Key + joinMap.InputVideoNames;

					var feedback = inputVideoName.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}
				foreach (var inputAudioName in chassis.InputAudioNameFeedbacks)
				{
					var joinActual = inputAudioName.Key + joinMap.InputAudioNames;

					var feedback = inputAudioName.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}

				foreach (var outputVideoName in chassis.OutputVideoNameFeedbacks)
				{
					var joinActual = outputVideoName.Key + joinMap.OutputVideoNames;

					var feedback = outputVideoName.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}
				foreach (var outputAudioName in chassis.OutputAudioNameFeedbacks)
				{
					var joinActual = outputAudioName.Key + joinMap.OutputAudioNames;

					var feedback = outputAudioName.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}


				foreach (var output in chassis.OutputNameFeedbacks)
				{
					var joinActual = output.Key + joinMap.OutputNames;

					var feedback = output.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}

				foreach (var input in chassis.OutputVideoRouteNameFeedbacks)
				{
					var joinActual = input.Key + joinMap.OutputCurrentVideoInputNames;

					var feedback = input.Value;
					// Debug.ConsoleWithLog(2, "OutputVideoRouteNameFeedbacks Link:{0}\r", joinActual);
					if (feedback == null) continue;
					
					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}

				foreach (var output in chassis.OutputAudioRouteNameFeedbacks)
				{
					var joinActual = output.Key + joinMap.OutputCurrentAudioInputNames;

					var feedback = output.Value;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.StringInput[(uint)joinActual]);
				}
				foreach (var output in chassis.OutputAudioLevelFeedbacks)
				{

					var outputLocal = output.Key;
					var volJoin = output.Key + joinMap.OutputVolume;
					var volUpJoin = output.Key + joinMap.OutputVolumeUp;
					var volDownJoin = output.Key + joinMap.OutputVolumeDown;

					var feedback = output.Value.Feedback;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.UShortInput[(uint)volJoin]);
					trilist.SetBoolSigAction((uint)volUpJoin, (b) => chassis.OutputVolumeIncrement(outputLocal));
					trilist.SetBoolSigAction((uint)volDownJoin, (b) => chassis.OutputVolumeDecrement(outputLocal));
					trilist.SetUShortSigAction((uint)volJoin, (a) => chassis.OutputVolumeSet(outputLocal, a));
				}
				foreach (var output in chassis.OutputAudioMuteFeedbacks)
				{

					var outputLocal = output.Key;
					var join = output.Key + joinMap.OutputMuteToggle;

					var feedback = output.Value.Feedback;
					if (feedback == null) continue;

					feedback.LinkInputSig(trilist.BooleanInput[(uint)join]);
					trilist.SetSigTrueAction((uint)join, () => chassis.OutputMuteToggle(outputLocal));
				}
				trilist.StringInput[joinMap.ChassisName].StringValue = chassis.Name;
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(0, "LinkToApiExt Exception:{0}\r", ex.Message);
			}
        }
    }
}