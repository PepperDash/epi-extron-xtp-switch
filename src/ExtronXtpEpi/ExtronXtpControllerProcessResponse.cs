using System;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace ExtronXtpEpi
{
	public partial class ExtronXtpController
	{
		private void ProcessVideoRouteResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var input = Convert.ToInt32(responses[1].Replace("In", ""));
				var output = Convert.ToInt32(responses[0].Replace("Out", ""));

				Debug.Console(2, this, "ProcessVideoRouteResponse: input-'{0}', output-'{1}'",
					input, output);

				if (output == 0) return;

				videoOutputRoutes[output] = input;

				IntFeedback outputFeedback;
				if (VideoOutputFeedbacks.TryGetValue(output, out outputFeedback))
					outputFeedback.FireUpdate();

				StringFeedback nameFeedback;
				if (OutputVideoRouteNameFeedbacks.TryGetValue(output, out nameFeedback))
					nameFeedback.FireUpdate();
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessVideoRouteResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessAudioRouteResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var input = Convert.ToInt32(responses[1].Replace("In", ""));
				var output = Convert.ToInt32(responses[0].Replace("Out", ""));

				Debug.Console(2, this, "ProcessAudioRouteResponse: input-'{0}', output-'{1}'",
					input, output);

				if (output == 0) return;

				audioOutputRoutes[output] = input;

				IntFeedback outputFeedback;
				if (AudioOutputFeedbacks.TryGetValue(output, out outputFeedback))
					outputFeedback.FireUpdate();

				StringFeedback nameFeedback;
				if (OutputAudioNameFeedbacks.TryGetValue(output, out nameFeedback))
					nameFeedback.FireUpdate();
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessAudioRouteResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessVideoInputSyncResponse(string data)
		{
			try
			{
				var pertinentInfo = data.Split(' ')[1];
				for (var i = 1; i <= pertinentInfo.Length; i++)
				{
					var status = pertinentInfo[i];
					if (!videoInputSync.ContainsKey(i)) continue;

					var sync = Convert.ToInt32(status);

					videoInputSync[i] = (status == 1);

					BoolFeedback feedback;
					if (!VideoInputSyncFeedbacks.TryGetValue(i, out feedback)) return;

					if (feedback == null) return;

					feedback.FireUpdate();
				}
			}
			catch (Exception ex)
			{
				Debug.Console(1, this, "ProcessSignalSyncResponse Exception: {0}", ex.Message);
			}
		}

		private void ProcessOutputVolumeResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var volume = Convert.ToInt32(responses[1].Replace("Vol", ""));
				var output = Convert.ToInt32(responses[0].Replace("Out", ""));

				Debug.Console(2, this, "ProcessOutputVolumeResponse: output-'{0}', volume-'{1}",
					output, volume);

				if (output == 0) return;

				IntWithFeedback feedback;
				if (!OutputAudioLevelFeedbacks.TryGetValue(output, out feedback)) return;

				if (feedback == null) return;

				feedback.Value = volume * 1024 - 1;
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessOutputVolumeResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessOutputMuteResponse(string data)
		{
			try
			{
				var responses = data.Split(' ');

				var mute = Convert.ToInt32(responses[1]);
				var output = Convert.ToInt32(responses[0].Replace("Amt", ""));

				Debug.Console(2, this, "ProcessOutputMuteResponse: output-'{0}', mute-'{1}",
					output, mute);

				if (output == 0) return;

				BoolWithFeedback feedback;
				if (!OutputAudioMuteFeedbacks.TryGetValue(output, out feedback)) return;

				if (feedback == null) return;

				feedback.Value = (mute > 0);
			}
			catch (Exception ex)
			{
				Debug.ConsoleWithLog(1, this, "ProcessOutputMuteResponse Excepton: {0}", ex.Message);
			}
		}

		private void ProcessErrorResponse(string data)
		{
			var error = Convert.ToInt32(data);
			var errorMsg = string.Empty;

			switch (error)
			{
				case (1):
					{
						errorMsg = "Invalid input channel number (out of range)";
						break;
					}
				case (10):
					{
						errorMsg = "Invalid command";
						break;
					}
				case (11):
					{
						errorMsg = "Invalid preset number (out of range)";
						break;
					}
				case (12):
					{
						errorMsg = "Invalid output number (out of range)";
						break;
					}
				case (13):
					{
						errorMsg = "Invalid value (out of range)";
						break;
					}
				case (14):
					{
						errorMsg = "Invalid command for this configuration";
						break;
					}
				case (17):
					{
						errorMsg = "Timeout (caused only by direct write of global presets";
						break;
					}
				case (22):
					{
						errorMsg = "Busy";
						break;
					}
				case (24):
					{
						errorMsg = "Privileges violation (users have access to all view and read commands [other than the administrator password], and can create ties, presets, and audio mutes";
						break;
					}
				case (25):
					{
						errorMsg = "Device not present";
						break;
					}
				case (26):
					{
						errorMsg = "Maximum number of connections exceeded";
						break;
					}
				case (27):
					{
						errorMsg = "Invalid event number";
						break;
					}
				case (28):
					{
						errorMsg = "Bad filename / file not found";
						break;
					}
				default:
					{
						errorMsg = "Unknown error";
						break;
					}
			}

			Debug.Console(1, this, "Error{0:02}: {1}", error, errorMsg);
		}
	}
}