using System;
using System.Net;

namespace PanoramicData.NetFlowServer.Models;

/// <summary>
/// Represents a single NetFlow v5 flow record.
/// </summary>
public class NetFlowV5Record
{
	/// <summary>
	/// Gets the IP address of the client that sent the NetFlow packet.
	/// </summary>
	public required IPAddress ClientIp { get; init; }

	/// <summary>
	/// Gets the NetFlow v5 header associated with this record.
	/// </summary>
	public required NetFlowV5Header Header { get; init; }

	/// <summary>
	/// Gets the timestamp of the flow record.
	/// </summary>
	public required DateTimeOffset DateTimeOffset { get; init; }

	/// <summary>
	/// Gets the source IP address of the flow.
	/// </summary>
	public required IPAddress SourceIp { get; init; }

	/// <summary>
	/// Gets the destination IP address of the flow.
	/// </summary>
	public required IPAddress DestinationIp { get; init; }

	/// <summary>
	/// Gets the IP address of the next hop router.
	/// </summary>
	public required IPAddress NextHopIp { get; init; }

	/// <summary>
	/// Gets the SNMP index of the input interface.
	/// </summary>
	public required int InputInterfaceIndex { get; init; }

	/// <summary>
	/// Gets the SNMP index of the output interface.
	/// </summary>
	public required int OutputInterfaceIndex { get; init; }

	/// <summary>
	/// Gets the number of packets in the flow.
	/// </summary>
	public required int PacketCount { get; init; }

	/// <summary>
	/// Gets the total number of Layer 3 bytes in the flow.
	/// </summary>
	public required int TotalL3Bytes { get; init; }

	/// <summary>
	/// Gets the SysUptime at the start of the flow.
	/// </summary>
	public required int SysUptime { get; init; }

	/// <summary>
	/// Gets the SysUptime when the last packet of the flow was received.
	/// </summary>
	public required int LastPacketSysUptime { get; init; }

	/// <summary>
	/// Gets the source port number.
	/// </summary>
	public required int SourcePort { get; init; }

	/// <summary>
	/// Gets the destination port number.
	/// </summary>
	public required int DestinationPort { get; init; }

	/// <summary>
	/// Gets the padding byte.
	/// </summary>
	public required byte Padding { get; init; }

	/// <summary>
	/// Gets the cumulative OR of TCP flags for the flow.
	/// </summary>
	public required byte TcpFlags { get; init; }

	/// <summary>
	/// Gets the IP protocol type (e.g. 6=TCP, 17=UDP).
	/// </summary>
	public required byte Protocol { get; init; }

	/// <summary>
	/// Gets the IP type-of-service value.
	/// </summary>
	public required byte TypeOfService { get; init; }

	/// <summary>
	/// Gets the autonomous system number of the source.
	/// </summary>
	public required int SourceAsNumber { get; init; }

	/// <summary>
	/// Gets the autonomous system number of the destination.
	/// </summary>
	public required int DestinationAsNumber { get; init; }

	/// <summary>
	/// Gets the source address prefix mask bits.
	/// </summary>
	public required byte SourcePrefixMaskBits { get; init; }

	/// <summary>
	/// Gets the destination address prefix mask bits.
	/// </summary>
	public required byte DestinationPrefixMaskBits { get; init; }

	/// <summary>
	/// Gets the unused field value.
	/// </summary>
	public required int Unused { get; init; }

	/// <inheritdoc/>
	public override string ToString() =>
	$"{DateTimeOffset:yyyy-MM-dd HH:mm:ss.fff} {ClientIp} {SourceIp}:{SourcePort} -> {DestinationIp}:{DestinationPort} {Protocol} {PacketCount} packets {TotalL3Bytes} bytes";
}