using System;

namespace PanoramicData.NetFlowServer.Models;

/// <summary>
/// Represents a NetFlow v5 packet header.
/// </summary>
public class NetFlowV5Header
{
	/// <summary>
	/// Gets the client device uptime in milliseconds.
	/// </summary>
	public int ClientUptimeMilliseconds { get; internal set; }

	/// <summary>
	/// Gets the timestamp of the packet.
	/// </summary>
	public DateTimeOffset DateTimeOffset { get; internal set; }

	/// <summary>
	/// Gets the sequence number of total flows seen.
	/// </summary>
	public int SequenceNumber { get; internal set; }

	/// <summary>
	/// Gets the type of flow-switching engine.
	/// </summary>
	public byte EngineType { get; internal set; }

	/// <summary>
	/// Gets the slot number of the flow-switching engine.
	/// </summary>
	public byte EngineId { get; internal set; }

	/// <summary>
	/// Gets the sampling mode.
	/// </summary>
	public int SamplingMode { get; internal set; }

	/// <summary>
	/// Gets the sampling interval.
	/// </summary>
	public int SamplingInterval { get; internal set; }
}
