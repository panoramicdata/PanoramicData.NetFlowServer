using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PanoramicData.NetFlowServer.Config;
using System.Net;
using System.Net.Sockets;

namespace PanoramicData.NetFlowServer.Test;

/// <summary>
/// A running <see cref="NetFlowServer"/> listening on a free port, with a client that can send
/// packets to it. Tests exercise the parser the way real traffic does, over the wire.
/// </summary>
internal sealed class NetFlowServerFixture : IAsyncDisposable
{
	private readonly UdpClient _client = new();
	private readonly int _port;

	private NetFlowServerFixture(NetFlowServer server, RecordingNetFlowApplication application, int port)
	{
		Server = server;
		Application = application;
		_port = port;
	}

	public NetFlowServer Server { get; }

	public RecordingNetFlowApplication Application { get; }

	public static async Task<NetFlowServerFixture> StartAsync(CancellationToken cancellationToken)
	{
		var port = GetFreeUdpPort();
		var application = new RecordingNetFlowApplication();
		var server = CreateServer(port, application);

		await server.StartAsync(cancellationToken);

		return new NetFlowServerFixture(server, application, port);
	}

	/// <summary>
	/// Creates an unstarted server on the given port.
	/// </summary>
	public static NetFlowServer CreateServer(int port, RecordingNetFlowApplication application)
		=> new(
			Options.Create(new NetFlowServerConfiguration { UdpPort = port }),
			NullLoggerFactory.Instance,
			application);

	public Task SendAsync(byte[] packet, CancellationToken cancellationToken)
		=> _client
			.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, _port), cancellationToken)
			.AsTask();

	/// <summary>
	/// Binds port 0 to have the operating system name a port that is currently free, then releases it.
	/// </summary>
	public static int GetFreeUdpPort()
	{
		using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
		return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
	}

	public async ValueTask DisposeAsync()
	{
		await Server.StopAsync(CancellationToken.None);

		Server.Dispose();
		_client.Dispose();
		Application.Dispose();
	}
}
