using PepperDash.Core;

namespace ExtronXtpEpi
{
	public static class DebugExt
	{
		public static uint Trace { get; set; }
		public static uint Debug { get; set; }
		public static uint Verbose { get; set; }

		static DebugExt()
		{
			ResetLevels();
		}

		public static void SetLevel(uint level)
		{
			if (level > 2) return;

			Trace = level;
			Debug = level;
			Verbose = level;
		}

		public static void ResetLevels()
		{
			Trace = 0;
			Debug = 0; //1;
			Verbose = 0; //2;
		}
	}
}