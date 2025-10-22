// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Plugin.Errors;
using PepperDash.Essentials.Plugin.IOs;
using static Crestron.SimplSharpPro.DM.Audio;

namespace PepperDash.Essentials.Plugin.ExtronAvMatrix
{
    /// <summary>
    /// Plugin device template for third party devices that use IBasicCommunication
    /// </summary>
    public class ExtronAvMatrixController : EssentialsBridgeableDevice, IMatrixRouting, IRoutingWithFeedback, ICommunicationMonitor, IDeviceInfoProvider
    {
        private const string commsDelimiter = "\r";
        private const string gatherDelimiter = "\n";
        private const string RgbSwitch = "&";
        private const string VideoSwitch = "%";
        private const string AudioSwitch = "$";
        private const string AllSwitch = "!";

        private uint inputCount = 8;
        private uint outputCount = 8;

        /// <summary>
        /// It is often desirable to store the config
        /// </summary>
        private readonly ExtronAvMatrixConfig config;

        /// <summary>
        /// Provides a queue and dedicated worker thread for processing feedback messages from a device.
        /// </summary>
        private readonly GenericQueue receiveQueue;

        private readonly IBasicCommunication comms;
        private readonly CommunicationGather commsGather;

        /// <summary>
        /// Communication monitor for the device
        /// </summary>
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        /// <summary>
        /// Connects/disconnects the comms of the plugin device
        /// </summary>
        public bool Connect
        {
            get { return comms.IsConnected; }
            set
            {
                if (value)
                {
                    comms.Connect();
                    CommunicationMonitor.Start();
                }
                else
                {
                    comms.Disconnect();
                    CommunicationMonitor.Stop();
                }
            }
        }

        /// <summary>
        /// Reports connect feedback through the bridge
        /// </summary>
        public BoolFeedback ConnectFeedback { get; private set; }

        /// <summary>
        /// Reports online feedback through the bridge
        /// </summary>
        public BoolFeedback OnlineFeedback { get; private set; }

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback StatusFeedback { get; private set; }

        public Dictionary<uint, IntFeedback> VideoOutputFeedbacks { get; private set; }
        public Dictionary<uint, IntFeedback> AudioOutputFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputVideoNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputAudioNameFeedbacks { get; private set; }

        public Dictionary<uint, StringFeedback> OutputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputVideoNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputAudioNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }

        public Dictionary<string, IRoutingInputSlot> InputSlots { get; private set; }

        public Dictionary<string, IRoutingOutputSlot> OutputSlots { get; private set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public Dictionary<uint, string> InputNames { get; private set; }
        public Dictionary<uint, string> OutputNames { get; private set; }

        public event RouteChangedEventHandler RouteChanged;
        public event DeviceInfoChangeHandler DeviceInfoChanged;

        public List<RouteSwitchDescriptor> CurrentRoutes { get; private set; }

        public DeviceInfo DeviceInfo { get; private set; }

        /// <summary>
        /// Plugin device constructor for devices that need IBasicCommunication
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <param name="comms"></param>
        public ExtronAvMatrixController(string key, string name, ExtronAvMatrixConfig config, IBasicCommunication comms, string typeName)
      : base(key, name)
        {
            this.LogInformation("Constructing new {0} instance", name);

            this.config = config;

            receiveQueue = new GenericQueue(key + "-rxqueue");  // If you need to set the thread priority, use one of the available overloaded constructors.

            InputNames = new Dictionary<uint, string>();
            OutputNames = new Dictionary<uint, string>();

            InputSlots = new Dictionary<string, IRoutingInputSlot>();
            OutputSlots = new Dictionary<string, IRoutingOutputSlot>();

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            ConnectFeedback = new BoolFeedback("connect", () => Connect);
            OnlineFeedback = new BoolFeedback("online", () => CommunicationMonitor.IsOnline);
            StatusFeedback = new IntFeedback("status", () => (int)CommunicationMonitor.Status);

            InputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            InputVideoNameFeedbacks = new Dictionary<uint, StringFeedback>();
            InputAudioNameFeedbacks = new Dictionary<uint, StringFeedback>();

            OutputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputVideoNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputAudioNameFeedbacks = new Dictionary<uint, StringFeedback>();

            VideoInputSyncFeedbacks = new Dictionary<uint, BoolFeedback>();

            VideoOutputFeedbacks = new Dictionary<uint, IntFeedback>();
            AudioOutputFeedbacks = new Dictionary<uint, IntFeedback>();

            OutputVideoRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputAudioRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();

            this.comms = comms;
            CommunicationMonitor = new GenericCommunicationMonitor(
              this,
              this.comms,
              this.config.PollTimeMs == 0 ? 60000 : this.config.PollTimeMs,
              180000,
              300000,
              Poll);

            commsGather = new CommunicationGather(this.comms, gatherDelimiter);
            commsGather.LineReceived += Handle_LineRecieved;

            CurrentRoutes = new List<RouteSwitchDescriptor>();

            SetupSlots();
        }

        public override void Initialize()
        {
            base.Initialize();

            var socket = this.comms as ISocketStatus;
            if (socket != null)
            {
                // device comms is IP **ELSE** device comms is RS232
                socket.ConnectionChange += Socket_ConnectionChange;
                Connect = true;

                return;
            }

            CommunicationMonitor.Start();

            PollRoutes();
        }

        private void SetupSlots()
        {
            InputNames = config.InputNames;
            OutputNames = config.OutputNames;

            InputNames.Add(0, config.NoRouteText);

            inputCount = (uint)(InputNames.Count >= 0 ? InputNames.Count : 8);
            outputCount = (uint)(OutputNames.Count > 0 ? OutputNames.Count : 8);

            SetupInputSlot(0);

            for (uint i = 1; i <= inputCount; i++)
            {
                SetupInputSlot(i);
            }

            for (uint i = 1; i <= outputCount; i++)
            {
                SetupOutputSlot(i);
            }

            foreach (var item in InputSlots)
            {
                this.LogInformation($"SetupSlots: InputSlots[{item.Key}] Key={item.Value.Key}, Name={item.Value.Name}, SlotNumber={item.Value.SlotNumber}");
            }

            foreach (var item in OutputSlots)
            {
                this.LogInformation($"SetupSlots: OutputSlots[{item.Key}] Key={item.Value.Key}, Name={item.Value.Name}, SlotNumber={item.Value.SlotNumber}");
            }

            foreach (var item in InputPorts)
            {
                this.LogInformation($"SetupSlots: InputPorts Key={item.Key}, Port={item.Port}, Type={item.Type}, Selector={item.Selector}, ConnectionType={item.ConnectionType}, Parent={item.ParentDevice}");
            }

            foreach (var item in OutputPorts)
            {
                this.LogInformation($"SetupSlots: OutputPorts Key={item.Key}, Port={item.Port}, Type={item.Type}, Selector={item.Selector}, ConnectionType={item.ConnectionType}, Parent={item.ParentDevice}");
            }
        }

        private void SetupInputSlot(uint slotNum)
        {
            var key = $"input{slotNum}";
            var name = InputNames.ContainsKey(slotNum) ? InputNames[slotNum] : $"Input {slotNum}";
            var slot = new InputSlot(key, name, (int)slotNum);

            if (slotNum == 0)
            {
                // set static values
                slot.VideoSyncDetected = true;
            }

            InputSlots.Add(key, slot);

            InputPorts.Add(
              new RoutingInputPort(
                key,
                eRoutingSignalType.AudioVideo,
                eRoutingPortConnectionType.Hdmi,
                slotNum,
                this,
                true)
              {
                  FeedbackMatchObject = key,
              });

            InputNameFeedbacks[slotNum] = new StringFeedback($"inputNameFeedback-{slot.Key}", () => slot.Name);
            InputVideoNameFeedbacks[slotNum] = new StringFeedback($"inputVideoNameFeedback-{slot.Key}", () => slot.Name);
            InputAudioNameFeedbacks[slotNum] = new StringFeedback($"inputAudioNameFeedback-{slot.Key}", () => slot.Name);

            VideoInputSyncFeedbacks[slotNum] = new BoolFeedback($"videoInputSyncFeedback-{slot.Key}", () => slot.VideoSyncDetected);
        }

        private void SetupOutputSlot(uint slotNum)
        {
            if (slotNum == 0) return;

            var key = $"output{slotNum}";
            var name = OutputNames.ContainsKey(slotNum) ? OutputNames[slotNum] : $"Output {slotNum}";
            var slot = new OutputSlot(key, name, (int)slotNum);

            OutputSlots.Add(key, slot);

            OutputPorts.Add(
              new RoutingOutputPort(
                key,
                eRoutingSignalType.AudioVideo,
                eRoutingPortConnectionType.Hdmi,
                slotNum,
                this,
                true));

            OutputNameFeedbacks[slotNum] = new StringFeedback($"outputNameFeedback-{slot.Key}", () => slot.Name);
            OutputVideoNameFeedbacks[slotNum] = new StringFeedback($"outputVideoNameFeedback-{slot.Key}", () => slot.Name);
            OutputAudioNameFeedbacks[slotNum] = new StringFeedback($"outputAudioNameFeedback-{slot.Key}", () => slot.Name);

            VideoOutputFeedbacks[slotNum] = new IntFeedback($"videoOutputFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.Video] is InputSlot inputSlot ? inputSlot.SlotNumber : 0);
            AudioOutputFeedbacks[slotNum] = new IntFeedback($"audioOutputFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.Audio] is InputSlot inputSlot ? inputSlot.SlotNumber : 0);

            OutputVideoRouteNameFeedbacks[slotNum] = new StringFeedback($"outputVideoRouteNameFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.Video]?.Name ?? config.NoRouteText);
            OutputAudioRouteNameFeedbacks[slotNum] = new StringFeedback($"outputAudioRouteNameFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.Audio]?.Name ?? config.NoRouteText);
        }


        private void Socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            ConnectFeedback?.FireUpdate();

            StatusFeedback?.FireUpdate();

            if (!args.Client.IsConnected) return;

            // Set verbose mode
            SendText("\x1B3CV");

            PollRoutes();
        }

        private void Handle_LineRecieved(object sender, GenericCommMethodReceiveTextArgs args)
        {
            // Enqueues the message to be processed in a dedicated thread, but the specified method
            receiveQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessFeedbackMessage));
        }


        /// <summary>
        /// This method should perform any necessary parsing of feedback messages from the device
        /// </summary>
        /// <param name="message"></param>
        private void ProcessFeedbackMessage(string message)
        {
            // Check for error responses first
            if (ExtronSisErrors.IsErrorResponse(message))
            {
                // Parse an error response from device
                var errorCode = ExtronSisErrors.ParseErrorResponse(message);
                var errorMessage = ExtronSisErrors.FormatErrorMessage(errorCode);
                this.LogError($"ProcessFeedbackMessage: {errorCode} - {errorMessage}");
                return;
            }


            // Process Extron SIS switch responses using unified regex pattern
            // Pattern: "Out[XX] In[YY] [All|Vid|Aud]"
            var switchResponseRegex = new System.Text.RegularExpressions.Regex(@"Out(0?\d|[1-9]\d)\s+In(0?\d|[1-9]\d)\s+(All|Vid|Aud)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var switchMatch = switchResponseRegex.Match(message);

            if (switchMatch.Success)
            {
                var outputNumber = uint.Parse(switchMatch.Groups[1].Value);
                var inputNumber = uint.Parse(switchMatch.Groups[2].Value);
                var signalType = switchMatch.Groups[3].Value;

                this.LogDebug($"ProcessFeedbackMessage: Switch response {signalType} Input-{inputNumber} to Output-{outputNumber}");

                var outputSlot = OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == outputNumber).Value;
                var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value;

                if (outputSlot != null && inputSlot != null)
                {
                    this.LogDebug($"ProcessFeedbackMessage: {signalType} route feedback {inputSlot.SlotNumber}-{inputSlot.Name} to {outputSlot.SlotNumber}-{outputSlot.Name}");

                    // Handle different signal types
                    switch (signalType.ToLower())
                    {
                        case "all":
                            //(outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.AudioVideo, inputSlot);
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Video, inputSlot);
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Audio, inputSlot);
                            break;
                        case "vid":
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Video, inputSlot);
                            break;
                        case "aud":
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Audio, inputSlot);
                            break;
                    }

                    UpdateCurrentRoutes(inputNumber, outputNumber);

                }
                else if (outputSlot == null)
                {
                    this.LogWarning("ProcessFeedbackMessage: Could not find outputNum slot {0}", outputNumber);
                }
                else if (inputSlot == null)
                {
                    this.LogWarning("ProcessFeedbackMessage: Could not find inputNum slot {0}", inputNumber);
                }

                return;
            }

            // Handle sink status bitmap response (e.g., "Frq00 01100110")
            if (message.StartsWith("Frq"))
            {
                ParseSyncStatus(message);
                return;
            }

            if (message.StartsWith("Ver"))
            {
                // Process firmware version response (verbose)
                // Pattern: "Ver00*1.01-2.05.0000-b004*(2.07LX-0DXP HD PLUS     -Thu, 18 Jan 2024 06:02 UTC)-?.??"
                ParseFirmwareVersion(message);
                return;
            }

            // Log unhandled messages for debugging
            this.LogDebug("ProcessFeedbackMessage: Unhandled message '{0}'", message);
        }

        /// <summary>
        /// Parses Extron FRQ response
        /// </summary>
        /// <param name="message"></param>
        private void ParseSyncStatus(string message)
        {
            var syncRegex = new System.Text.RegularExpressions.Regex(@"Frq\d+\s+([01]+)");
            var syncMatch = syncRegex.Match(message);
            if (syncMatch.Success)
            {
                var syncBitmap = syncMatch.Groups[1].Value;
                this.LogDebug("ParseSyncStatus: Sync status bitmap = {0}", syncBitmap);

                // Process each bit in the bitmap
                for (int i = 0; i < syncBitmap.Length && i < inputCount; i++)
                {
                    var inputNumber = i + 1; // Inputs are 1-based
                    var hasSync = syncBitmap[i] == '1';

                    var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value as InputSlot;
                    if (inputSlot == null)
                    {
                        this.LogError("ParseSyncStatus: Could not find inputslot.SlotNumber {0} for sync status update", inputNumber);
                        return;
                    }

                    inputSlot.VideoSyncDetected = hasSync;

                    this.LogDebug("ParseSyncStatus: Input {0} sync status is {1}", inputNumber, hasSync);
                }
            }
        }

        /// <summary>
        /// Parses Extron firmware version response (verbose format)
        /// </summary>
        /// <param name="message">The message to parse</param>
        /// <returns>True if the message was a firmware version response and was processed</returns>
        private void ParseFirmwareVersion(string message)
        {
            // New firmware version pattern for "Ver00*..." format
            // Example: "Ver00*1.01-2.05.0000-b004*(2.07LX-0DXP HD PLUS     -Thu, 18 Jan 2024 06:02 UTC)-?.??"
            var firmwareRegex = new System.Text.RegularExpressions.Regex(
                @"^Ver\d+\*([^-]+)-([^*]+)\*\(([^)]+)\)-(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var match = firmwareRegex.Match(message.Trim());

            if (match.Success)
            {
                var mainVersion = match.Groups[1].Value;          // e.g., "1.01"
                var buildVersion = match.Groups[2].Value;         // e.g., "2.05.0000-b004"
                var deviceDetails = match.Groups[3].Value;        // e.g., "2.07LX-0DXP HD PLUS     -Thu, 18 Jan 2024 06:02 UTC"
                var additionalInfo = match.Groups[4].Value;       // e.g., "?.??"

                this.LogDebug("ParseFirmwareVersion: Main = {0}, Build = {1}, Additional = {2}", mainVersion, buildVersion, additionalInfo);
                this.LogDebug("ParseFirmwareVersion: Device details = {0}", deviceDetails);

                // Extract model number from device details
                var modelRegex = new System.Text.RegularExpressions.Regex(@"(\d+\.\d+)([A-Z]+-[A-Z0-9\s]+)");
                var modelMatch = modelRegex.Match(deviceDetails);
                var modelNumber = "";

                if (modelMatch.Success)
                {
                    modelNumber = modelMatch.Groups[2].Value.Trim();
                }

                // Create comprehensive firmware version string
                var fullFirmwareVersion = $"Main: {mainVersion}, Build: {buildVersion}";
                if (!string.IsNullOrEmpty(modelNumber))
                {
                    fullFirmwareVersion += $" ({modelNumber})";
                }

                // Update DeviceInfo with parsed firmware information
                UpdateDeviceInfoWithFirmware(fullFirmwareVersion, modelNumber);

                this.LogInformation("ParseFirmwareVersion: Device firmware version to {0}", fullFirmwareVersion);
            }
        }

        /// <summary>
        /// Sends text to the device plugin comms
        /// </summary>
        /// <remarks>
        /// Can be used to test commands with the device plugin using the DEVPROPS and DEVJSON console commands
        /// </remarks>
        /// <param name="text">Command to be sent</param>		
        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            comms.SendText(string.Format("{0}{1}", text, commsDelimiter));
        }

        /// <summary>
        /// Polls the device
        /// </summary>
        /// <remarks>
        /// Poll method is used by the communication monitor.  Update the poll method as needed for the plugin being developed
        /// </remarks>
        public void Poll()
        {
            // information request
            SendText("I");

            Thread.Sleep(500);

            // part number request
            SendText("N");

            // firmware version request (verbose)
            SendText("Q");

            Thread.Sleep(500);

            PollSync();
        }

        public void PollRoutes()
        {
            // query each outputNum for video and audio
            for (int i = 1; i < outputCount; i++)
            {
                SendText($"{i}{VideoSwitch}");
                Thread.Sleep(500);
                SendText($"{i}{AudioSwitch}");
                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Poll device for inputNum sync detection status
        /// </summary>
        /// <remarks>
        /// Example return: "Frq00 01100110\r" (8 inputs, inputs 2, 3, 6, and 7 have sync)
        /// </remarks>
        public void PollSync()
        {
            SendText("0LS");
        }

        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Links the plugin device to the EISC bridge
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new ExtronAvMatrixJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            bridge?.AddJoinMap(Key, joinMap);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            this.LogDebug("Linking to Trilist {id}", trilist.ID.ToString("X"));
            this.LogInformation("Linking to Bridge Type {type}", GetType().Name);

            // links to bridge
            trilist.SetString(joinMap.Name.JoinNumber, Name);

            trilist.SetBoolSigAction(joinMap.Connect.JoinNumber, sig => Connect = sig);
            ConnectFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Connect.JoinNumber]);

            StatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);
            OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            // Clear inputNum name feedback
            InputNameFeedbacks[0].LinkInputSig(trilist.StringInput[joinMap.NoRouteName.JoinNumber]);

            // inputNum name feedbacks
            for (uint i = 1; i <= inputCount; i++)
            {
                var input = i;

                LinkInputsToApi(trilist, joinMap, input);
            }

            // outputNum name feedbacks and routing control/feedback
            for (uint i = 1; i <= outputCount; i++)
            {
                var output = i;

                LinkOutputsToApi(trilist, joinMap, output);
            }

            UpdateFeedbacks();

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;

                trilist.SetString(joinMap.Name.JoinNumber, Name);

                UpdateFeedbacks();
            };
        }

        private void LinkInputsToApi(BasicTriList trilist, ExtronAvMatrixJoinMap joinMap, uint input)
        {
            var inputJoinOffset = input - 1;

            this.LogInformation($"LinkInputsToApi: inputNum={input}, inputJoinOffset={inputJoinOffset}");

            InputNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + inputJoinOffset]);
            InputVideoNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputVideoNames.JoinNumber + inputJoinOffset]);
            InputAudioNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputAudioNames.JoinNumber + inputJoinOffset]);
            VideoInputSyncFeedbacks[input].LinkInputSig(trilist.BooleanInput[joinMap.VideoSyncStatus.JoinNumber + inputJoinOffset]);
        }

        private void LinkOutputsToApi(BasicTriList trilist, ExtronAvMatrixJoinMap joinMap, uint output)
        {
            if (output == 0)
                return;

            var outputJoinOffset = output - 1;

            this.LogInformation($"LinkOutputsToApi: outputNum={output}, outputJoinOffset={outputJoinOffset}");


            // Routing Control
            trilist.SetUShortSigAction(joinMap.OutputVideo.JoinNumber + outputJoinOffset,
                o => ExecuteSwitch(o, (ushort)output, eRoutingSignalType.Video));

            trilist.SetUShortSigAction(joinMap.OutputAudio.JoinNumber + outputJoinOffset,
                o => ExecuteSwitch(o, (ushort)output, eRoutingSignalType.Audio));

            // Routing Feedbacks
            OutputNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + outputJoinOffset]);
            OutputVideoNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputVideoNames.JoinNumber + outputJoinOffset]);
            OutputAudioNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputAudioNames.JoinNumber + outputJoinOffset]);

            VideoOutputFeedbacks[output].LinkInputSig(trilist.UShortInput[joinMap.OutputVideo.JoinNumber + outputJoinOffset]);
            AudioOutputFeedbacks[output].LinkInputSig(trilist.UShortInput[joinMap.OutputAudio.JoinNumber + outputJoinOffset]);

            OutputVideoRouteNameFeedbacks[output].LinkInputSig(
                trilist.StringInput[joinMap.OutputCurrentVideoInputNames.JoinNumber + outputJoinOffset]);
            OutputAudioRouteNameFeedbacks[output].LinkInputSig(
                trilist.StringInput[joinMap.OutputCurrentAudioInputNames.JoinNumber + outputJoinOffset]);
        }

        #endregion

        private void UpdateFeedbacks()
        {
            // TODO [ ] Update as needed for the plugin being developed
            ConnectFeedback?.FireUpdate();
            OnlineFeedback?.FireUpdate();
            StatusFeedback?.FireUpdate();

            foreach (var item in InputNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in InputVideoNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in InputAudioNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in VideoInputSyncFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputVideoNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputAudioNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in VideoOutputFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in AudioOutputFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputVideoRouteNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputAudioRouteNameFeedbacks)
                item.Value.FireUpdate();
        }

        /// <summary>
        /// Routes an inputNum to an outputNum for the specified signal type(s)
        /// </summary>
        /// <param name="inputSlotKey"></param>
        /// <param name="outputSlotKey"></param>
        /// <param name="type"></param>
        public void Route(string inputSlotKey, string outputSlotKey, eRoutingSignalType type)
        {
            try
            {
                var inputSlot = InputSlots.TryGetValue(inputSlotKey, out var inSlot) ? inSlot as InputSlot : null;
                var outputSlot = OutputSlots.TryGetValue(outputSlotKey, out var outSlot) ? outSlot as OutputSlot : null;

                if (inputSlot == null)
                {
                    this.LogError($"Route: failed to find inputSlotKey `{inputSlotKey}`");
                    return;
                }

                if (outputSlot == null)
                {
                    this.LogError($"Route: failed to find outputSlotKey `{outputSlotKey}`");
                    return;
                }

                if (type.HasFlag(eRoutingSignalType.AudioVideo))
                {
                    SetAvRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                    return;
                }

                if (type.HasFlag(eRoutingSignalType.Video))
                {
                    SetVideoRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                }

                if (type.HasFlag(eRoutingSignalType.Audio))
                {
                    SetAudioRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                }
            }
            catch (Exception ex)
            {
                this.LogError("Route: {inputNum} to {outputNum} exception {message}", inputSlotKey, outputSlotKey, ex.Message);
                this.LogDebug(ex, "Route: Exception StackTrace");
                return;
            }
        }

        /// <summary>
        /// Executes a switch from an inputNum to an outputNum for the specified signal type(s)
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        /// <param name="signalType"></param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            try
            {
                this.LogVerbose($"ExecuteSwitch: Making {signalType.ToString().ToLower()} route from inputNum {inputSelector} to outputNum {outputSelector}");

                var inputNum = Convert.ToUInt16(inputSelector);
                var outputNum = Convert.ToUInt16(outputSelector);

                // route a/v
                if (signalType.HasFlag(eRoutingSignalType.AudioVideo))
                {
                    SetAvRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                    return;
                }
                // route video
                if (signalType.HasFlag(eRoutingSignalType.Video))
                {
                    SetVideoRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                }
                // route audio
                if (signalType.HasFlag(eRoutingSignalType.Audio))
                {
                    SetAudioRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                }
            }
            catch (Exception ex)
            {
                this.LogError("ExecuteSwitch: {inputNum} to {outputNum} exception {message}", inputSelector, outputSelector, ex.Message);
                this.LogDebug(ex, "ExecuteSwitch: Exception StackTrace");
                return;
            }

        }

        /// <summary>
        /// Updates the current routes based on the inputNum and outputNum numbers.
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        private void UpdateCurrentRoutes(uint inputSelector, uint outputSelector)
        {
            RouteSwitchDescriptor descriptor;

            descriptor = GetRouteDescriptorByOutputPort(outputSelector);
            this.LogDebug("UpdateCurrentRoutes: Found existing descriptor: {0}", descriptor != null ? "Yes" : "No");

            var inputPort = GetRoutingInputPortForSelector(inputSelector);

            var outputPort = GetRoutingOutputPortForSelector(outputSelector);

            this.LogDebug("UpdateCurrentRoutes: Updating route for inputNum {inputNum} to outputNum {outputNum}", this, outputSelector, inputSelector);

            if (outputPort is null)
            {
                this.LogDebug("UpdateCurrentRoutes: Unable to find port for {outputNum}", this, outputSelector);
                return;
            }

            if (descriptor is null && outputPort is not null)
            {
                descriptor = new(outputPort, inputPort);

                CurrentRoutes.Add(descriptor);
            }
            else
            {
                descriptor.InputPort = inputPort;
            }

            RouteChanged?.Invoke(this, descriptor);
        }

        /// <summary>
        /// Gets the route descriptor for the specified outputNum port number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RouteSwitchDescriptor GetRouteDescriptorByOutputPort(uint selector)
        {
            this.LogDebug("GetRouteDescriptorByOutputPort: Looking for route descriptor with outputNum port selector {0}", selector);
            return CurrentRoutes.FirstOrDefault(rd =>
            {
                this.LogDebug("GetRouteDescriptorByOutputPort: Checking descriptor with outputNum port selector {0}", rd.OutputPort.Selector);
                if (rd.OutputPort.Selector is not uint opSelector)
                {
                    this.LogDebug("GetRouteDescriptorByOutputPort: Output port selector is not a uint");
                    return false;
                }

                this.LogDebug("GetRouteDescriptorByOutputPort: Comparing {0} to {1}", opSelector, selector);
                return opSelector == selector;
            });
        }

        /// <summary>
        /// Gets the routing inputNum port for the specified inputNum number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RoutingInputPort GetRoutingInputPortForSelector(uint selector)
        {
            this.LogDebug("GetRoutingInputPortForSelector: Looking for inputNum port with selector {0}", selector);

            return InputPorts.FirstOrDefault(ip =>
            {
                this.LogDebug("GetRoutingInputPortForSelector: Checking inputNum port with selector {0}", ip.Selector);
                if (ip.Selector is not uint ipSelector)
                {
                    this.LogDebug("GetRoutingInputPortForSelector: Input port selector is not a uint");
                    return false;
                }

                this.LogDebug("GetRoutingInputPortForSelector: Comparing {0} to {1}", ipSelector, selector);
                return ipSelector == selector;
            });
        }


        /// <summary>
        /// Gets the routing outputNum port for the specified outputNum number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RoutingOutputPort GetRoutingOutputPortForSelector(uint selector)
        {

            return OutputPorts.FirstOrDefault(op =>
            {
                if (op.Selector is not uint opSelector)
                {
                    return false;
                }

                return opSelector == selector;
            });
        }

        private void SetVideoRoute(int input, int output)
        {
            SendText($"{input}*{output}{VideoSwitch}");
        }

        private void SetAudioRoute(int input, int output)
        {
            SendText($"{input}*{output}{AudioSwitch}");
        }

        private void SetAvRoute(int input, int output)
        {
            SendText($"{input}*{output}{AllSwitch}");
        }

        public void UpdateDeviceInfo()
        {
            var socket = comms as GenericTcpIpClient;

            // Initialize DeviceInfo if it doesn't exist or preserve existing firmware version
            var existingFirmware = DeviceInfo?.FirmwareVersion ?? "";
            var existingHostName = DeviceInfo?.HostName ?? "";

            DeviceInfo = new DeviceInfo
            {
                FirmwareVersion = existingFirmware,
                HostName = existingHostName,
                IpAddress = socket?.Hostname ?? "",
                MacAddress = "",
                SerialNumber = ""
            };

            var handler = DeviceInfoChanged;
            if (handler == null) return;

            handler(this, new DeviceInfoEventArgs { DeviceInfo = DeviceInfo });
        }

        /// <summary>
        /// Updates the DeviceInfo with firmware version information
        /// </summary>
        /// <param name="firmwareVersion">The parsed firmware version string</param>
        /// <param name="modelNumber">The extracted model number (if available)</param>
        private void UpdateDeviceInfoWithFirmware(string firmwareVersion, string modelNumber)
        {
            var socket = comms as GenericTcpIpClient;

            // Create or update DeviceInfo
            DeviceInfo = new DeviceInfo
            {
                FirmwareVersion = firmwareVersion,
                HostName = modelNumber,
                IpAddress = socket?.Hostname ?? "",
                MacAddress = "",
                SerialNumber = ""
            };

            // Fire the DeviceInfoChanged event
            var handler = DeviceInfoChanged;
            if (handler != null)
            {
                handler(this, new DeviceInfoEventArgs { DeviceInfo = DeviceInfo });
            }

            this.LogDebug("UpdateDeviceInfoWithFirmware: Update firmwarVersion to {0}", firmwareVersion);
        }
    }
}