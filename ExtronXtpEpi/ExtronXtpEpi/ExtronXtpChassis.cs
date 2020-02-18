using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace ExtronXtpEpi
{
    public class ExtronXtpChassis : Device, IRoutingInputsOutputs, IRouting
    {
        static readonly string videoCmd = "&";
        static readonly string audioCmd = "$";
        static readonly char escape = (char)27;

        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        private readonly CommunicationGather portGather;
        private readonly DeviceConfig config;

        public Dictionary<uint, IntFeedback> VideoOutputFeedbacks { get; private set; }
        public Dictionary<uint, IntFeedback> AudioOutputFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }

        public Dictionary<uint, string> InputNames { get; set; }
        public Dictionary<uint, string> OutputNames { get; set; }

        public ExtronXtpChassis(DeviceConfig config)
            : base(config.Key, config.Name)
        {
            this.config = config;

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

            ConnectFb = new BoolFeedback(() => Connect);
            portGather = new CommunicationGather(Communication, '\n');

            AddPostActivationAction(() => Connect = true);
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
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IRoutingOutputs Members

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IRouting Members

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            throw new NotImplementedException();
        }

        #endregion

        static string TieVideoInputCmd(uint input, uint output)
        {
            var builder = new StringBuilder(100);
            builder.Append(input);
            builder.Append("*");
            builder.Append(output);
            builder.Append(videoCmd);

            return builder.ToString();
        }

        static string TieAudioInputCmd(uint input, uint output)
        {
            var builder = new StringBuilder(100);
            builder.Append(input);
            builder.Append("*");
            builder.Append(output);
            builder.Append(audioCmd);

            return builder.ToString();
        }

        static string QuickTieCmd(params string[] cmds)
        {
            var builder = new StringBuilder(100);
            builder.Append(escape);
            builder.Append("+Q");
            foreach (var cmd in cmds)
            {
                builder.Append(cmd);
            }
            builder.Append("\r");

            return builder.ToString();
        }

        static string RecallPresetCmd(uint preset)
        {
            var builder = new StringBuilder(50);
            builder.Append(escape);
            builder.Append("R");
            builder.Append(preset);
            builder.Append("PRST");
            builder.Append("r");

            return builder.ToString();
        }
    }
}