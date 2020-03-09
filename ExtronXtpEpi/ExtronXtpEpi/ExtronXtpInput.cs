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
		private string _VideoName;
		private string _AudioName; 
        [JsonProperty("name")]
        public string Name { get; set; }

		[JsonProperty("videoName")]
		public string VideoName
		{
			get { return _VideoName ?? Name + "-Video"; }
			set { _VideoName = value; }
		}

        [JsonProperty("audioName")]
		public string AudioName
		{
			get { return _AudioName ?? Name + "-Audio"; }
			set { _AudioName = value; }
		}

        [JsonProperty("ioNumber")]
        public int IoNumber { get; set; }
    }
}