using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PanoramicData.NetFlowServer.Config;
using System.Net;

namespace PanoramicData.NetFlowServer.Test;

/// <summary>
/// Tests for <see cref="NetFlowServer"/>: it listens for NetFlow v5 datagrams and hands each flow
/// record in them to the application, without letting a malformed packet stop it listening.
/// </summary>
public class NetFlowServerTests
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	/// <summary>
	/// The period a test waits before concluding that a packet was, correctly, not turned into a record.
	/// </summary>
	private static readonly TimeSpan IgnoredPacketGracePeriod = TimeSpan.FromMilliseconds(500);

	[Fact]
	public void Constructor_WithNullOptions_Throws()
	{
		var construct = () => new NetFlowServer(null!, NullLoggerFactory.Instance, new RecordingNetFlowApplication());

		construct.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Id_IsUniquePerInstance()
	{
		using var application = new RecordingNetFlowApplication();
		using var first = NetFlowServerFixture.CreateServer(1234, application);
		using var second = NetFlowServerFixture.CreateServer(1234, application);

		first.Id.Should().NotBeEmpty();
		first.Id.Should().NotBe(second.Id);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task StartAsync_WithoutConfiguredPort_Throws(int udpPort)
	{
		using var application = new RecordingNetFlowApplication();
		using var server = new NetFlowServer(
			Options.Create(new NetFlowServerConfiguration { UdpPort = udpPort }),
			NullLoggerFactory.Instance,
			application);

		var start = async () => await server.StartAsync(TestContext.Current.CancellationToken);

		await start.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("UDP port not configured.");
	}

	[Fact]
	public async Task StartAsync_WhenAlreadyStarted_Throws()
	{
		await using var fixture = await NetFlowServerFixture.StartAsync(TestContext.Current.CancellationToken);

		var startAgain = async () => await fixture.Server.StartAsync(TestContext.Current.CancellationToken);

		await startAgain.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("The server is already started.");
	}

	[Fact]
	public async Task StopAsync_WhenNeverStarted_DoesNothing()
	{
		using var application = new RecordingNetFlowApplication();
		using var server = NetFlowServerFixture.CreateServer(1234, application);

		var stop = async () => await server.StopAsync(TestContext.Current.CancellationToken);

		await stop.Should().NotThrowAsync();
	}

	/// <summary>
	/// Stopping is an ordinary shutdown, not a failure: cancelling the listener must not surface
	/// as an exception to whichever host is stopping the service.
	/// </summary>
	[Fact]
	public async Task StopAsync_AfterStarting_DoesNotThrow()
	{
		using var application = new RecordingNetFlowApplication();
		using var server = NetFlowServerFixture.CreateServer(NetFlowServerFixture.GetFreeUdpPort(), application);
		await server.StartAsync(TestContext.Current.CancellationToken);

		var stop = async () => await server.StopAsync(TestContext.Current.CancellationToken);

		await stop.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ReceiveV5Packet_ParsesHeader()
	{
		var timestamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
		var packet = new NetFlowV5PacketBuilder
		{
			ClientUptimeMilliseconds = 123_456,
			Timestamp = timestamp,
			SequenceNumber = 42,
			EngineType = 7,
			EngineId = 9,
			SamplingMode = 2,
			SamplingInterval = 1_000
		}
			.AddRecord(new NetFlowV5RecordValues())
			.Build();

		var record = await SendAndReceiveSingleRecordAsync(packet);

		record.Header.Should().BeEquivalentTo(new
		{
			ClientUptimeMilliseconds = 123_456,
			DateTimeOffset = timestamp,
			SequenceNumber = 42,
			EngineType = (byte)7,
			EngineId = (byte)9,
			SamplingMode = 2,
			SamplingInterval = 1_000
		});
	}

	[Fact]
	public async Task ReceiveV5Packet_AddsResidualNanosecondsToTheTimestamp()
	{
		var timestamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
		var packet = new NetFlowV5PacketBuilder
		{
			Timestamp = timestamp,

			// 100 nanoseconds is one tick, so half a second is 5,000,000 ticks.
			ResidualNanoseconds = 500_000_000
		}
			.AddRecord(new NetFlowV5RecordValues())
			.Build();

		var record = await SendAndReceiveSingleRecordAsync(packet);

		record.Header.DateTimeOffset.Should().Be(timestamp.AddTicks(5_000_000));
	}

	[Fact]
	public async Task ReceiveV5Packet_ParsesRecordFields()
	{
		var packet = new NetFlowV5PacketBuilder()
			.AddRecord(new NetFlowV5RecordValues
			{
				SourceIp = IPAddress.Parse("192.168.1.10"),
				DestinationIp = IPAddress.Parse("8.8.8.8"),
				NextHopIp = IPAddress.Parse("192.168.1.1"),
				InputInterfaceIndex = 3,
				OutputInterfaceIndex = 4,
				PacketCount = 17,
				TotalL3Bytes = 4_096,
				SysUptime = 1_000,
				LastPacketSysUptime = 2_000,
				SourcePort = 51_234,
				DestinationPort = 443,
				Padding = 1,
				TcpFlags = 0x18,
				Protocol = 6,
				TypeOfService = 0x28,
				SourceAsNumber = 64_512,
				DestinationAsNumber = 15_169,
				SourcePrefixMaskBits = 24,
				DestinationPrefixMaskBits = 32,
				Unused = 5
			})
			.Build();

		var record = await SendAndReceiveSingleRecordAsync(packet);

		record.SourceIp.Should().Be(IPAddress.Parse("192.168.1.10"));
		record.DestinationIp.Should().Be(IPAddress.Parse("8.8.8.8"));
		record.NextHopIp.Should().Be(IPAddress.Parse("192.168.1.1"));
		record.InputInterfaceIndex.Should().Be(3);
		record.OutputInterfaceIndex.Should().Be(4);
		record.PacketCount.Should().Be(17);
		record.TotalL3Bytes.Should().Be(4_096);
		record.SysUptime.Should().Be(1_000);
		record.LastPacketSysUptime.Should().Be(2_000);
		record.SourcePort.Should().Be(51_234);
		record.DestinationPort.Should().Be(443);
		record.Padding.Should().Be(1);
		record.TcpFlags.Should().Be(0x18);
		record.Protocol.Should().Be(6);
		record.TypeOfService.Should().Be(0x28);
		record.SourceAsNumber.Should().Be(64_512);
		record.DestinationAsNumber.Should().Be(15_169);
		record.SourcePrefixMaskBits.Should().Be(24);
		record.DestinationPrefixMaskBits.Should().Be(32);
		record.Unused.Should().Be(5);

		record.ClientIp.Should().Be(IPAddress.Loopback, "the record records who sent the packet");
	}

	[Fact]
	public async Task ReceiveV5Packet_WithMultipleRecords_RaisesOnePerRecord()
	{
		var packet = new NetFlowV5PacketBuilder()
			.AddRecord(new NetFlowV5RecordValues { SourcePort = 1, DestinationPort = 80 })
			.AddRecord(new NetFlowV5RecordValues { SourcePort = 2, DestinationPort = 443 })
			.AddRecord(new NetFlowV5RecordValues { SourcePort = 3, DestinationPort = 8080 })
			.Build();

		using var cancellationTokenSource = new CancellationTokenSource(Timeout);
		await using var fixture = await NetFlowServerFixture.StartAsync(cancellationTokenSource.Token);
		await fixture.SendAsync(packet, cancellationTokenSource.Token);

		var records = await fixture.Application.WaitForRecordsAsync(3, cancellationTokenSource.Token);

		records.Select(r => r.SourcePort).Should().Equal(1, 2, 3);
		records.Select(r => r.DestinationPort).Should().Equal(80, 443, 8080);

		records.Select(r => r.Header).Distinct()
			.Should().ContainSingle("every record in a packet shares that packet's header");
	}

	/// <summary>
	/// A record count larger than the bytes present is a truncated packet: the records that are
	/// wholly present are still delivered, and the server keeps running.
	/// </summary>
	[Fact]
	public async Task ReceiveV5Packet_DeclaringMoreRecordsThanItCarries_DeliversTheCompleteOnes()
	{
		var packet = new NetFlowV5PacketBuilder { DeclaredRecordCount = 4 }
			.AddRecord(new NetFlowV5RecordValues { SourcePort = 1 })
			.Build();

		var record = await SendAndReceiveSingleRecordAsync(packet);

		record.SourcePort.Should().Be(1);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(9)]
	public async Task ReceivePacketOfUnsupportedVersion_IsIgnored(int version)
	{
		var packet = new NetFlowV5PacketBuilder { Version = version }
			.AddRecord(new NetFlowV5RecordValues())
			.Build();

		await AssertPacketIsIgnoredAsync(packet);
	}

	[Fact]
	public async Task ReceiveV5PacketShorterThanItsHeader_IsIgnored()
	{
		var packet = new NetFlowV5PacketBuilder()
			.AddRecord(new NetFlowV5RecordValues())
			.Build();

		await AssertPacketIsIgnoredAsync(packet[..8]);
	}

	[Fact]
	public async Task ReceivePacketTooShortToCarryAVersion_IsIgnored()
		=> await AssertPacketIsIgnoredAsync([5]);

	/// <summary>
	/// A packet declaring more than the 30 records NetFlow v5 permits is rejected, and the
	/// listener survives to handle the next packet.
	/// </summary>
	[Fact]
	public async Task ReceiveV5Packet_DeclaringTooManyRecords_IsRejectedAndTheServerKeepsListening()
	{
		var tooManyRecords = new NetFlowV5PacketBuilder { DeclaredRecordCount = 31 }
			.AddRecord(new NetFlowV5RecordValues { SourcePort = 1 })
			.Build();
		var valid = new NetFlowV5PacketBuilder()
			.AddRecord(new NetFlowV5RecordValues { SourcePort = 2 })
			.Build();

		using var cancellationTokenSource = new CancellationTokenSource(Timeout);
		await using var fixture = await NetFlowServerFixture.StartAsync(cancellationTokenSource.Token);

		await fixture.SendAsync(tooManyRecords, cancellationTokenSource.Token);
		await fixture.SendAsync(valid, cancellationTokenSource.Token);

		var records = await fixture.Application.WaitForRecordsAsync(1, cancellationTokenSource.Token);

		records.Should().ContainSingle("the rejected packet yields nothing")
			.Which.SourcePort.Should().Be(2, "the packet after it is still handled");
	}

	private static async Task<NetFlowV5Record> SendAndReceiveSingleRecordAsync(byte[] packet)
	{
		using var cancellationTokenSource = new CancellationTokenSource(Timeout);
		await using var fixture = await NetFlowServerFixture.StartAsync(cancellationTokenSource.Token);

		await fixture.SendAsync(packet, cancellationTokenSource.Token);

		var records = await fixture.Application.WaitForRecordsAsync(1, cancellationTokenSource.Token);

		return records.Should().ContainSingle().Subject;
	}

	private static async Task AssertPacketIsIgnoredAsync(byte[] packet)
	{
		using var cancellationTokenSource = new CancellationTokenSource(Timeout);
		await using var fixture = await NetFlowServerFixture.StartAsync(cancellationTokenSource.Token);

		await fixture.SendAsync(packet, cancellationTokenSource.Token);

		var noRecordArrived = await fixture.Application.NoRecordArrivesWithinAsync(IgnoredPacketGracePeriod);

		noRecordArrived.Should().BeTrue("the packet cannot be parsed, so it yields no record");
	}
}
