// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Plugin.IOs;
using PepperDash.Essentials.Plugin.Errors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PepperDash.Essentials.Plugin.ExtronAvMatrix
{
    /// <summary>
    /// Plugin device template for third party devices that use IBasicCommunication
    /// </summary>
    public class ExtronAvMatrixController : EssentialsBridgeableDevice, IMatrixRouting, IRoutingWithFeedback, ICommunicationMonitor, IDeviceInfoProvider
    {
        private const string commsDelimiter = "\r";
        private const string VideoSwitch = "%";
        private const string AudioSwitch = "$";
        private const string AllSwitch = "!";

        private const string VideoResponse = "Vid";
        private const string AudioResponse = "Aud";
        private const string AllResponse = "All";

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

        public Dictionary<string, IRoutingInputSlot> InputSlots { get; private set; }

        public Dictionary<string, IRoutingOutputSlot> OutputSlots { get; private set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public Dictionary<int, string> InputNames { get; private set; } = new Dictionary<int, string>();
        public Dictionary<int, string> OutputNames { get; private set; } = new Dictionary<int, string>();

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

            ConnectFeedback = new BoolFeedback("connect", () => Connect);
            OnlineFeedback = new BoolFeedback("online", () => CommunicationMonitor.IsOnline);
            StatusFeedback = new IntFeedback("status", () => (int)CommunicationMonitor.Status);

            this.comms = comms;
            CommunicationMonitor = new GenericCommunicationMonitor(
              this,
              this.comms,
              this.config.PollTimeMs == 0 ? 60000 : this.config.PollTimeMs,
              180000,
              300000,
              Poll);

            #region Communication data event handlers.  Comment out any that don't apply to the API type

            // Only one of the below handlers should be necessary.  

            commsGather = new CommunicationGather(this.comms, commsDelimiter);
            commsGather.LineReceived += Handle_LineRecieved;

            #endregion

            InputSlots = new Dictionary<string, IRoutingInputSlot>();
            OutputSlots = new Dictionary<string, IRoutingOutputSlot>();

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            InputNames = this.config.InputNames;
            OutputNames = this.config.OutputNames;

            inputCount = (uint)(InputNames.Count > 0 ? InputNames.Count : 8);
            outputCount = (uint)(OutputNames.Count > 0 ? OutputNames.Count : 8);

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
            }
        }

        private string GetInputPortSelector(int slotNum)
        {
            return $"input{slotNum}";
        }

        private string GetOutputPortSelector(int slotNum)
        {
            return $"output{slotNum}";
        }


        private string GetAudioInputPortSelector(int slotNum)
        {
            return $"audio-in{slotNum}";
        }
        
        private string GetAudioOutputPortSelector(int slotNum)
        {
            return $"audio-out{slotNum}";
        }

        private void SetupSlots()
        {
            for (var i = 1; i <= inputCount; i++)
            {
                SetupInputSlots(i);
            }

            for (var i = 1; i <= outputCount; i++)
            {
                SetupOutputSlots(i);
            }
        }

        private void SetupInputSlots(int slotNum)
        {
            // Setup input slot
            var inputName = InputNames.ContainsKey(slotNum) ? InputNames[slotNum] : $"Input {slotNum}";
            var inputSlot = new InputSlot($"input{slotNum}", $"{inputName}", slotNum);
            InputSlots.Add(inputSlot.Key, inputSlot);
            var inputKey = GetInputPortSelector(slotNum);
            InputPorts.Add(
              new RoutingInputPort(
                inputKey,
                eRoutingSignalType.AudioVideo,
                eRoutingPortConnectionType.Hdmi,
                inputKey,
                this)
              {
                  FeedbackMatchObject = inputKey,
              });
        }

        private void SetupOutputSlots(int slotNum)
        {
            // Setup output slot
            var outputName = OutputNames.ContainsKey(slotNum) ? OutputNames[slotNum] : $"Output {slotNum}";
            var outputSlot = new OutputSlot($"output{slotNum}", $"{outputName}", slotNum);
            OutputSlots.Add(outputSlot.Key, outputSlot);
            // Setup video output port
            var outputKey = GetOutputPortSelector(slotNum);
            OutputPorts.Add(
              new RoutingOutputPort(
                outputKey,
                eRoutingSignalType.AudioVideo,
                eRoutingPortConnectionType.Hdmi,
                outputKey,
                this));
        }


        private void Socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            ConnectFeedback?.FireUpdate();

            StatusFeedback?.FireUpdate();

            if (!args.Client.IsConnected) return;
            
            // Set verbose mode
            SendText("\x1B3CV");
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
                this.LogError("Extron device error: {0}", errorMessage);
                return;
            }

            // Process firmware version response (verbose)
            // Pattern: "1.23-1.00(1.81LX-DTPCP108 -Fri, 31 Jul 2015 00:00:00 UTC)-1.00*(2.03LX-DTPCP108 -Fri, 30 Nov 2018 16:39:21 UTC)"
            if (ParseFirmwareVersion(message))
            {
                return;
            }

            // Process Extron SIS switch responses using unified regex pattern
            // Pattern: "Out[index] In[index] [All|Video|Audio]"
            //var switchResponseRegex = new System.Text.RegularExpressions.Regex(@"Out(\d+)\s+In(\d+)\s+(All|Vid|Aud)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var switchResponseRegex = new System.Text.RegularExpressions.Regex(@"Out(0?\d|[1-9]\d)\s+In(0?\d|[1-9]\d)\s+(All|Vid|Aud)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var switchMatch = switchResponseRegex.Match(message);

            if (switchMatch.Success)
            {
                var outputNumber = int.Parse(switchMatch.Groups[1].Value);
                var inputNumber = int.Parse(switchMatch.Groups[2].Value);
                var signalType = switchMatch.Groups[3].Value;

                this.LogDebug("Switch response detected: Input {0} to Output {1}, Signal Type: {2}", inputNumber, outputNumber, signalType);

                var outputSlot = OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == outputNumber).Value;
                var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value;

                if (outputSlot != null && inputSlot != null)
                {
                    this.LogDebug("Route detected: InputSlot.Name {0} to OutputSlot.Name {1}, Type: {2}", inputSlot.Name, outputSlot.Name, signalType);

                    // Handle different signal types
                    switch (signalType.ToLower())
                    {
                        case "all":
                            //(outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.AudioVideo, inputSlot);
                            //UpdateCurrentRoutes(GetInputPortSelector(inputNumber), GetOutputPortSelector(outputNumber));
                            // video
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Video, inputSlot);
                            UpdateCurrentRoutes(GetInputPortSelector(inputNumber), GetOutputPortSelector(outputNumber));
                            // audio
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Audio, inputSlot);
                            UpdateCurrentRoutes(GetInputPortSelector(inputNumber), GetOutputPortSelector(outputNumber));
                            break;
                        case "vid":
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Video, inputSlot);
                            UpdateCurrentRoutes(GetInputPortSelector(inputNumber), GetOutputPortSelector(outputNumber));
                            break;
                        case "aud":
                            (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.Audio, inputSlot);
                            UpdateCurrentRoutes(GetInputPortSelector(inputNumber), GetOutputPortSelector(outputNumber));
                            break;
                    }
                }
                else
                {
                    this.LogWarning("Could not find input slot {0} or output slot {1}", inputNumber, outputNumber);
                }

                return;
            }

            // Handle sync status bitmap response (e.g., "Frq00 01100110")
            if (message.StartsWith("Frq"))
            {
                var syncRegex = new System.Text.RegularExpressions.Regex(@"Frq\d+\s+([01]+)");
                var syncMatch = syncRegex.Match(message);
                if (syncMatch.Success)
                {
                    var syncBitmap = syncMatch.Groups[1].Value;
                    this.LogDebug("Sync status bitmap: {0}", syncBitmap);
                    
                    // Process each bit in the bitmap
                    for (int i = 0; i < syncBitmap.Length && i < inputCount; i++)
                    {
                        var inputNumber = i + 1; // Inputs are 1-based
                        var hasSync = syncBitmap[i] == '1';
                        
                        var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value as InputSlot;
                        if (inputSlot != null)
                        {
                            inputSlot.VideoSyncDetected = hasSync;
                            this.LogDebug("Input {0} sync status: {1}", inputNumber, hasSync);
                        }
                    }
                }
                return;
            }

            // Log unhandled messages for debugging
            this.LogDebug("Unhandled message: {0}", message);
        }

        /// <summary>
        /// Parses Extron firmware version response (verbose format)
        /// </summary>
        /// <param name="message">The message to parse</param>
        /// <returns>True if the message was a firmware version response and was processed</returns>
        private bool ParseFirmwareVersion(string message)
        {
            // Firmware version pattern for verbose response from "0Q" command
            // Example: "1.23-1.00(1.81LX-DTPCP108 -Fri, 31 Jul 2015 00:00:00 UTC)-1.00*(2.03LX-DTPCP108 -Fri, 30 Nov 2018 16:39:21 UTC)"
            var firmwareRegex = new System.Text.RegularExpressions.Regex(
                @"^(\d+\.\d+)-(\d+\.\d+)\(([^)]+)\)-(\d+\.\d+)\*\(([^)]+)\)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var match = firmwareRegex.Match(message.Trim());

            if (match.Success)
            {
                var mainVersion = match.Groups[1].Value;          // e.g., "1.23"
                var bootVersion = match.Groups[2].Value;          // e.g., "1.00"
                var bootDetails = match.Groups[3].Value;          // e.g., "1.81LX-DTPCP108 -Fri, 31 Jul 2015 00:00:00 UTC"
                var appVersion = match.Groups[4].Value;           // e.g., "1.00"
                var appDetails = match.Groups[5].Value;           // e.g., "2.03LX-DTPCP108 -Fri, 30 Nov 2018 16:39:21 UTC"

                this.LogDebug("Firmware version parsed - Main: {0}, Boot: {1}, App: {2}", mainVersion, bootVersion, appVersion);
                this.LogDebug("Boot details: {0}", bootDetails);
                this.LogDebug("App details: {0}", appDetails);

                // Extract model number from the boot or app details
                var modelRegex = new System.Text.RegularExpressions.Regex(@"(\d+\.\d+)([A-Z]+-[A-Z0-9]+)");
                var modelMatch = modelRegex.Match(bootDetails);
                var modelNumber = modelMatch.Success ? modelMatch.Groups[2].Value : "";

                // Extract application firmware version from app details
                var appFirmwareRegex = new System.Text.RegularExpressions.Regex(@"(\d+\.\d+)([A-Z]+-[A-Z0-9]+)");
                var appFirmwareMatch = appFirmwareRegex.Match(appDetails);
                var appFirmwareVersion = appFirmwareMatch.Success ? appFirmwareMatch.Groups[1].Value : appVersion;

                // Create comprehensive firmware version string
                var fullFirmwareVersion = $"Main: {mainVersion}, Boot: {bootVersion}, App: {appFirmwareVersion}";
                if (!string.IsNullOrEmpty(modelNumber))
                {
                    fullFirmwareVersion += $" ({modelNumber})";
                }

                // Update DeviceInfo with parsed firmware information
                UpdateDeviceInfoWithFirmware(fullFirmwareVersion, modelNumber);

                this.LogInformation("Device firmware version: {0}", fullFirmwareVersion);
                return true;
            }

            // Check for simpler firmware response patterns (fallback)
            var simpleFirmwareRegex = new System.Text.RegularExpressions.Regex(
                @"^(\d+\.\d+).*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var simpleMatch = simpleFirmwareRegex.Match(message.Trim());
            if (simpleMatch.Success && message.Length > 5) // Basic sanity check
            {
                var firmwareVersion = message.Trim();
                this.LogDebug("Simple firmware version detected: {0}", firmwareVersion);
                UpdateDeviceInfoWithFirmware(firmwareVersion, "");
                return true;
            }

            return false;
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
            // part number request
            SendText("N");
            // firmware version request (verbose)
            SendText("0Q");
        }

        public void PollRoutes()
        {
            // view all video ties
            SendText("\x1B0*1*1VC");
            // view all audio ties
            SendText("\x1B0*1*2VC");

            // query each output for video and audio
            for (int i = 1; i < outputCount; i++)
            {
                SendText($"{i}{VideoSwitch}");
                SendText($"{i}{AudioSwitch}");
            }
        }

        /// <summary>
        /// Poll device for input sync detection status
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

            // TODO [ ] Implement bridge links as needed

            // links to bridge
            trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

            trilist.SetBoolSigAction(joinMap.Connect.JoinNumber, sig => Connect = sig);
            ConnectFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Connect.JoinNumber]);

            StatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);
            OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            UpdateFeedbacks();

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;

                trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
                UpdateFeedbacks();
            };
        }

        #endregion

        private void UpdateFeedbacks()
        {
            // TODO [ ] Update as needed for the plugin being developed
            ConnectFeedback?.FireUpdate();
            OnlineFeedback?.FireUpdate();
            StatusFeedback?.FireUpdate();
        }

        /// <summary>
        /// Routes an input to an output for the specified signal type(s)
        /// </summary>
        /// <param name="inputSlotKey"></param>
        /// <param name="outputSlotKey"></param>
        /// <param name="type"></param>
        public void Route(string inputSlotKey, string outputSlotKey, eRoutingSignalType type)
        {
            if(type.HasFlag(eRoutingSignalType.AudioVideo))
            {
                var input = InputSlots[inputSlotKey] as InputSlot;
                var output = OutputSlots[outputSlotKey] as OutputSlot;
                if (input == null || output == null)
                {
                    Debug.LogError("Invalid input or output slot key");
                    return;
                }
                SetAvRoute(input.SlotNumber, output.SlotNumber);
                return;
            }

            if (type.HasFlag(eRoutingSignalType.Video))
            {
                var input = InputSlots[inputSlotKey] as InputSlot;
                var output = OutputSlots[outputSlotKey] as OutputSlot;
                if (input == null || output == null)
                {
                    Debug.LogError("Invalid input or output slot key");
                    return;
                }
                SetVideoRoute(input.SlotNumber, output.SlotNumber);
            }

            if (type.HasFlag(eRoutingSignalType.Audio))
            {
                var input = InputSlots[inputSlotKey] as InputSlot;
                var output = OutputSlots[outputSlotKey] as OutputSlot;
                if (input == null || output == null)
                {
                    Debug.LogError("Invalid input or output slot key");
                    return;
                }
                SetAudioRoute(input.SlotNumber, output.SlotNumber);
            }
        }

        /// <summary>
        /// Executes a switch from an input to an output for the specified signal type(s)
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        /// <param name="signalType"></param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            Debug.LogVerbose(this, "Making route from input {0} to output {1}", inputSelector, outputSelector);

            if (signalType.HasFlag(eRoutingSignalType.AudioVideo))
            {
                SetAvRoute((int)inputSelector, (int)outputSelector);
                UpdateCurrentRoutes((string)inputSelector, (string)outputSelector);
                return;
            }

            if (signalType.HasFlag(eRoutingSignalType.Video))
            {
                SetVideoRoute((int)inputSelector, (int)outputSelector);

                UpdateCurrentRoutes((string)inputSelector, (string)outputSelector);
            }
            if (signalType.HasFlag(eRoutingSignalType.Audio))
            {
                SetAudioRoute((int)inputSelector, (int)outputSelector);

                UpdateCurrentRoutes((string)inputSelector, (string)outputSelector);
            }            
        }

        /// <summary>
        /// Updates the current routes based on the input and output numbers.
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        private void UpdateCurrentRoutes(string inputSelector, string outputSelector)
        {
            RouteSwitchDescriptor descriptor;

            descriptor = GetRouteDescriptorByOutputPort(outputSelector);

            var inputPort = GetRoutingInputPortForSelector(inputSelector);

            var outputPort = GetRoutingOutputPortForSelector(outputSelector);

            if (outputPort is null)
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Warning, "Unable to find port for {outputNum}", this, outputSelector);
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
        /// Gets the route descriptor for the specified output port number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RouteSwitchDescriptor GetRouteDescriptorByOutputPort(string selector)
        {
            return CurrentRoutes.FirstOrDefault(rd =>
            {
                if (rd.OutputPort.Selector is not string opSelector)
                {
                    return false;
                }

                return opSelector == selector;
            });
        }

        /// <summary>
        /// Gets the routing input port for the specified input number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RoutingInputPort GetRoutingInputPortForSelector(string selector)
        {

            return InputPorts.FirstOrDefault(ip =>
            {
                if (ip.Selector is not string ipSelector)
                {
                    return false;
                }

                return ipSelector == selector;
            });
        }


        /// <summary>
        /// Gets the routing output port for the specified output number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RoutingOutputPort GetRoutingOutputPortForSelector(string selector)
        {

            return OutputPorts.FirstOrDefault(op =>
            {
                if (op.Selector is not string opSelector)
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

            this.LogDebug("DeviceInfo updated with firmware version: {0}", firmwareVersion);
        }
    }
}