using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace ExtronXtpEpi
{
    public class ExtronXtpPropertiesConfig
    {
        [JsonProperty("control")]
        public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("inputs")]
        public List<ExtronXtpIoConfig> Inputs { get; set; }

        [JsonProperty("outputs")]
        public List<ExtronXtpIoConfig> Outputs { get; set; }

        [JsonProperty("username")]
        public string UserName { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

		[JsonProperty("virtualMode")]
		public bool VirtualMode { get; set; }

		[JsonProperty("pollTime")]
		public int? PollTime { get; set; }

	    public ExtronXtpPropertiesConfig()
	    {
		    Inputs = new List<ExtronXtpIoConfig>();
			Outputs = new List<ExtronXtpIoConfig>();
	    }
    }

	public class ExtronXtpIoConfig
	{
		private string _name;
		private string _videoName;
		private string _audioName;

		[JsonProperty("name")]
		public string Name
		{
			get { return _name ?? _videoName; }
			set { _name = value; }
		}

		[JsonProperty("videoName")]
		public string VideoName
		{
			get { return _videoName ?? Name + "-Video"; }
			set { _videoName = value; }
		}

		[JsonProperty("audioName")]
		public string AudioName
		{
			get { return _audioName ?? Name + "-Audio"; }
			set { _audioName = value; }
		}

		[JsonProperty("ioNumber")]
		public int IoNumber { get; set; }
	}
}