using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Queues;

namespace ExtronXtpEpi
{
	public class ExtronXtpController : EssentialsBridgeableDevice, IRoutingNumericWithFeedback, ICommunicationMonitor
	{
		private const string VideoCmd = "%";
		private const string AudioCmd = "$";
		private const string AvCmd = "!";
		private const char Escape = (char)27; //"\x1B";

		private Dictionary<string, eRoutingSignalType> _typeMap = new Dictionary<string, eRoutingSignalType>
			{
				{"!", eRoutingSignalType.AudioVideo},
				{"%", eRoutingSignalType.Video},
				{"$", eRoutingSignalType.Audio},
			};

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

				Debug.Console(DebugExt.Debug, this, "RouteReady: {0}", _routeReady);

				if (_routeReady)
					UpdateFeedbacks();
			}
		}

		public bool VirtualMode { get; set; }

		public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;

		private readonly List<ExtronXtpIoConfig> _inputs = new List<ExtronXtpIoConfig>();
		private readonly List<ExtronXtpIoConfig> _outputs = new List<ExtronXtpIoConfig>();

		private readonly Dictionary<int, bool> _videoInputSync = new Dictionary<int, bool>();
		private readonly Dictionary<int, int> _videoOutputRoutes = new Dictionary<int, int>();
		private readonly Dictionary<int, int> _audioOutputRoutes = new Dictionary<int, int>();

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

			Debug.Console(DebugExt.Trace, this, "Constructing new extronXtp plugin instance");

			SetRouteReady(false);

			_username = propertiesConfig.Control.TcpSshProperties.Username;
			_password = propertiesConfig.Control.TcpSshProperties.Password;

			VirtualMode = propertiesConfig.VirtualMode;

			_inputs = propertiesConfig.Inputs;
			_outputs = propertiesConfig.Outputs;

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

			OnlineFeedback = new BoolFeedback(() => _comms.IsConnected);
			RouteReadyFeedback = new BoolFeedback(() => RouteReady);

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
			Debug.Console(DebugExt.Trace, this, "**********************");
			Debug.Console(DebugExt.Trace, this, "*****> LinkToApi");
			Debug.Console(DebugExt.Trace, this, "**********************");


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

			Debug.Console(DebugExt.Trace, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
			Debug.Console(DebugExt.Trace, this, "Linking to Bridge Type {0}", GetType().Name);

			// link joins to bridge
			OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnlineXtpLegacy.JoinNumber]);
			SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.SocketStatus.JoinNumber]);
			RouteReadyFeedback.LinkInputSig(trilist.BooleanInput[joinMap.RouteReady.JoinNumber]);

			LinkInputsToApi(trilist, joinMap);
			LinkOutputsToApi(trilist, joinMap);

			trilist.OnlineStatusChange += (sender, args) =>
			{
				if (!args.DeviceOnLine) return;

				var ready = new CTimer(o => { RouteReady = true; }, 10000);
			};
		}

		private void LinkInputsToApi(BasicTriList trilist, ExtronXtpBridgeJoinMap joinMap)
		{
			Debug.Console(DebugExt.Trace, this, "**********************");
			Debug.Console(DebugExt.Trace, this, "*****> LinkInputsToApi");
			Debug.Console(DebugExt.Trace, this, "**********************");

			// input names
			Debug.Console(DebugExt.Trace, this, "*****> InputNameFeedbacks.Count: {0}", InputNameFeedbacks.Count());
			foreach (var item in InputNameFeedbacks)
			{
				var join = (ushort)(joinMap.InputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				Debug.Console(DebugExt.Trace, this, "LinkInputsToApi: InputNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);

				feedback.LinkInputSig(trilist.StringInput[join]);
			}

			// video input names
			Debug.Console(DebugExt.Trace, this, "*****> InputVideoNameFeedbacks.Count: {0}", InputVideoNameFeedbacks.Count());
			foreach (var item in InputVideoNameFeedbacks)
			{
				var join = (ushort)(joinMap.InputVideoNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Trace, this, "LinkInputsToApi: InputVideoNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// audio input names
			Debug.Console(DebugExt.Trace, this, "*****> InputAudioNameFeedbacks.Count: {0}", InputAudioNameFeedbacks.Count());
			foreach (var item in InputAudioNameFeedbacks)
			{
				var join = (ushort)(joinMap.InputAudioNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Trace, this, "LinkInputsToApi: InputAudioNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// input video syncs
			Debug.Console(DebugExt.Trace, this, "*****> VideoInputSyncFeedbacks.Count: {0}", VideoInputSyncFeedbacks.Count());
			foreach (var item in VideoInputSyncFeedbacks)
			{
				var join = (ushort)(joinMap.VideoSyncStatus.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.BooleanInput[join]);

				Debug.Console(DebugExt.Trace, this, "LinkInputsToApi: VideoInputSyncFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}
		}

		private void LinkOutputsToApi(BasicTriList trilist, ExtronXtpBridgeJoinMap joinMap)
		{
			Debug.Console(DebugExt.Trace, this, "***********************");
			Debug.Console(DebugExt.Trace, this, "*****> LinkOutputsToApi");
			Debug.Console(DebugExt.Trace, this, "***********************");

			// output names
			Debug.Console(DebugExt.Trace, this, "*****> OutputNameFeedbacks.Count: {0}", OutputNameFeedbacks.Count());
			foreach (var item in OutputNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: OutputNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// video output names
			Debug.Console(DebugExt.Trace, this, "*****> OutputVideoNameFeedbacks.Count: {0}", OutputVideoNameFeedbacks.Count());
			foreach (var item in OutputVideoNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputVideoNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: OutputVideoNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// audio output names
			Debug.Console(DebugExt.Trace, this, "*****> OutputAudioNameFeedbacks.Count: {0}", OutputAudioNameFeedbacks.Count());
			foreach (var item in OutputAudioNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputAudioNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: OutputAudioNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// video routes
			Debug.Console(DebugExt.Trace, this, "*****> VideoOutputFeedbacks.Count: {0}", VideoOutputFeedbacks.Count());
			foreach (var item in VideoOutputFeedbacks)
			{
				var join = (ushort)(joinMap.OutputVideo.JoinNumber + item.Key - 1);

				var output = item.Key;
				trilist.SetUShortSigAction(join, input =>
				{
					// dont allow routes until RouteReady == true;
					if (RouteReady) ExecuteSwitch(input, output, eRoutingSignalType.Video);
				});

				var feedback = item.Value;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.UShortInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: VideoOutputFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// audio routes
			Debug.Console(DebugExt.Trace, this, "*****> AudioOutputFeedbacks.Count: {0}", AudioOutputFeedbacks.Count());
			foreach (var item in AudioOutputFeedbacks)
			{
				var join = (ushort)(joinMap.OutputAudio.JoinNumber + item.Key - 1);

				var output = item.Key;
				trilist.SetUShortSigAction(join, input =>
				{
					// dont allow routes until RouteReady == true;
					if (RouteReady) ExecuteSwitch(input, output, eRoutingSignalType.Audio);
				});

				var feedback = item.Value;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.UShortInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: AudioOutputFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// video routed names
			Debug.Console(DebugExt.Trace, this, "*****> OutputVideoNameFeedbacks.Count: {0}", OutputVideoNameFeedbacks.Count());
			foreach (var item in OutputVideoNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputCurrentVideoInputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: OutputVideoNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// audio routed names
			Debug.Console(DebugExt.Trace, this, "*****> OutputAudioNameFeedbacks.Count: {0}", OutputAudioNameFeedbacks.Count());
			foreach (var item in OutputAudioNameFeedbacks)
			{
				var join = (ushort)(joinMap.OutputCurrentAudioInputNames.JoinNumber + item.Key - 1);
				var feedback = item.Value;

				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.StringInput[join]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: OutputAudioNameFeedbacks join-'{0}', feedback.Key-'{1}'",
					join, feedback.Key);
			}

			// output volumes
			Debug.Console(DebugExt.Trace, this, "*****> OutputAudioLevelFeedbacks.Count: {0}", OutputAudioLevelFeedbacks.Count());
			foreach (var item in OutputAudioLevelFeedbacks)
			{
				var volIncJoin = (ushort)(joinMap.OutputVolumeUp.JoinNumber + item.Key - 1);
				var volDecJoin = (ushort)(joinMap.OutputVolumeDown.JoinNumber + item.Key - 1);
				var volSetJoin = (ushort)(joinMap.OutputVolume.JoinNumber + item.Key - 1);
				var output = item.Key;

				trilist.SetUShortSigAction(volIncJoin, b => OutputVolumeIncrement(output));
				trilist.SetUShortSigAction(volDecJoin, b => OutputVolumeDecrement(output));
				trilist.SetUShortSigAction(volSetJoin, a => OutputVolumeSet(output, a));

				var feedback = item.Value.Feedback;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.UShortInput[volSetJoin]);

				Debug.Console(DebugExt.Verbose, this,
					"LinkOutputsToApi: OutputAudioLevelFeedbacks output-'{0}' volIncJoin-'{1}', volDecJoin-'{2}', volSetJoin-'{3}', feedback.Key-'{4}'",
					output, volIncJoin, volDecJoin, volSetJoin, feedback.Key);
			}

			// output mutes
			Debug.Console(DebugExt.Trace, this, "*****> OutputAudioMuteFeedbacks.Count: {0}", OutputAudioMuteFeedbacks.Count());
			foreach (var item in OutputAudioMuteFeedbacks)
			{
				var muteTogJoin = (ushort)(joinMap.OutputMuteToggle.JoinNumber + item.Key - 1);
				var output = item.Key;

				trilist.SetSigFalseAction(muteTogJoin, () => OutputMuteToggle(output));

				var feedback = item.Value.Feedback;
				if (feedback == null) continue;

				feedback.LinkInputSig(trilist.BooleanInput[muteTogJoin]);

				Debug.Console(DebugExt.Verbose, this, "LinkOutputsToApi: OutputAudioMuteFeedbacks output-'{0}', muteTogJoin-'{1}', feedback.Key-'{2}'",
					output, muteTogJoin, feedback.Key);
			}
		}

		#endregion


		#region overrides Essentials Initialize

		/// <summary>
		/// Overrides Essentials CustomActivate
		/// </summary>
		/// <returns></returns>
		public override bool CustomActivate()
		{
			InitializeInputs();
			InitializeOutputs();
			return base.CustomActivate();
		}

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
				"sets verbose mode, 1-3",
				ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(
				MakeRoute,
				"makeroute",
				"makeroute {in}*{out}{level}",
				ConsoleAccessLevelEnum.AccessOperator);

			_comms.Connect();
			_commsMonitor.Start();

			var init = new CTimer(o => UpdateFeedbacks(), 5000);
		}

		private void InitializeInputs()
		{
			foreach (var item in _inputs)
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
				_videoInputSync.Add(ioNumber, false);
				VideoInputSyncFeedbacks.Add(item.IoNumber, new BoolFeedback(() =>
				{
					bool result;
					return _videoInputSync.TryGetValue(ioNumber, out result) && result;
				}));

				// initialize name feedbacks				
				InputNameFeedbacks.Add(ioNumber, new StringFeedback(() => name ?? string.Empty));
				InputVideoNameFeedbacks.Add(ioNumber, new StringFeedback(() => videoName ?? string.Empty));
				InputAudioNameFeedbacks.Add(ioNumber, new StringFeedback(() => audioName ?? string.Empty));

				Debug.Console(DebugExt.Verbose, this, "InitializeInputs: ioNumber-'{0}', name-'{1}', videoName-'{2}', audioName-'{3}', routingPortVideoName-'{4}', routingPortAudioName-'{5}'",
					ioNumber, name, videoName, audioName, routingPortVideoName, routingPortAudioName);
			}
		}

		private void InitializeOutputs()
		{
			foreach (var item in _outputs)
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

				_videoOutputRoutes.Add(ioNumber, 0);
				_audioOutputRoutes.Add(ioNumber, 0);

				// initialize route feedbacks
				VideoOutputFeedbacks.Add(ioNumber, new IntFeedback(() =>
				{
					int result;
					return _videoOutputRoutes.TryGetValue(ioNumber, out result) ? result : 0;
				}));

				// initialize route feedbacks
				AudioOutputFeedbacks.Add(ioNumber, new IntFeedback(() =>
				{
					int result;
					return _audioOutputRoutes.TryGetValue(ioNumber, out result) ? result : 0;
				}));

				// initialize name feedbacks
				OutputNameFeedbacks.Add(ioNumber, new StringFeedback(() => name ?? string.Empty));
				OutputVideoNameFeedbacks.Add(ioNumber, new StringFeedback(() => videoName ?? string.Empty));
				OutputAudioNameFeedbacks.Add(ioNumber, new StringFeedback(() => audioName ?? string.Empty));

				// initialize routed name feedbacks
				OutputVideoRouteNameFeedbacks.Add(ioNumber, new StringFeedback(() =>
				{
					int result;
					if (!_videoOutputRoutes.TryGetValue(ioNumber, out result))
						return "Unknown";

					var src = _inputs.First(i => i.IoNumber == result);
					return src.VideoName ?? "No Source";
				}));

				// initialize routed name feedbacks
				OutputAudioRouteNameFeedbacks.Add(ioNumber, new StringFeedback(() =>
				{
					int result;
					if (!_audioOutputRoutes.TryGetValue(ioNumber, out result))
						return "Unknown";

					var src = _inputs.FirstOrDefault(i => i.IoNumber == result);
					return src.AudioName ?? "No Source";
				}));

				// initialize audio level/mute feedbacks
				OutputAudioLevelFeedbacks.Add(ioNumber, new IntWithFeedback());
				OutputAudioMuteFeedbacks.Add(ioNumber, new BoolWithFeedback());

				Debug.Console(DebugExt.Verbose, this, "InitializeOutputs: ioNumber-'{0}', name-'{1}', videoName-'{2}', audioName-'{3}', routingPortVideoName-'{4}', routingPortAudioName-'{5}'",
					ioNumber, name, videoName, audioName, routingPortVideoName, routingPortAudioName);
			}
		}

		#endregion

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

		private void OnSocketConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
		{
			Debug.Console(DebugExt.Verbose, this, "OnSocketConnectionChange: {0}", args.Client.ClientStatus.ToString());

			if (!args.Client.IsConnected) return;

			UpdateFeedbacks();
		}

		private void OnCommsGatherLineReceived(object sender, GenericCommMethodReceiveTextArgs args)
		{
			if (args == null)
			{
				Debug.Console(DebugExt.Debug, this, "OnCommsGatherLineReceived: args are null");
				return;
			}

			if (string.IsNullOrEmpty(args.Text))
			{
				Debug.Console(DebugExt.Debug, this, "OnCommsGatherLineReceived: args.Text is null or empty");
				return;
			}

			try
			{
				Debug.Console(DebugExt.Debug, this, "OnCommsGatherLineReceived: args.Text-'{0}'", args.Text);
				_commsRxQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessResponse));
			}
			catch (Exception ex)
			{
				Debug.Console(DebugExt.Debug, this, Debug.ErrorLogLevel.Error, "OnCommsGatherLineReceived Exception Message: {0}", ex.Message);
				Debug.Console(DebugExt.Verbose, this, Debug.ErrorLogLevel.Error, "OnCommsGatherLineReceived Exception Stack Trace: {0}", ex.StackTrace);
				if (ex.InnerException != null) Debug.Console(DebugExt.Debug, this, Debug.ErrorLogLevel.Error, "OnCommsGatherLineReceived Inner Exception: '{0}'", ex.InnerException);
			}
		}

		private void ProcessResponse(string data)
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.Console(DebugExt.Debug, this, "ProcessResponse: data is empty or null");
				return;
			}

			Debug.Console(DebugExt.Debug, this, "ProcessResponse: '{0}'", data);

			if (data.Contains("Vid"))
			{
				ProcessVideoRouteResponse(data);
			}
			else if (data.Contains("Aud"))
			{
				ProcessAudioRouteResponse(data);
			}
			else if (data.Contains("All"))
			{
				ProcessVideoRouteResponse(data);
				ProcessAudioRouteResponse(data);
			}
			else if (data.Contains("Feq0 "))
			{
				ProcessVideoInputSyncResponse(data);
			}
			else if (data.Contains("Vol"))
			{
				ProcessOutputVolumeResponse(data);
			}
			else if (data.Contains("Amt"))
			{
				ProcessOutputMuteResponse(data);
			}
			else if (data.Contains("Extron Electronics"))
			{
				SendInitialCommands();
			}
			else if (data.Contains("Password:"))
			{
				SendInitialCommands();
			}
			else if (data.StartsWith("E"))
			{
				ProcessErrorResponse(data);
			}
			else
			{
				Debug.Console(DebugExt.Debug, this, "ProcessResponse: unhandled response '{0}'", data);
			}
		}

		private void ProcessVideoRouteResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var input = Convert.ToInt32(responses[1].Replace("In", ""));
				var output = Convert.ToInt32(responses[0].Replace("Out", ""));

				Debug.Console(DebugExt.Verbose, this, "ProcessVideoRouteResponse: input-'{0}', output-'{1}'",
					input, output);

				if (output == 0) return;

				_videoOutputRoutes[output] = input;

				IntFeedback outputFeedback;
				if (VideoOutputFeedbacks.TryGetValue(output, out outputFeedback))
					outputFeedback.FireUpdate();
				else
					Debug.Console(DebugExt.Debug, this, "ProcessVideoRouteResponse: failed to get VideoOutputFeedbacks item for output-'{0}'",
						output);

				StringFeedback nameFeedback;
				if (OutputVideoRouteNameFeedbacks.TryGetValue(output, out nameFeedback))
					nameFeedback.FireUpdate();
				else
					Debug.Console(DebugExt.Debug, this, "ProcessVideoRouteResponse: failed to get OutputVideoRouteNameFeedbacks item for output-'{0}'",
						output);
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessVideoRouteResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessAudioRouteResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var input = Convert.ToInt32(responses[1].Replace("In", ""));
				var output = Convert.ToInt32(responses[0].Replace("Out", ""));

				Debug.Console(DebugExt.Verbose, this, "ProcessAudioRouteResponse: input-'{0}', output-'{1}'",
					input, output);

				if (output == 0) return;

				_audioOutputRoutes[output] = input;

				IntFeedback outputFeedback;
				if (AudioOutputFeedbacks.TryGetValue(output, out outputFeedback))
					outputFeedback.FireUpdate();

				StringFeedback nameFeedback;
				if (OutputAudioNameFeedbacks.TryGetValue(output, out nameFeedback))
					nameFeedback.FireUpdate();
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessAudioRouteResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessVideoInputSyncResponse(string data)
		{
			try
			{
				var pertinentInfo = data.Split(' ')[1];
				for (var i = 1; i <= pertinentInfo.Length; i++)
				{
					var status = pertinentInfo[i];
					if (!_videoInputSync.ContainsKey(i)) continue;

					var sync = Convert.ToInt32(status);

					_videoInputSync[i] = (sync == 1);

					BoolFeedback feedback;
					if (!VideoInputSyncFeedbacks.TryGetValue(i, out feedback)) return;

					if (feedback == null) return;

					feedback.FireUpdate();
				}
			}
			catch (Exception ex)
			{
				Debug.Console(DebugExt.Debug, this, "ProcessSignalSyncResponse Exception: {0}", ex.Message);
			}
		}

		private void ProcessOutputVolumeResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var volume = Convert.ToInt32(responses[1].Replace("Vol", ""));
				var output = Convert.ToInt32(responses[0].Replace("Out", ""));

				Debug.Console(DebugExt.Verbose, this, "ProcessOutputVolumeResponse: output-'{0}', volume-'{1}",
					output, volume);

				if (output == 0) return;

				IntWithFeedback feedback;
				if (!OutputAudioLevelFeedbacks.TryGetValue(output, out feedback)) return;

				if (feedback == null) return;

				feedback.Value = volume * 1024 - 1;
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessOutputVolumeResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessOutputMuteResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var mute = Convert.ToInt32(responses[1]);
				var output = Convert.ToInt32(responses[0].Replace("Amt", ""));

				Debug.Console(DebugExt.Verbose, this, "ProcessOutputMuteResponse: output-'{0}', mute-'{1}",
					output, mute);

				if (output == 0) return;

				BoolWithFeedback feedback;
				if (!OutputAudioMuteFeedbacks.TryGetValue(output, out feedback)) return;

				if (feedback == null) return;

				feedback.Value = (mute > 0);
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessOutputMuteResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessErrorResponse(string data)
		{
			var error = Convert.ToInt32(data);
			string errorMsg;

			switch (error)
			{
				case (1):
					{
						errorMsg = "Invalid input channel number (out of range)";
						break;
					}
				case (10):
					{
						errorMsg = "Invalid command";
						break;
					}
				case (11):
					{
						errorMsg = "Invalid preset number (out of range)";
						break;
					}
				case (12):
					{
						errorMsg = "Invalid output number (out of range)";
						break;
					}
				case (13):
					{
						errorMsg = "Invalid value (out of range)";
						break;
					}
				case (14):
					{
						errorMsg = "Invalid command for this configuration";
						break;
					}
				case (17):
					{
						errorMsg = "Timeout (caused only by direct write of global presets";
						break;
					}
				case (22):
					{
						errorMsg = "Busy";
						break;
					}
				case (24):
					{
						errorMsg = "Privileges violation (users have access to all view and read commands [other than the administrator password], and can create ties, presets, and audio mutes";
						break;
					}
				case (25):
					{
						errorMsg = "Device not present";
						break;
					}
				case (26):
					{
						errorMsg = "Maximum number of connections exceeded";
						break;
					}
				case (27):
					{
						errorMsg = "Invalid event number";
						break;
					}
				case (28):
					{
						errorMsg = "Bad filename / file not found";
						break;
					}
				default:
					{
						errorMsg = "Unknown error";
						break;
					}
			}

			Debug.Console(DebugExt.Debug, this, "Error{0:02}: {1}", error, errorMsg);
		}

		/// <summary>
		/// Appends delimiter and sends full command to device
		/// </summary>
		/// <param name="cmd"></param>
		public void SendText(string cmd)
		{
			if (string.IsNullOrEmpty(cmd)) return;

			Debug.Console(DebugExt.Verbose, this, "SendText: '{0}'", cmd);

			var fullCmd = string.Format("{0}{1}", cmd, CmdDelimiter);
			_comms.SendText(fullCmd);
		}

		/// <summary>
		/// Appends delimiter and sends full command to device
		/// </summary>
		/// <param name="cmd"></param>
		public void SendTextWihtoutDelimiter(string cmd)
		{
			if (string.IsNullOrEmpty(cmd)) return;

			Debug.Console(DebugExt.Verbose, this, "SendTextWihtoutDelimiter: '{0}'", cmd);

			_comms.SendText(cmd);
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
						cmd = string.Format("{0}*{1}{2}", input, output, AvCmd);
						break;
					}
				case eRoutingSignalType.Video:
					{
						cmd = string.Format("{0}*{1}{2}", input, output, VideoCmd);
						break;
					}
				case eRoutingSignalType.Audio:
					{
						cmd = string.Format("{0}*{1}{2}", input, output, AudioCmd);
						break;
					}
				default:
					{
						Debug.Console(DebugExt.Debug, this, "ExecuteSwitch: unhandled signalType '{0}'", signalType.ToString());
						break;
					}
			}

			if (cmd == null) return;

			Debug.Console(DebugExt.Verbose, this, "ExecuteSwitch: cmd-'{0}'", cmd);

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
			Debug.Console(DebugExt.Verbose, this, "ExecuteVirtualSwitch: input-'{0}', output-{1}', type-'{2}'",
				input, output, type.ToString());

			string routeType;
			switch (type)
			{
				case eRoutingSignalType.Video:
					{
						routeType = "Vid";
						break;
					}
				case eRoutingSignalType.Audio:
					{
						routeType = "All";
						break;
					}
				case eRoutingSignalType.AudioVideo:
					{
						routeType = "All";
						break;
					}
				default:
					{
						return;
					}
			}

			var virtualResponse = string.Format("Out{0} In{1} {2}\n", output, input, routeType);
			Debug.Console(DebugExt.Verbose, this, "ExecuteVirtualSwitch: {0}", virtualResponse);

			OnCommsGatherLineReceived(this, new GenericCommMethodReceiveTextArgs(virtualResponse));
		}

		/// <summary>
		/// Takes extron style command to make a route from the console
		/// </summary>
		/// <param name="data"></param>
		public void MakeRoute(string data)
		{
			var d = data.Trim().ToLower();
			Debug.Console(DebugExt.Verbose, this, "MakeRoute: d-'{0}'", d);

			var match = Regex.Match(d, @"(?<input>\d+)[\*:\-](?<output>\d+)(?<type>.)", RegexOptions.CultureInvariant);
			if (!match.Success)
			{
				Debug.Console(DebugExt.Verbose, this, "MakeRoute: regex match failed", d);
				return;
			}

			var input = Convert.ToUInt16(match.Groups["input"].Value);
			var output = Convert.ToUInt16(match.Groups["output"].Value);
			var typeKey = match.Groups["type"].Value;

			eRoutingSignalType type;
			if (!_typeMap.TryGetValue(typeKey, out type)) return;

			ExecuteNumericSwitch(input, output, type);
		}

		/// <summary>
		/// Prints routing ports to console for debug purposes
		/// </summary>
		public void PrintRoutingInputPorts()
		{
			var line = new string('*', 50);

			Debug.Console(DebugExt.Trace, this, line);
			foreach (var item in InputPorts)
			{
				Debug.Console(DebugExt.Trace, this, "InputPort: key-'{1}', type-'{2}', parent-'{3}'",
					item.Key, item.Type, item.ParentDevice);
			}
			Debug.Console(DebugExt.Trace, this, line);
		}

		/// <summary>
		/// Prints routing ports to console for debug purposes
		/// </summary>
		public void PrintRoutingOutputPorts()
		{
			var line = new string('*', 50);

			Debug.Console(DebugExt.Trace, this, line);
			foreach (var item in OutputPorts)
			{
				Debug.Console(DebugExt.Trace, this, "OutputPort: key-'{1}', type-'{2}', parent-'{3}'",
					item.Key, item.Type, item.ParentDevice);
			}
			Debug.Console(DebugExt.Trace, this, line);
		}

		/// <summary>
		/// Polls the devices status
		/// </summary>
		public void Poll()
		{
			// query controller firmware
			//SendText("Q");
			SendTextWihtoutDelimiter("Q");
			// query controller firmware (verbose)
			//SendText("0Q");
			//SendTextWihtoutDelimiter("0Q");

			SendTextWihtoutDelimiter("S4S7S8");
		}

		/// <summary>
		/// Polls the switcher for current route, volume and mute states
		/// </summary>
		/// <param name="o"></param>
		public void PollRoutes(object o)
		{
			foreach (var item in _outputs)
			{
				var ioNumber = item.IoNumber;

				// poll video routes
				var cmd = string.Format("{0}{1}", ioNumber, VideoCmd);
				SendText(cmd);

				// poll audio routes
				cmd = string.Format("{0}{1}", ioNumber, AudioCmd);
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
		}

		/// <summary>
		/// Sets the verbose mode
		/// </summary>
		/// <param name="data"></param>
		public void SetVerboseMode(string data)
		{
			var d = data.Trim().ToLower();
			Debug.Console(DebugExt.Verbose, this, "SetVerboseMode: d-'{0}'", d);

			var v = Convert.ToUInt32(d);
			Debug.Console(DebugExt.Verbose, this, "SetVerboseMode: v-'{0}'", v);
			if (v > 3) return;

			var cmd = string.Format("{0:X2}{1}CV", Escape, v);
			SendText(cmd);
		}

		/// <summary>
		/// Quick tie commands aka salvos
		/// </summary>
		/// <param name="cmds"></param>
		public void QuickTie(params string[] cmds)
		{
			var ties = cmds.Aggregate(string.Empty, (current, item) => current + item);
			var cmd = string.Format("{0}+Q{1}", Escape, ties);
			SendText(cmd);
		}

		/// <summary>
		/// recalls the preset by index
		/// </summary>
		public void RecallPreset(int preset)
		{
			var cmd = string.Format("{0}R{1}PRST", Escape, preset);
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
				Debug.Console(DebugExt.Verbose, this, "OutputMuteToggle: output '{0}' not found in OutputAudioMuteFeedbacks", output);
				return;
			}

			var state = feedback.Value ? 0 : 3;
			var cmd = string.Format("{0}*{1}Z", output, state);
			SendText(cmd);
		}
	}
}