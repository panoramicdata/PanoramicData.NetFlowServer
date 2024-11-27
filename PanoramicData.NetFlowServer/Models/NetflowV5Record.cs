using System;
using System.Net;

namespace PanoramicData.NetFlowServer.Models;

public class NetFlowV5Record
{
	public required IPAddress ClientIp { get; init; }
	public required NetFlowV5Header Header { get; init; }
	public required DateTimeOffset DateTimeOffset { get; init; }
	public required IPAddress SourceIp { get; init; }
	public required IPAddress DestinationIp { get; init; }
	public required IPAddress NextHopIp { get; init; }
	public required int InputInterfaceIndex { get; init; }
	public required int OutputInterfaceIndex { get; init; }
	public required int PacketCount { get; init; }
	public required int TotalL3Bytes { get; init; }
	public required int SysUptime { get; init; }
	public required int LastPacketSysUptime { get; init; }
	public required int SourcePort { get; init; }
	public required int DestinationPort { get; init; }
	public required byte Padding { get; init; }
	public required byte TcpFlags { get; init; }
	public required byte Protocol { get; init; }
	public required byte TypeOfService { get; init; }
	public required int SourceAsNumber { get; init; }
	public required int DestinationAsNumber { get; init; }
	public required byte SourcePrefixMaskBits { get; init; }
	public required byte DestinationPrefixMaskBits { get; init; }
	public required int Unused { get; init; }

	public override string ToString() =>
	$"{DateTimeOffset:yyyy-MM-dd HH:mm:ss.fff} {ClientIp} {SourceIp}:{SourcePort} -> {DestinationIp}:{DestinationPort} {Protocol} {PacketCount} packets {TotalL3Bytes} bytes";
}