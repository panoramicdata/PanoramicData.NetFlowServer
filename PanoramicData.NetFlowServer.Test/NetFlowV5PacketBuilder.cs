using System.Net;

namespace PanoramicData.NetFlowServer.Test;

/// <summary>
/// Builds NetFlow v5 packets, so that tests can describe a packet rather than hand-assemble bytes.
/// </summary>
internal sealed class NetFlowV5PacketBuilder
{
	private const int HeaderLength = 24;
	private const int RecordLength = 48;

	private readonly List<NetFlowV5RecordValues> _records = [];

	public int Version { get; init; } = 5;

	public int ClientUptimeMilliseconds { get; init; }

	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

	public int ResidualNanoseconds { get; init; }

	public int SequenceNumber { get; init; }

	public byte EngineType { get; init; }

	public byte EngineId { get; init; }

	public int SamplingMode { get; init; }

	public int SamplingInterval { get; init; }

	/// <summary>
	/// The record count written into the header, when it should differ from the number of
	/// records actually appended - which is how a malformed packet is described.
	/// </summary>
	public int? DeclaredRecordCount { get; init; }

	public NetFlowV5PacketBuilder AddRecord(NetFlowV5RecordValues record)
	{
		_records.Add(record);
		return this;
	}

	public byte[] Build()
	{
		var buffer = new byte[HeaderLength + (_records.Count * RecordLength)];

		WriteUInt16(buffer, 0, Version);
		WriteUInt16(buffer, 2, DeclaredRecordCount ?? _records.Count);
		WriteInt32(buffer, 4, ClientUptimeMilliseconds);
		WriteInt32(buffer, 8, (int)Timestamp.ToUnixTimeSeconds());
		WriteInt32(buffer, 12, ResidualNanoseconds);
		WriteInt32(buffer, 16, SequenceNumber);
		buffer[20] = EngineType;
		buffer[21] = EngineId;
		WriteUInt16(buffer, 22, ((SamplingMode & 0x3) << 14) | (SamplingInterval & 0x3FFF));

		var offset = HeaderLength;
		foreach (var record in _records)
		{
			record.SourceIp.GetAddressBytes().CopyTo(buffer, offset);
			record.DestinationIp.GetAddressBytes().CopyTo(buffer, offset + 4);
			record.NextHopIp.GetAddressBytes().CopyTo(buffer, offset + 8);
			WriteUInt16(buffer, offset + 12, record.InputInterfaceIndex);
			WriteUInt16(buffer, offset + 14, record.OutputInterfaceIndex);
			WriteInt32(buffer, offset + 16, record.PacketCount);
			WriteInt32(buffer, offset + 20, record.TotalL3Bytes);
			WriteInt32(buffer, offset + 24, record.SysUptime);
			WriteInt32(buffer, offset + 28, record.LastPacketSysUptime);
			WriteUInt16(buffer, offset + 32, record.SourcePort);
			WriteUInt16(buffer, offset + 34, record.DestinationPort);
			buffer[offset + 36] = record.Padding;
			buffer[offset + 37] = record.TcpFlags;
			buffer[offset + 38] = record.Protocol;
			buffer[offset + 39] = record.TypeOfService;
			WriteUInt16(buffer, offset + 40, record.SourceAsNumber);
			WriteUInt16(buffer, offset + 42, record.DestinationAsNumber);
			buffer[offset + 44] = record.SourcePrefixMaskBits;
			buffer[offset + 45] = record.DestinationPrefixMaskBits;
			WriteUInt16(buffer, offset + 46, record.Unused);

			offset += RecordLength;
		}

		return buffer;
	}

	private static void WriteUInt16(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)((value >> 8) & 0xFF);
		buffer[offset + 1] = (byte)(value & 0xFF);
	}

	private static void WriteInt32(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)((value >> 24) & 0xFF);
		buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
		buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
		buffer[offset + 3] = (byte)(value & 0xFF);
	}
}

/// <summary>
/// The values of a single NetFlow v5 flow record, as written by <see cref="NetFlowV5PacketBuilder"/>.
/// </summary>
internal sealed class NetFlowV5RecordValues
{
	public IPAddress SourceIp { get; init; } = IPAddress.Parse("10.0.0.1");

	public IPAddress DestinationIp { get; init; } = IPAddress.Parse("10.0.0.2");

	public IPAddress NextHopIp { get; init; } = IPAddress.Parse("10.0.0.254");

	public int InputInterfaceIndex { get; init; }

	public int OutputInterfaceIndex { get; init; }

	public int PacketCount { get; init; }

	public int TotalL3Bytes { get; init; }

	public int SysUptime { get; init; }

	public int LastPacketSysUptime { get; init; }

	public int SourcePort { get; init; }

	public int DestinationPort { get; init; }

	public byte Padding { get; init; }

	public byte TcpFlags { get; init; }

	public byte Protocol { get; init; } = 6;

	public byte TypeOfService { get; init; }

	public int SourceAsNumber { get; init; }

	public int DestinationAsNumber { get; init; }

	public byte SourcePrefixMaskBits { get; init; }

	public byte DestinationPrefixMaskBits { get; init; }

	public int Unused { get; init; }
}
