using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Core;
using PepperDash.Essentials.Core;

using Newtonsoft.Json;

namespace ExtronXtpEpi
{
    public class ExtronXtpIo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("videoName")]
        public string VideoName { get; set; }

        [JsonProperty("audioName")]
        public string AudioName { get; set; }

        [JsonProperty("ioNumber")]
        public int IoNumber { get; set; }
    }
}