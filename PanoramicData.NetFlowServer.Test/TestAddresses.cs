using System.Net;

namespace PanoramicData.NetFlowServer.Test;

/// <summary>
/// IPv4 addresses from the RFC 5737 documentation ranges, so tests never name a routable host.
/// </summary>
internal static class TestAddresses
{
	public static readonly IPAddress Source = new([192, 0, 2, 10]);

	public static readonly IPAddress Destination = new([198, 51, 100, 8]);

	public static readonly IPAddress NextHop = new([192, 0, 2, 1]);

	public static readonly IPAddress Client = new([203, 0, 113, 1]);

	public static readonly IPAddress DefaultSource = new([198, 51, 100, 1]);

	public static readonly IPAddress DefaultDestination = new([198, 51, 100, 2]);

	public static readonly IPAddress DefaultNextHop = new([198, 51, 100, 254]);
}
