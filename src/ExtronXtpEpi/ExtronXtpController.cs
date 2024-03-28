using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Queues;

namespace ExtronXtpEpi
{
	public partial class ExtronXtpController : EssentialsBridgeableDevice, IRoutingNumericWithFeedback, ICommunicationMonitor
	{
		private const string videoCmd = "%";
		private const string audioCmd = "$";
		private const string avCmd = "!";
		private const string escape = "\x1B"; //(char)27;

		private const string CmdDelimiter = "\r";
		private const char GatherDelimiter = '\n';

		private readonly string _username;
		private readonly string _password;

		private readonly IBasicCommunication _comms;
		private readonly GenericQueue _commsRxQueue;
		private readonly GenericCommunicationMonitor _commsMonitor;

		public StatusMonitorBase CommunicationMonitor { get { return _commsMonitor; } }

		private bool _routeReady;
		public bool RouteReady
		{
			get { return _routeReady; }
			private set
			{
				_routeReady = value;

				if (_routeReady)
					UpdateFeedbacks();
			}
		}

		public bool VirtualMode { get; set; }

		public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;

		private readonly List<ExtronXtpIoConfig> inputs = new List<ExtronXtpIoConfig>();
		private readonly List<ExtronXtpIoConfig> outputs = new List<ExtronXtpIoConfig>();

		private readonly Dictionary<int, bool> videoInputSync = new Dictionary<int, bool>();
		private readonly Dictionary<int, int> videoOutputRoutes = new Dictionary<int, int>();
		private readonly Dictionary<int, int> audioOutputRoutes = new Dictionary<int, int>();

		public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
		public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

		public BoolFeedback OnlineFeedback { get; private set; }
		public IntFeedback SocketStatusFeedback { get; private set; }
		public BoolFeedback RouteReadyFeedback { get; private set; }

		public Dictionary<int, BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
		public Dictionary<int, IntFeedback> VideoOutputFeedbacks { get; private set; }
		public Dictionary<int, IntFeedback> AudioOutputFeedbacks { get; private set; }

		public Dictionary<int, StringFeedback> InputNameFeedbacks { get; private set; }
		public Dictionary<int, StringFeedback> InputAudioNameFeedbacks { get; private set; }
		public Dictionary<int, StringFeedback> InputVideoNameFeedbacks { get; private set; }

		public Dictionary<int, StringFeedback> OutputNameFeedbacks { get; private set; }
		public Dictionary<int, StringFeedback> OutputAudioNameFeedbacks { get; private set; }
		public Dictionary<int, StringFeedback> OutputVideoNameFeedbacks { get; private set; }

		public Dictionary<int, StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
		public Dictionary<int, StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }

		public Dictionary<int, IntWithFeedback> OutputAudioLevelFeedbacks { get; private set; }
		public Dictionary<int, BoolWithFeedback> OutputAudioMuteFeedbacks { get; private set; }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key"></param>
		/// <param name="name"></param>
		/// <param name="propertiesConfig"></param>
		/// <param name="comms"></param>
		public ExtronXtpController(string key, string name, ExtronXtpPropertiesConfig propertiesConfig, IBasicCommunication comms)
			: base(key, name)
		{

			Debug.Console(0, this, "Constructing new extronXtp plugin instance");

			SetRouteReady(false);

			_username = propertiesConfig.Control.TcpSshProperties.Username;
			_password = propertiesConfig.Control.TcpSshProperties.Password;

			VirtualMode = propertiesConfig.VirtualMode;

			inputs = new List<ExtronXtpIoConfig>();
			outputs = new List<ExtronXtpIoConfig>();

			inputs = propertiesConfig.Inputs;
			outputs = propertiesConfig.Outputs;

			InputPorts = new RoutingPortCollection<RoutingInputPort>();
			OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

			VideoInputSyncFeedbacks = new Dictionary<int, BoolFeedback>();
			VideoOutputFeedbacks = new Dictionary<int, IntFeedback>();
			AudioOutputFeedbacks = new Dictionary<int, IntFeedback>();

			InputNameFeedbacks = new Dictionary<int, StringFeedback>();
			InputVideoNameFeedbacks = new Dictionary<int, StringFeedback>();
			InputAudioNameFeedbacks = new Dictionary<int, StringFeedback>();

			OutputNameFeedbacks = new Dictionary<int, StringFeedback>();
			OutputVideoNameFeedbacks = new Dictionary<int, StringFeedback>();
			OutputAudioNameFeedbacks = new Dictionary<int, StringFeedback>();

			OutputVideoRouteNameFeedbacks = new Dictionary<int, StringFeedback>();
			OutputAudioRouteNameFeedbacks = new Dictionary<int, StringFeedback>();

			OutputAudioLevelFeedbacks = new Dictionary<int, IntWithFeedback>();
			OutputAudioMuteFeedbacks = new Dictionary<int, BoolWithFeedback>();

			// build comms objects
			_comms = comms;
			var commsGather = new CommunicationGather(_comms, GatherDelimiter);
			commsGather.LineReceived += OnCommsGatherLineReceived;
			_commsRxQueue = new GenericQueue(Key + "-queue");

			var polltime = propertiesConfig.PollTime ?? 30000;
			_commsMonitor = new GenericCommunicationMonitor(this, _comms, polltime, 180000, 300000, Poll);
			DeviceManager.AddDevice(_commsMonitor);

			var socket = _comms as ISocketStatus;
			if (socket != null)
			{
				socket.ConnectionChange += OnSocketConnectionChange;
				SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
			}
		}


		#region Overrides of EssentilasBridgeableDevice

		/// <summary>
		/// Links device 
		/// </summary>
		/// <param name="trilist"></param>
		/// <param name="joinStart"></param>
		/// <param name="joinMapKey"></param>
		/// <param name="bridge"></param>
		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new ExtronXtpBridgeJoinMap(joinStart);

			// This adds the join map to the collection on the bridge
			if (bridge != null)
			{
				bridge.AddJoinMap(Key, joinMap);
			}

			var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
			if (customJoins != null)
			{
				joinMap.SetCustomJoinData(customJoins);
			}

			Debug.Console(0, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
			Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

			// link joins to bridge
			LinkBaseFeedbacksToApi(trilist, joinMap);
			LinkInputsToApi(trilist, joinMap);
			LinkOutputsToApi(trilist, joinMap);

			trilist.OnlineStatusChange += (sender, args) =>
			{
				if (!args.DeviceOnLine) return;

				var ready = new CTimer(o =>
				{
					RouteReady = true;
				}, 10000);
			};
		}

		

		#endregion

		/// <summary>
		/// Overrides Essentials Initialize
		/// </summary>
		public override void Initialize()
		{
			CrestronConsole.AddNewConsoleCommand(c =>
				PrintRoutingInputPorts(),
				"listinputports",
				"prints the routing input ports",
				ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(c =>
				PrintRoutingOutputPorts(),
				"listoutputports",
				"prints the routing output ports",
				ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(
				SetVerboseMode,
				"verbosemode",
				"sets, gets extron verbose mode",
				ConsoleAccessLevelEnum.AccessOperator);

			InitializeInputs();
			InitializeOutputs();

			_comms.Connect();
			_commsMonitor.Start();

			var init = new CTimer(o => UpdateFeedbacks(), 5000);
		}

		

		private void UpdateFeedbacks()
		{
			if (OnlineFeedback != null)
				OnlineFeedback.FireUpdate();

			if (SocketStatusFeedback != null)
				SocketStatusFeedback.FireUpdate();

			if (VideoOutputFeedbacks != null)
				foreach (var item in VideoOutputFeedbacks) item.Value.FireUpdate();

			if (AudioOutputFeedbacks != null)
				foreach (var item in AudioOutputFeedbacks) item.Value.FireUpdate();

			if (InputNameFeedbacks != null)
				foreach (var item in InputNameFeedbacks) item.Value.FireUpdate();

			if (InputVideoNameFeedbacks != null)
				foreach (var item in InputVideoNameFeedbacks) item.Value.FireUpdate();

			if (InputAudioNameFeedbacks != null)
				foreach (var item in InputAudioNameFeedbacks) item.Value.FireUpdate();

			if (VideoInputSyncFeedbacks != null)
				foreach (var item in VideoInputSyncFeedbacks) item.Value.FireUpdate();

			if (OutputNameFeedbacks != null)
				foreach (var item in OutputNameFeedbacks) item.Value.FireUpdate();

			if (OutputVideoNameFeedbacks != null)
				foreach (var item in OutputVideoNameFeedbacks) item.Value.FireUpdate();

			if (OutputAudioNameFeedbacks != null)
				foreach (var item in OutputAudioNameFeedbacks) item.Value.FireUpdate();

			if (OutputVideoRouteNameFeedbacks != null)
				foreach (var item in OutputVideoRouteNameFeedbacks) item.Value.FireUpdate();

			if (OutputAudioRouteNameFeedbacks != null)
				foreach (var item in OutputAudioRouteNameFeedbacks) item.Value.FireUpdate();

			if (OutputAudioLevelFeedbacks != null)
				foreach (var item in OutputAudioLevelFeedbacks) item.Value.Feedback.FireUpdate();

			if (OutputAudioMuteFeedbacks != null)
				foreach (var item in OutputAudioMuteFeedbacks) item.Value.Feedback.FireUpdate();
		}

		/// <summary>
		/// Prints routing ports to console for debug purposes
		/// </summary>
		public void PrintRoutingInputPorts()
		{
			var line = new string('*', 50);

			Debug.Console(0, this, line);
			foreach (var item in InputPorts)
			{
				Debug.Console(0, this, "InputPort: key-'{1}', type-'{2}', parent-'{3}'",
					item.Key, item.Type, item.ParentDevice);
			}
			Debug.Console(0, this, line);
		}

		/// <summary>
		/// Prints routing ports to console for debug purposes
		/// </summary>
		public void PrintRoutingOutputPorts()
		{
			var line = new string('*', 50);

			Debug.Console(0, this, line);
			foreach (var item in OutputPorts)
			{
				Debug.Console(0, this, "OutputPort: key-'{1}', type-'{2}', parent-'{3}'",
					item.Key, item.Type, item.ParentDevice);
			}
			Debug.Console(0, this, line);
		}

		private void OnSocketConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
		{
			Debug.Console(2, this, "OnSocketConnectionChange: {0}", args.Client.ClientStatus.ToString());

			if (!args.Client.IsConnected) return;

			UpdateFeedbacks();
		}

		private void OnCommsGatherLineReceived(object sender, GenericCommMethodReceiveTextArgs args)
		{
			if (args == null)
			{
				Debug.Console(1, this, "OnCommsGatherLineReceived: args are null");
				return;
			}

			if (string.IsNullOrEmpty(args.Text))
			{
				Debug.Console(1, this, "OnCommsGatherLineReceived: args.Text is null or empty");
				return;
			}

			try
			{
				Debug.Console(1, this, "OnCommsGatherLineReceived: args.Text-'{0}'", args.Text);
				_commsRxQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessResponse));
			}
			catch (Exception ex)
			{
				Debug.Console(1, this, Debug.ErrorLogLevel.Error, "OnCommsGatherLineReceived Exception Message: {0}", ex.Message);
				Debug.Console(2, this, Debug.ErrorLogLevel.Error, "OnCommsGatherLineReceived Exception Stack Trace: {0}", ex.StackTrace);
				if (ex.InnerException != null) Debug.Console(1, this, Debug.ErrorLogLevel.Error, "OnCommsGatherLineReceived Inner Exception: '{0}'", ex.InnerException);
			}
		}

		private void ProcessResponse(string data)
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.Console(1, this, "ProcessResponse: data is empty or null");
				return;
			}

			Debug.Console(1, this, "ProcessResponse: '{0}'", data);

			if (data.Contains("Vid"))
				ProcessVideoRouteResponse(data);
			else if (data.Contains("Aud"))
				ProcessAudioRouteResponse(data);
			else if (data.Contains("Feq0 "))
				ProcessVideoInputSyncResponse(data);
			else if (data.Contains("Vol"))
				ProcessOutputVolumeResponse(data);
			else if (data.Contains("Amt"))
				ProcessOutputMuteResponse(data);
			else if (data.Contains("Extron Electronics"))
				SendInitialCommands();
			else if (data.Contains("Password:"))
				SendInitialCommands();
			else if (data.StartsWith("E"))
				ProcessErrorResponse(data);
		}

		/// <summary>
		/// Appends delimiter and sends full command to device
		/// </summary>
		/// <param name="cmd"></param>
		public void SendText(string cmd)
		{
			if (string.IsNullOrEmpty(cmd)) return;

			Debug.Console(2, this, "SendText: '{0}'", cmd);

			var fullCmd = string.Format("{0}{1}", cmd, CmdDelimiter);
			_comms.SendText(fullCmd);
		}

		/// <summary>
		/// Essentials switch method
		/// </summary>
		/// <param name="inputSelector"></param>
		/// <param name="outputSelector"></param>
		/// <param name="signalType"></param>
		public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
		{
			var input = Convert.ToInt32(inputSelector);
			var output = Convert.ToInt32(outputSelector);

			string cmd = null;

			switch (signalType)
			{
				case eRoutingSignalType.AudioVideo:
					{
						cmd = string.Format("{0}*{1}{2}", input, output, avCmd);
						break;
					}
				case eRoutingSignalType.Video:
					{
						cmd = string.Format("{0}*{1}{2}", input, output, videoCmd);
						break;
					}
				case eRoutingSignalType.Audio:
					{
						cmd = string.Format("{0}*{1}{2}", input, output, audioCmd);
						break;
					}
				default:
					{
						Debug.Console(1, this, "ExecuteSwitch: unhandled signalType '{0}'", signalType.ToString());
						break;
					}
			}

			if (cmd == null) return;

			Debug.Console(2, this, "ExecuteSwitch: cmd-'{0}'", cmd);

			SendText(cmd);

			if (!VirtualMode) return;

			ExecuteVirtualSwitch(input, output, signalType);
		}

		/// <summary>
		/// Bridge switch method
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="type"></param>
		public void ExecuteNumericSwitch(ushort input, ushort output, eRoutingSignalType type)
		{
			ExecuteSwitch(input, output, type);
		}

		/// <summary>
		/// Used to emaulate switcher responses
		/// </summary>
		/// <param name="input"></param>
		/// <param name="output"></param>
		/// <param name="type"></param>
		public void ExecuteVirtualSwitch(int input, int output, eRoutingSignalType type)
		{
			Debug.Console(2, this, "ExecuteVirtualSwitch: input-'{0}', output-{1}', type-'{2}'",
				input, output, type.ToString());

			string routeType;

			switch (type)
			{
				case eRoutingSignalType.Video:
					routeType = "Vid";
					break;
				case eRoutingSignalType.Audio:
					routeType = "Aud";
					break;
				default:
					return;
			}

			var virtualResponse = string.Format("Out{0} In{1} {2}\n", output, input, routeType);
			Debug.Console(2, this, "ExecuteVirtualSwitch: {0}", virtualResponse);

			OnCommsGatherLineReceived(this, new GenericCommMethodReceiveTextArgs(virtualResponse));
		}

		/// <summary>
		/// Polls the devices status
		/// </summary>
		public void Poll()
		{
			// query controller firmware
			SendText("Q");
			// query controller firmware (verbose)
			//SendText("0Q");
		}

		/// <summary>
		/// Polls the switcher for current route, volume and mute states
		/// </summary>
		/// <param name="o"></param>
		public void PollRoutes(object o)
		{
			foreach (var item in outputs)
			{
				var ioNumber = item.IoNumber;

				// poll video routes
				var cmd = string.Format("{0}{1}", ioNumber, videoCmd);
				SendText(cmd);

				// poll audio routes
				cmd = string.Format("{0}{1}", ioNumber, audioCmd);
				SendText(cmd);

				cmd = string.Format("{0}V", ioNumber);
				SendText(cmd);

				cmd = string.Format("{0}Z", ioNumber);
				SendText(cmd);
			}
		}

		/// <summary>
		/// Poll - view all input connections
		/// </summary>
		public void PollVideoInputSync()
		{
			SendText("0LS");
		}

		private void SendInitialCommands()
		{
			SendText(_password);
			SetVerboseMode("3");
			PollVideoInputSync();
			SetVerboseMode("1");
		}

		private void SetRouteReady(bool state)
		{
			RouteReady = state;
			Debug.Console(1, this, "SetRouteReady: {0}", RouteReady);
		}

		/// <summary>
		/// Sets the verbose mode
		/// </summary>
		/// <param name="data"></param>
		public void SetVerboseMode(string data)
		{
			var d = data.ToLower().Split(' ');
			if (d.Length == 0) return;

			var cmd = string.Format("{0}{1}CV", escape, d[1]);
			SendText(cmd);
		}

		/// <summary>
		/// Quick tie commands aka salvos
		/// </summary>
		/// <param name="cmds"></param>
		public void QuickTie(params string[] cmds)
		{
			var ties = cmds.Aggregate(string.Empty, (current, item) => current + item);
			var cmd = string.Format("{0}+Q{1}", escape, ties);
		}

		/// <summary>
		/// recalls the preset by index
		/// </summary>
		public void RecallPreset(int preset)
		{
			var cmd = string.Format("{0}R{1}PRST", escape, preset);
			SendText(cmd);
		}

		/// <summary>
		/// Increments volume for output specified
		/// </summary>
		/// <param name="output"></param>
		public void OutputVolumeIncrement(int output)
		{
			var cmd = string.Format("{0}+V", output);
			SendText(cmd);
		}

		/// <summary>
		/// Decrements volume for output specified
		/// </summary>
		/// <param name="output"></param>
		public void OutputVolumeDecrement(int output)
		{
			var cmd = string.Format("{0}-V", output);
			SendText(cmd);
		}

		/// <summary>
		/// Sets volume for output specified to the level specified
		/// </summary>
		/// <param name="output"></param>
		/// <param name="level"></param>
		public void OutputVolumeSet(int output, int level)
		{
			var cmd = string.Format("{0}+{1}V", output, level);
			SendText(cmd);
		}

		/// <summary>
		/// Turns mute on for output specified
		/// </summary>
		/// <param name="output"></param>
		public void OutputMuteOn(int output)
		{
			var cmd = string.Format("{0}*3Z", output);
			SendText(cmd);
		}

		/// <summary>
		/// Turns mute off for output specified
		/// </summary>
		/// <param name="output"></param>
		public void OutputMuteOff(int output)
		{
			var cmd = string.Format("{0}*0Z", output);
			SendText(cmd);
		}

		/// <summary>
		/// Toggles mute on for output specified based on current feedback
		/// </summary>
		/// <param name="output"></param>
		public void OutputMuteToggle(int output)
		{
			BoolWithFeedback feedback;
			if (!OutputAudioMuteFeedbacks.TryGetValue(output, out feedback))
			{
				Debug.Console(2, this, "OutputMuteToggle: output '{0}' not found in OutputAudioMuteFeedbacks", output);
				return;
			}

			var state = feedback.Value ? 0 : 3;
			var cmd = string.Format("{0}*{1}Z", output, state);
			SendText(cmd);
		}
	}
}