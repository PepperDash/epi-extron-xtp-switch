using System;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;

namespace PepperDash.Essentials.Plugin.IOs
{
    public class ClearInput : IRoutingInputSlot
    {
        private readonly string key;

        public string TxDeviceKey => string.Empty;

        public int SlotNumber { get; private set; }

        public eRoutingSignalType SupportedSignalTypes => eRoutingSignalType.AudioVideo;

        public string Name { get; private set; }

        public BoolFeedback IsOnline { get; private set; }

        public bool VideoSyncDetected => true;

        public string Key => $"{key}";

#pragma warning disable CS0067
        public event EventHandler VideoSyncChanged;
#pragma warning restore CS0067

        public ClearInput(string key, string name, int slotNum)
        {
            this.key = key;
            Name = name;
            IsOnline = new BoolFeedback("IsOnline", () => true);
            IsOnline.FireUpdate();
            SlotNumber = slotNum;
        }
    }
}
