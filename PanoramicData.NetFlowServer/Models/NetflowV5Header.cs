using System;

namespace PanoramicData.NetFlowServer.Models;
public class NetflowV5Header
{
	public int ClientUptimeMilliseconds { get; internal set; }
	public DateTimeOffset DateTimeOffset { get; internal set; }
	public int SequenceNumber { get; internal set; }
	public byte EngineType { get; internal set; }
	public byte EngineId { get; internal set; }
	public int SamplingMode { get; internal set; }
	public int SamplingInterval { get; internal set; }
}
