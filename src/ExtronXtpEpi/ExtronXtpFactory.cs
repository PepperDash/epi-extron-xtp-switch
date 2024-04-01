using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace ExtronXtpEpi
{
	public class ExtronXtpFactory : EssentialsPluginDeviceFactory<ExtronXtpController>
	{
		public ExtronXtpFactory()
		{
			MinimumEssentialsFrameworkVersion = "1.11.1";

			TypeNames = new List<string> { "extronXtp" };
		}

		public override EssentialsDevice BuildDevice(DeviceConfig dc)
		{
			Debug.Console(DebugExt.Trace, "Factory Attempting to create new device from type: {0}", dc.Type, dc.Name);

			var propertiesConfig = dc.Properties.ToObject<ExtronXtpPropertiesConfig>();
			if (propertiesConfig == null)
			{
				Debug.Console(DebugExt.Verbose, "[{0}] Factory: failed to read properties config for {1}", dc.Key, dc.Name);
				return null;
			}

			var comms = CommFactory.CreateCommForDevice(dc);
			if(comms != null) return new ExtronXtpController(dc.Key, dc.Name, propertiesConfig, comms);

			Debug.Console(DebugExt.Trace, "[{0}] Factory: failed to create {2} comms for {1}", dc.Key, dc.Name, dc.Properties["control"]["method"].Value<string>());
			return null;
		}
	}
}