using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;
using System;

namespace PepperDash.Essentials.Plugin.IOs
{
    public class ClearInput : IRoutingInputSlot
    {
        private readonly string key;

        public string TxDeviceKey => string.Empty;

        public int SlotNumber { get; private set; }

        public eRoutingSignalType SupportedSignalTypes => eRoutingSignalType.AudioVideo;

        public string Name { get; private set; }

        public BoolFeedback IsOnline => new BoolFeedback("IsOnline", () => false);

        public bool VideoSyncDetected => false;

        public string Key => $"{key}";

#pragma warning disable CS0067
        public event EventHandler VideoSyncChanged;
#pragma warning restore CS0067

        public ClearInput(string key, string name, int slotNum)
        {
            this.key = key;
            Name = name;
            SlotNumber = slotNum;
        }
    }
}
