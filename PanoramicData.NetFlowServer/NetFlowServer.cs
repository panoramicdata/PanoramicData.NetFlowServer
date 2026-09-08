using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PanoramicData.NetFlowServer.Config;
using PanoramicData.NetFlowServer.Interfaces;
using PanoramicData.NetFlowServer.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PanoramicData.NetFlowServer;

/// <summary>
/// A hosted service that listens for and processes NetFlow v5 UDP packets.
/// </summary>
/// <param name="options">The NetFlow server configuration options.</param>
/// <param name="loggerFactory">The logger factory.</param>
/// <param name="syslogApplication">The application that handles received NetFlow records.</param>
public class NetFlowServer(
	IOptions<NetFlowServerConfiguration> options,
	ILoggerFactory loggerFactory,
	INetFlowApplication syslogApplication) : IHostedService, IDisposable
{
	/// <summary>
	/// The number of bytes in a NetFlow v5 packet header.
	/// </summary>
	private const int V5HeaderLength = 24;

	/// <summary>
	/// The number of bytes in a single NetFlow v5 flow record.
	/// </summary>
	private const int V5RecordLength = 48;

	/// <summary>
	/// The maximum number of flow records a NetFlow v5 packet may contain.
	/// </summary>
	private const int V5MaxRecordCount = 30;

	private readonly Lock _lock = new();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly ILogger _logger = loggerFactory.CreateLogger<NetFlowServer>();
	private bool _started;
	private bool _disposedValue;
	private Task? _udpListenerTask;

	/// <summary>
	/// Gets the unique identifier for this server instance.
	/// </summary>
	public Guid Id { get; } = Guid.NewGuid();

	private readonly NetFlowServerConfiguration _config = (options ?? throw new ArgumentNullException(nameof(options))).Value;

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_started)
		{
			throw new InvalidOperationException("The server is already started.");
		}

		if (_config.UdpPort <= 0)
		{
			throw new InvalidOperationException("UDP port not configured.");
		}

		_logger.LogInformation("Starting UDP listener on port {UdpPort}...", _config.UdpPort);
		try
		{
			_udpListenerTask = UdpListenerLoopAsync(_config.UdpPort, _cancellationTokenSource.Token);
			_logger.LogInformation("Starting UDP listener on port {UdpPort} complete.", _config.UdpPort);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Error starting UDP listener on port {UdpPort}: {Message}",
				_config.UdpPort,
				ex.Message);
		}

		_started = true;

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			if (!_started)
			{
				return Task.CompletedTask;
			}

			_cancellationTokenSource.Cancel();

			_udpListenerTask?.Wait(cancellationToken);

			_started = false;

		}

		return Task.CompletedTask;
	}

	private async Task UdpListenerLoopAsync(int udpServerPort, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Creating UDP Client...");
		using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, udpServerPort));

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_logger.LogDebug("Waiting for UDP packet...");
				var receiveResult = await udpClient.ReceiveAsync(cancellationToken);

				await ProcessNetFlowMessageAsync(receiveResult);
			}
			catch (OperationCanceledException)
			{
				// Stopping the server cancels the receive; that is an ordinary shutdown,
				// so the loop ends rather than faulting the listener task.
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in UDP listener");
			}
		}
	}
	private Task ProcessNetFlowMessageAsync(UdpReceiveResult udpReceiveResult)
	{
		try
		{
			// Determine the version
			var data = udpReceiveResult.Buffer;
			if (data.Length < 2)
			{
				_logger.LogWarning("NetFlow message too short to determine version");
				return Task.CompletedTask;
			}

			var version = (data[0] << 8) | data[1];

			if (version == 5)
			{
				ProcessNetFlowV5(udpReceiveResult);
			}
			else
			{
				_logger.LogWarning("Unsupported NetFlow version: {Version}", version);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing netflow message");
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Protocols from https://www.cisco.com/c/en/us/td/docs/net_mgmt/netflow_collection_engine/3-6/user/guide/format.html
	/// </summary>
	/// <param name="udpReceiveResult"></param>
	/// <exception cref="FormatException"></exception>
	private void ProcessNetFlowV5(UdpReceiveResult udpReceiveResult)
	{
		var data = udpReceiveResult.Buffer;

		if (data.Length < V5HeaderLength)
		{
			_logger.LogWarning("Invalid NetFlow v5 packet");
			return;
		}

		// All data is bigendian
		var recordCount = ReadUInt16(data, 2);
		_logger.LogDebug("NetFlow v5: {RecordCount} records (max is {MaxRecordCount})", recordCount, V5MaxRecordCount);
		if (recordCount > V5MaxRecordCount)
		{
			throw new FormatException($"Too many records.  Max was {V5MaxRecordCount}, received {recordCount}.");
		}

		var header = ReadV5Header(data);

		// Iterate through the records, which follow the header.
		var offset = V5HeaderLength;
		for (var i = 0; i < recordCount; i++)
		{
			if (offset + V5RecordLength > data.Length)
			{
				break;
			}

			var netflowV5Record = ReadV5Record(data, offset, header, udpReceiveResult.RemoteEndPoint.Address);

			syslogApplication.NetFlowRecordReceived(this, netflowV5Record);

			offset += V5RecordLength;
		}
	}

	/// <summary>
	/// Reads the NetFlow v5 header from the start of a packet.
	/// </summary>
	private static NetFlowV5Header ReadV5Header(byte[] data)
	{
		// Next 4 bytes are Client Uptime in milliseconds
		var clientUptimeMilliseconds = ReadInt32(data, 4);

		// Next 4 bytes are Current count of seconds since 0000 UTC 1970
		var unixSeconds = ReadInt32(data, 8);

		// Next 4 bytes are Residual nanoseconds since 0000 UTC 1970
		var residualNanoseconds = ReadInt32(data, 12);

		// Next 4 bytes are Sequence number of total flows seen
		var sequenceNumber = ReadInt32(data, 16);

		// Next 1 byte is the engine type
		var engineType = data[20];

		// Next 1 byte is the engine ID
		var engineId = data[21];

		// Next 2 bytes are sampling mode and interval
		var samplingModeAndInterval = ReadUInt16(data, 22);

		return new NetFlowV5Header
		{
			ClientUptimeMilliseconds = clientUptimeMilliseconds,
			DateTimeOffset = DateTimeOffset
				.FromUnixTimeSeconds(unixSeconds)
				.AddTicks(residualNanoseconds / 100),
			SequenceNumber = sequenceNumber,
			EngineType = engineType,
			EngineId = engineId,

			// The first two bits hold the sampling mode
			SamplingMode = (samplingModeAndInterval & 0xC000) >> 14,

			// The remaining 14 bits hold the sampling interval
			SamplingInterval = samplingModeAndInterval & 0x3FFF
		};
	}

	/// <summary>
	/// Reads a single NetFlow v5 flow record at the given offset.
	/// </summary>
	private static NetFlowV5Record ReadV5Record(
		byte[] data,
		int offset,
		NetFlowV5Header header,
		IPAddress clientIp)
		=> new()
		{
			ClientIp = clientIp,
			Header = header,
			DateTimeOffset = header.DateTimeOffset,

			// 4 bytes: Source address
			SourceIp = new IPAddress(data[offset..(offset + 4)]),

			// 4 bytes: Destination address
			DestinationIp = new IPAddress(data[(offset + 4)..(offset + 8)]),

			// 4 bytes: Next hop IP address
			NextHopIp = new IPAddress(data[(offset + 8)..(offset + 12)]),

			// 2 bytes: SNMP index of Input interface
			InputInterfaceIndex = ReadUInt16(data, offset + 12),

			// 2 bytes: SNMP index of Output interface
			OutputInterfaceIndex = ReadUInt16(data, offset + 14),

			// 4 bytes: Flow packet count
			PacketCount = ReadInt32(data, offset + 16),

			// 4 bytes: Total number of L3 bytes in the packets of the flow
			TotalL3Bytes = ReadInt32(data, offset + 20),

			// 4 bytes: SysUptime at start of flow
			SysUptime = ReadInt32(data, offset + 24),

			// 4 bytes: SysUptime at the time the last packet of the flow was received
			LastPacketSysUptime = ReadInt32(data, offset + 28),

			// 2 bytes: Source port
			SourcePort = ReadUInt16(data, offset + 32),

			// 2 bytes: Destination port
			DestinationPort = ReadUInt16(data, offset + 34),

			// 1 byte: Padding
			Padding = data[offset + 36],

			// 1 byte: TCP flags
			TcpFlags = data[offset + 37],

			// 1 byte: IP protocol type, e.g., 6=TCP, 17=UDP, 1=ICMP, 2=IGMP, 89=OSPF
			Protocol = data[offset + 38],

			// 1 byte: IP type of service
			TypeOfService = data[offset + 39],

			// 2 bytes: Autonomous system number of the source, either origin or peer
			SourceAsNumber = ReadUInt16(data, offset + 40),

			// 2 bytes: Autonomous system number of the destination, either origin or peer
			DestinationAsNumber = ReadUInt16(data, offset + 42),

			// 1 byte: Source address prefix mask bits
			SourcePrefixMaskBits = data[offset + 44],

			// 1 byte: Destination address prefix mask bits
			DestinationPrefixMaskBits = data[offset + 45],

			// 2 bytes: Unused
			Unused = ReadUInt16(data, offset + 46)
		};

	/// <summary>
	/// Reads a big-endian 16 bit unsigned value.
	/// </summary>
	private static int ReadUInt16(byte[] data, int offset)
		=> (data[offset] << 8) | data[offset + 1];

	/// <summary>
	/// Reads a big-endian 32 bit value.
	/// </summary>
	private static int ReadInt32(byte[] data, int offset)
		=> (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

	/// <summary>
	/// Releases the unmanaged resources and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_cancellationTokenSource.Dispose();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
