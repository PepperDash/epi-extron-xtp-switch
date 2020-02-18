using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Bridges;

using ExtronXtpEpi.Config;

namespace ExtronXtpEpi
{
    public class ExtronXtpChassis : Device, IRoutingInputsOutputs, IRouting, IBridge
    {
        static readonly string videoCmd = "%";
        static readonly string audioCmd = "$";
        static readonly char escape = (char)27;

        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        private readonly CommunicationGather portGather;
        private readonly ExtronXtpPropsConfig deviceConfig;
        private readonly ExtronCmdProcessor cmdProcessor = new ExtronCmdProcessor();
        private CTimer pollTimer;

        private readonly RoutingPortCollection<RoutingInputPort> inputPorts = new RoutingPortCollection<RoutingInputPort>();
        private readonly RoutingPortCollection<RoutingOutputPort> outputPorts = new RoutingPortCollection<RoutingOutputPort>();
        private readonly List<ExtronXtpIo> inputs = new List<ExtronXtpIo>();
        private readonly List<ExtronXtpIo> outputs = new List<ExtronXtpIo>();

        private readonly Dictionary<int, int> videoRoutes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> audioRoutes = new Dictionary<int, int>();
        private readonly Dictionary<int, bool> videoInputSync = new Dictionary<int, bool>();

        public Dictionary<int, IntFeedback> VideoOutputFeedbacks { get; private set; }
        public Dictionary<int, IntFeedback> AudioOutputFeedbacks { get; private set; }
        public Dictionary<int, BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public Dictionary<int, StringFeedback> InputNameFeedbacks { get; private set; }
        public Dictionary<int, StringFeedback> OutputNameFeedbacks { get; private set; }
        public Dictionary<int, StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public Dictionary<int, StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }

        public IEnumerable<ExtronXtpIo> Inputs { get { return inputs; } }
        public IEnumerable<ExtronXtpIo> Outputs { get { return outputs; } }

        public ExtronXtpChassis(DeviceConfig config)
            : base(config.Key, config.Name)
        {
            deviceConfig = ExtronXtpPropsConfig.FromConfig(config);

            Communication = CommFactory.CreateCommForDevice(config);
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, "Q");

            var socket = Communication as ISocketStatus;
            if (socket != null)
            {
                socket.ConnectionChange += (sender, args) =>
                {
                    Debug.Console(2, this, args.Client.ClientStatus.ToString());
                    if (ConnectFb != null) ConnectFb.FireUpdate();
                }; 
            }

            VideoOutputFeedbacks = new Dictionary<int, IntFeedback>();
            AudioOutputFeedbacks = new Dictionary<int, IntFeedback>();
            VideoInputSyncFeedbacks = new Dictionary<int, BoolFeedback>();
            InputNameFeedbacks = new Dictionary<int, StringFeedback>();
            OutputNameFeedbacks = new Dictionary<int, StringFeedback>();
            OutputVideoRouteNameFeedbacks = new Dictionary<int, StringFeedback>();
            OutputAudioRouteNameFeedbacks = new Dictionary<int, StringFeedback>();

            ConnectFb = new BoolFeedback(() => Connect);
            portGather = new CommunicationGather(Communication, '\n');  

            AddPostActivationAction(() => portGather.LineReceived += HandleLineReceived);
            AddPostActivationAction(() => Connect = true);
            AddPostActivationAction(() => pollTimer = new CTimer(Poll, null, 30000, 30000));
        }

        public override bool CustomActivate()
        {
            CrestronConsole.AddNewConsoleCommand(s => ShowAllRoutingPorts(), "showroutingports", "", ConsoleAccessLevelEnum.AccessOperator);
            SetupInputs();
            SetupOutputs();

            return true;
        }

        public static void LoadPlugin()
        {
            DeviceFactory.AddFactoryForType("extronXtp", (config) =>
                {
                    return new ExtronXtpChassis(config);
                });
        }

        public BoolFeedback ConnectFb { get; private set; }
        public bool Connect
        {
            get { return Communication.IsConnected; }
            set
            {
                if (value == true)
                {
                    Communication.Connect();
                    CommunicationMonitor.Start();
                }
                else
                {
                    Communication.Disconnect();
                    CommunicationMonitor.Stop();
                }
            }
        }

        public BoolFeedback OkFb { get { return CommunicationMonitor.IsOnlineFeedback; } }

        #region IRoutingInputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts
        {
            get { return inputPorts; }
        }

        #endregion

        #region IRoutingOutputs Members

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { return outputPorts; }
        }

        #endregion

        #region IRouting Members

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            var input = Convert.ToInt32(inputSelector);
            var output = Convert.ToInt32(outputSelector);
            string cmd = null;

            switch (signalType)
            {
                case eRoutingSignalType.AudioVideo:
                    {
                        var builder = new StringBuilder();
                        builder.Append(TieVideoInputCmd(input, output));
                        builder.Append(TieAudioInputCmd(input, output));

                        cmd = builder.ToString();
                        break;
                    }
                case eRoutingSignalType.Video:
                    {
                        cmd = TieVideoInputCmd(input, output);
                        break;
                    }
                case eRoutingSignalType.Audio:
                    {
                        cmd = TieAudioInputCmd(input, output);
                        break;
                    }
            }

            if (cmd == null) return;

            Debug.Console(2, this, "Sending CMD: {0}", cmd);
            Communication.SendText(cmd);
        }

        #endregion
        
        private void HandleLineReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            var result = (ExtronXtpResponses)Enum.Parse(typeof(ExtronXtpResponses), e.Text, false);
            switch (result)
            {
                case ExtronXtpResponses.Vid:
                    {
                        cmdProcessor.EnqueueTask(() => ProcessVideoUpdateResponse(e.Text));
                        break;
                    }
                case ExtronXtpResponses.Aud:
                    {
                        cmdProcessor.EnqueueTask(() => ProcessAudioUpdateResponse(e.Text));
                        break;
                    }
                case ExtronXtpResponses.Frq:
                    {
                        cmdProcessor.EnqueueTask(() => ProcessSignalSyncUpdateResponse(e.Text));
                        break;
                    }
            }
        }

        private void SetupInputs()
        {
            foreach (var input in deviceConfig.Inputs)
            {
                var videoName = new StringBuilder(Key);
                videoName.Append("-");
                videoName.Append(input.VideoName);
                videoName.Replace(" ", "");
                inputPorts.Add(new RoutingInputPort(videoName.ToString(), eRoutingSignalType.Video, eRoutingPortConnectionType.Streaming, input.IoNumber, this));

                var audioName = new StringBuilder(Key);
                audioName.Append("-");
                audioName.Append(input.AudioName);
                audioName.Replace(" ", "");
                inputPorts.Add(new RoutingInputPort(audioName.ToString(), eRoutingSignalType.Audio, eRoutingPortConnectionType.Streaming, input.IoNumber, this));

                inputs.Add(input);

                videoInputSync.Add(input.IoNumber, false);
                VideoInputSyncFeedbacks.Add(input.IoNumber, new BoolFeedback(() =>
                    {
                        bool result;
                        return videoInputSync.TryGetValue(input.IoNumber, out result) ? result : false;
                    }));

                var nameFb = new StringFeedback(() => input.Name ?? string.Empty);

                InputNameFeedbacks.Add(input.IoNumber, nameFb);
                nameFb.FireUpdate();
            }
        }

        private void SetupOutputs()
        {
            foreach (var output in deviceConfig.Outputs)
            {
                var videoName = new StringBuilder(Key);
                videoName.Append("-");
                videoName.Append(output.VideoName);
                videoName.Replace(" ", "");
                inputPorts.Add(new RoutingInputPort(videoName.ToString(), eRoutingSignalType.Video, eRoutingPortConnectionType.Streaming, output.IoNumber, this));

                var audioName = new StringBuilder(Key);
                audioName.Append("-");
                audioName.Append(output.AudioName);
                audioName.Replace(" ", "");
                inputPorts.Add(new RoutingInputPort(videoName.ToString(), eRoutingSignalType.Audio, eRoutingPortConnectionType.Streaming, output.IoNumber, this));

                outputs.Add(output);

                videoRoutes.Add(output.IoNumber, 0);
                audioRoutes.Add(output.IoNumber, 0);

                VideoOutputFeedbacks.Add(output.IoNumber, new IntFeedback(() =>
                    {
                        int result;
                        return videoRoutes.TryGetValue(output.IoNumber, out result) ? result : 0;
                    }));

                AudioOutputFeedbacks.Add(output.IoNumber, new IntFeedback(() =>
                    {
                        int result;
                        return audioRoutes.TryGetValue(output.IoNumber, out result) ? result : 0;
                    }));

                OutputVideoRouteNameFeedbacks.Add(output.IoNumber, new StringFeedback(() =>
                    {
                        int result;
                        if (!videoRoutes.TryGetValue(output.IoNumber, out result)) return string.Empty;

                        var source = Inputs.FirstOrDefault(x => x.IoNumber == result);
                        return source.VideoName ?? "No Source";
                    }));

                OutputAudioRouteNameFeedbacks.Add(output.IoNumber, new StringFeedback(() =>
                {
                    int result;
                    if (!audioRoutes.TryGetValue(output.IoNumber, out result)) return string.Empty;

                    var source = Inputs.FirstOrDefault(x => x.IoNumber == result);
                    return source.AudioName ?? "No Source";
                }));

                var nameFb = new StringFeedback(() => output.Name ?? string.Empty);
                OutputNameFeedbacks.Add(output.IoNumber, nameFb);
                nameFb.FireUpdate();
            }
        }

        private void SendInitialCommands()
        {
            Communication.SendText(SetVerboseMode(1));
            Poll(null);
            Communication.SendText("0LS");
        }

        private void Poll(object o)
        {
            foreach (var output in outputs)
            {
                var videoCmdToSend = new StringBuilder();
                videoCmdToSend.Append(output.IoNumber);
                videoCmdToSend.Append(videoCmd);
                Communication.SendText(videoCmdToSend.ToString());

                var audioCmdToSend = new StringBuilder();
                audioCmdToSend.Append(output.IoNumber);
                audioCmdToSend.Append(audioCmd);
                Communication.SendText(audioCmdToSend.ToString());
            }
        }

        private void ShowAllRoutingPorts()
        {
            foreach (var item in InputPorts)
            {
                Debug.Console(0, this, "Key:{0}", item.Key);
                //Debug.Console(0, this, "{0}", item.Port);
                Debug.Console(0, this, "Type:{0}", item.Type);
                Debug.Console(0, this, "Parent:{0}\r", item.ParentDevice.Key);
            }

            foreach (var item in OutputPorts)
            {
                Debug.Console(0, this, "Key:{0}", item.Key);
                //Debug.Console(0, this, "{0}", item.Port);
                Debug.Console(0, this, "Type:{0}", item.Type);
                Debug.Console(0, this, "Parent:{0}\r", item.ParentDevice.Key);
            }
        }

        private void UpdateVideoRoute(int output, int value)
        {
            videoRoutes[output] = value;

            IntFeedback feedback;
            if (VideoOutputFeedbacks.TryGetValue(output, out feedback))
            {
                if (feedback != null) feedback.FireUpdate();
            }

            StringFeedback nameFeedback;
            if (!OutputVideoRouteNameFeedbacks.TryGetValue(output, out nameFeedback))
            {
                if (nameFeedback != null) nameFeedback.FireUpdate();
            }
        }

        private void UpdateAudioRoute(int output, int value)
        {
            audioRoutes[output] = value;

            IntFeedback feedback;
            if (AudioOutputFeedbacks.TryGetValue(output, out feedback))
            {
                if (feedback != null) feedback.FireUpdate();
            }

            StringFeedback nameFeedback;
            if (!OutputAudioRouteNameFeedbacks.TryGetValue(output, out nameFeedback))
            {
                if (nameFeedback != null) nameFeedback.FireUpdate();
            }
        }

        private void ProcessVideoUpdateResponse(string response)
        {
            var responses = response.Split(' ');

            var input = Convert.ToInt32(responses.SingleOrDefault(x => x.Contains("In")));
            var output = Convert.ToInt32(responses.SingleOrDefault(x => x.Contains("Out")));

            if (output == 0) return;
            UpdateVideoRoute(output, input);
        }

        private void ProcessAudioUpdateResponse(string response)
        {
            var responses = response.Split(' ');

            var input = Convert.ToInt32(responses.SingleOrDefault(x => x.Contains("In")));
            var output = Convert.ToInt32(responses.SingleOrDefault(x => x.Contains("Out")));

            if (output == 0) return;
            UpdateAudioRoute(output, input);
        }

        private void ProcessSignalSyncUpdateResponse(string response)
        {
            var responses = response.Split(' ');
            var pertinentInfo = responses[1];
            for (int x = 1; x <= pertinentInfo.Length; x++)
            {
                var status = pertinentInfo[x];
                if (!videoInputSync.ContainsKey(x)) continue;

                var sync = Convert.ToInt32(status);

                if (status == 1) videoInputSync[x] = true;
                else videoInputSync[x] = false;

                BoolFeedback feedback;
                if (!VideoInputSyncFeedbacks.TryGetValue(x, out feedback)) return;

                if (feedback != null) feedback.FireUpdate();
            }
        }

        static string TieVideoInputCmd(int input, int output)
        {
            var builder = new StringBuilder();
            builder.Append(input);
            builder.Append("*");
            builder.Append(output);
            builder.Append(videoCmd);

            return builder.ToString();
        }

        static string TieAudioInputCmd(int input, int output)
        {
            var builder = new StringBuilder();
            builder.Append(input);
            builder.Append("*");
            builder.Append(output);
            builder.Append(audioCmd);

            return builder.ToString();
        }

        static string QuickTieCmd(params string[] cmds)
        {
            var builder = new StringBuilder();
            builder.Append(escape);
            builder.Append("+Q");
            foreach (var cmd in cmds)
            {
                builder.Append(cmd);
            }
            builder.Append("\r");

            return builder.ToString();
        }

        static string RecallPresetCmd(int preset)
        {
            var builder = new StringBuilder();
            builder.Append(escape);
            builder.Append("R");
            builder.Append(preset);
            builder.Append("PRST");
            builder.Append("\r");

            return builder.ToString();
        }

        static string SetVerboseMode(int mode)
        {
            var builder = new StringBuilder();
            builder.Append(escape);
            builder.Append(mode);
            builder.Append("CV");
            builder.Append("\r");

            return builder.ToString();
        }

        #region IBridge Members

        public void LinkToApi(Crestron.SimplSharpPro.DeviceSupport.BasicTriList trilist, uint joinStart, string joinMapKey)
        {
            this.LinkToApiExt(trilist, joinStart, joinMapKey);
        }

        #endregion
    }
}