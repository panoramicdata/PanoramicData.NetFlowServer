namespace PanoramicData.NetFlowServer.Config;

public class NetFlowServerConfiguration
{
	/// <summary>
	/// The address on which the SSH server should listen.
	/// Use:
	/// - an IP address
	/// - "IPv6Any" to listen dual stack
	/// - "Any" to listen on all network interfaces.
	/// </summary>
	public string LocalAddress { get; set; } = string.Empty;

	/// <summary>
	/// Whether to permit UDP and on which port, or null to disable UDP.
	/// </summary>
	public required int UdpPort { get; set; }
}
