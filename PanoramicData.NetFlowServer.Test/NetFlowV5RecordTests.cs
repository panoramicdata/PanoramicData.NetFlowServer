namespace PanoramicData.NetFlowServer.Test;

/// <summary>
/// Tests for <see cref="NetFlowV5Record"/>.
/// </summary>
public class NetFlowV5RecordTests
{
	[Fact]
	public void ToString_SummarisesTheFlow()
	{
		var record = new NetFlowV5Record
		{
			ClientIp = TestAddresses.Client,
			Header = new NetFlowV5Header(),
			DateTimeOffset = new DateTimeOffset(2025, 1, 2, 3, 4, 5, 678, TimeSpan.Zero),
			SourceIp = TestAddresses.Source,
			DestinationIp = TestAddresses.Destination,
			NextHopIp = TestAddresses.NextHop,
			InputInterfaceIndex = 1,
			OutputInterfaceIndex = 2,
			PacketCount = 17,
			TotalL3Bytes = 4_096,
			SysUptime = 1_000,
			LastPacketSysUptime = 2_000,
			SourcePort = 51_234,
			DestinationPort = 443,
			Padding = 0,
			TcpFlags = 0x18,
			Protocol = 6,
			TypeOfService = 0,
			SourceAsNumber = 64_512,
			DestinationAsNumber = 15_169,
			SourcePrefixMaskBits = 24,
			DestinationPrefixMaskBits = 32,
			Unused = 0
		};

		record.ToString().Should()
			.Be("2025-01-02 03:04:05.678 203.0.113.1 192.0.2.10:51234 -> 198.51.100.8:443 6 17 packets 4096 bytes");
	}
}
