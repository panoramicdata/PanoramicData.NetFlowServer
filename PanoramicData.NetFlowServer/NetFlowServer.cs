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

public partial class NetFlowServer(
	IOptions<NetFlowServerConfiguration> options,
	ILoggerFactory loggerFactory,
	INetFlowApplication syslogApplication) : IHostedService, IDisposable
{
	private readonly Lock _lock = new();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private readonly ILogger _logger = loggerFactory.CreateLogger<NetFlowServer>();
	private bool _started;
	private bool _disposedValue;
	private Task? _udpListenerTask;

	public Guid Id { get; } = Guid.NewGuid();

	private readonly NetFlowServerConfiguration _config = (options ?? throw new ArgumentNullException(nameof(options))).Value;

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

		_logger.LogDebug("Creating remote endpoint definition...");
		var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_logger.LogDebug("Waiting for UDP packet...");
				var receiveResult = await udpClient.ReceiveAsync(cancellationToken);

				await ProcessNetFlowMessageAsync(receiveResult);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
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

			switch (version)
			{
				case 5:
					ProcessNetFlowV5(udpReceiveResult);
					break;

				default:
					Console.WriteLine($"Unsupported NetFlow version: {version}");
					break;
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

		if (data.Length < 24)
		{
			Console.WriteLine("Invalid NetFlow v5 packet");
			return;
		}

		// All data is bigendian

		var recordCount = (data[2] << 8) | data[3];
		_logger.LogDebug("NetFlow v5: {recordCount} records (max is 30)", recordCount);
		if (recordCount > 30)
		{
			throw new FormatException("Too many records.  Max was 30, received {recordCount}.");
		}

		// Next 4 bytes are Client Uptime in milliseconds
		var clientUptimeMilliseconds = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];

		// Next 4 bytes are Current count of seconds since 0000 UTC 1970
		var unixSeconds = (data[8] << 24) | (data[9] << 16) | (data[10] << 8) | data[11];
		// Next 4 bytes are Residual nanoseconds since 0000 UTC 1970
		var residualNanoseconds = (data[12] << 24) | (data[13] << 16) | (data[14] << 8) | data[15];

		var dateTime = DateTimeOffset
			.FromUnixTimeSeconds(unixSeconds)
			.AddTicks(residualNanoseconds / 100);

		// Next 4 bytes are Sequence number of total flows seen
		var sequenceNumber = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];

		// Next 1 byte is the engine type
		var engineType = data[20];

		// Next 1 byte is the engine ID
		var engineId = data[21];

		// Next 2 bytes are sampling interval
		var samplingModeAndInterval = (data[22] << 8) | data[23];

		// First two bits hold the sampling mode
		var samplingMode = (samplingModeAndInterval & 0xC000) >> 14;

		// Remaining 14 bits hold the sampling interval
		var samplingInterval = samplingModeAndInterval & 0x3FFF;

		// Create a header object
		var header = new NetFlowV5Header
		{
			ClientUptimeMilliseconds = clientUptimeMilliseconds,
			DateTimeOffset = dateTime,
			SequenceNumber = sequenceNumber,
			EngineType = engineType,
			EngineId = engineId,
			SamplingMode = samplingMode,
			SamplingInterval = samplingInterval
		};

		// Iterate through the records.
		// Offset starts at header size 24 bytes
		// Each record is 48 bytes
		var offset = 24;
		for (var i = 0; i < recordCount; i++)
		{
			if (offset + 48 > data.Length)
			{
				break;
			}

			// 4 bytes: Source address
			var sourceIp = new IPAddress(data[offset..(offset + 4)]);

			// 4 bytes: Destination address
			var destinationIp = new IPAddress(data[(offset + 4)..(offset + 8)]);

			// 4 bytes: Next hop IP address
			var nextHopIp = new IPAddress(data[(offset + 8)..(offset + 12)]);

			// 2 bytes: SNMP index of Input interface
			var inputInterfaceIndex = (data[offset + 12] << 8) | data[offset + 13];

			// 2 bytes: SNMP index of Output interface
			var outputInterfaceIndex = (data[offset + 14] << 8) | data[offset + 15];

			// 4 bytes: Flow packet count
			var packetCount = (data[offset + 16] << 24) | (data[offset + 17] << 16) | (data[offset + 18] << 8) | data[offset + 19];

			// 4 bytes: Total number of L3 bytes in the packets of the flow
			var totalL3Bytes = (data[offset + 20] << 24) | (data[offset + 21] << 16) | (data[offset + 22] << 8) | data[offset + 23];

			// 4 bytes: SysUptime at start of flow
			var sysUptime = (data[offset + 24] << 24) | (data[offset + 25] << 16) | (data[offset + 26] << 8) | data[offset + 27];

			// 4 bytes: SysUptime at the time the last packet of the flow was received
			var lastPacketSysUptime = (data[offset + 28] << 24) | (data[offset + 29] << 16) | (data[offset + 30] << 8) | data[offset + 31];

			// 2 bytes: Source port
			var sourcePort = (data[offset + 32] << 8) | data[offset + 33];

			// 2 bytes: Destination port
			var destinationPort = (data[offset + 34] << 8) | data[offset + 35];

			// 1 byte: Padding
			var padding = data[offset + 36];

			// 1 byte: TCP flags
			var tcpFlags = data[offset + 37];

			// 1 byte: IP protocol type, e.g., 6=TCP, 17=UDP, 1=ICMP, 2=IGMP, 89=OSPF
			var protocol = data[offset + 38];

			// 1 byte: IP type of service
			var typeOfService = data[offset + 39];

			// 2 bytes: Autonomous system number of the source, either origin or peer
			var sourceAsNumber = (data[offset + 40] << 8) | data[offset + 41];

			// 2 bytes: Autonomous system number of the destination, either origin or peer
			var destinationAsNumber = (data[offset + 42] << 8) | data[offset + 43];

			// 2 bytes: Source address prefix mask bits
			var sourcePrefixMaskBits = data[offset + 44];

			// 2 bytes: Destination address prefix mask bits
			var destinationPrefixMaskBits = data[offset + 45];

			// 2 bytes: Unused
			var unused = (data[offset + 46] << 8) | data[offset + 47];

			var netflowV5Record = new NetFlowV5Record
			{
				ClientIp = udpReceiveResult.RemoteEndPoint.Address,
				Header = header,
				DateTimeOffset = dateTime,
				SourceIp = sourceIp,
				DestinationIp = destinationIp,
				NextHopIp = nextHopIp,
				InputInterfaceIndex = inputInterfaceIndex,
				OutputInterfaceIndex = outputInterfaceIndex,
				PacketCount = packetCount,
				TotalL3Bytes = totalL3Bytes,
				SysUptime = sysUptime,
				LastPacketSysUptime = lastPacketSysUptime,
				SourcePort = sourcePort,
				DestinationPort = destinationPort,
				Padding = padding,
				TcpFlags = tcpFlags,
				Protocol = protocol,
				TypeOfService = typeOfService,
				SourceAsNumber = sourceAsNumber,
				DestinationAsNumber = destinationAsNumber,
				SourcePrefixMaskBits = sourcePrefixMaskBits,
				DestinationPrefixMaskBits = destinationPrefixMaskBits,
				Unused = unused
			};

			syslogApplication.NetflowRecordReceived(this, netflowV5Record);

			offset += 48; // Each record is 48 bytes
		}
	}

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

	public void Dispose()
	{
		// Do not change this code. Put clean-up code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
