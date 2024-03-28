using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace ExtronXtpEpi
{
	public class ExtronXtpBridgeJoinMap : DmChassisControllerJoinMap
	{
		#region Digitals

		// TODO [ ] Review IsOnline join data, EPI uses digital-1, DmChassisControllJoinMap uses digital-11
		[JoinName("IsOnlineXtpLegacy")]
		public JoinDataComplete IsOnlineXtpLegacy = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Extron XTP Chassis Online (LEGACY)", 
				JoinCapabilities = eJoinCapabilities.ToSIMPL, 
				JoinType = eJoinType.Digital
			});

		[JoinName("RouteReady")]
		public JoinDataComplete RouteReady = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 2,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "Reports to bridge users the chassis is ready routies",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("OutputVolumeUp")]
		public JoinDataComplete OutputVolumeUp = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1401,
				JoinSpan = 32
			},
			new JoinMetadata
			{
				Description = "Output Volume Up",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});

		[JoinName("OutputVolumeDown")]
		public JoinDataComplete OutputVolumeDown = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1601,
				JoinSpan = 32
			},
			new JoinMetadata
			{
				Description = "Output Volume Down",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Digital
			});


		[JoinName("OutputMuteToggle")]
		public JoinDataComplete OutputMuteToggle = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1801,
				JoinSpan = 32
			},
			new JoinMetadata
			{
				Description = "Output Mute Toggle",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Digital
			});
		#endregion


		#region Analogs

		[JoinName("SocketStatus")]
		public JoinDataComplete SocketStatus = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1,
				JoinSpan = 1
			},
			new JoinMetadata
			{
				Description = "SocketStatus",
				JoinCapabilities = eJoinCapabilities.ToSIMPL,
				JoinType = eJoinType.Analog
			});

		[JoinName("OutputVolume")]
		public JoinDataComplete OutputVolume = new JoinDataComplete(
			new JoinData
			{
				JoinNumber = 1401,
				JoinSpan = 32
			},
			new JoinMetadata
			{
				Description = "Output Volume Set/Get",
				JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
				JoinType = eJoinType.Analog
			});

		#endregion

		#region Serials


		#endregion

		public ExtronXtpBridgeJoinMap(uint joinStart)
			: base(joinStart, typeof(ExtronXtpBridgeJoinMap))
		{}
	}
}
