using System.Net;

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
			ClientIp = IPAddress.Parse("10.1.1.1"),
			Header = new NetFlowV5Header(),
			DateTimeOffset = new DateTimeOffset(2025, 1, 2, 3, 4, 5, 678, TimeSpan.Zero),
			SourceIp = IPAddress.Parse("192.168.1.10"),
			DestinationIp = IPAddress.Parse("8.8.8.8"),
			NextHopIp = IPAddress.Parse("192.168.1.1"),
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
			.Be("2025-01-02 03:04:05.678 10.1.1.1 192.168.1.10:51234 -> 8.8.8.8:443 6 17 packets 4096 bytes");
	}
}
