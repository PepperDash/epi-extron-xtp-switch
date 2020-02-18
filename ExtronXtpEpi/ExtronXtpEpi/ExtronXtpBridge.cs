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
            var joinMap = new DmChassisControllerJoinMap();
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
        }
    }
}