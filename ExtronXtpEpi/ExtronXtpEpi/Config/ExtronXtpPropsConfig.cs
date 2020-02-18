using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core.Config;

using Newtonsoft.Json;

namespace ExtronXtpEpi.Config
{
    public class ExtronXtpPropsConfig
    {
        public static ExtronXtpPropsConfig FromConfig(DeviceConfig config)
        {
            return JsonConvert.DeserializeObject<ExtronXtpPropsConfig>(config.Properties.ToString());
        }

        [JsonProperty("control")]
        public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("inputs")]
        public List<ExtronXtpIo> Inputs { get; set; }

        [JsonProperty("outputs")]
        public List<ExtronXtpIo> Outputs { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }
}