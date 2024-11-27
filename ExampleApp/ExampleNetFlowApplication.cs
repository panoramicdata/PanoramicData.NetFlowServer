using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PanoramicData.NetFlowServer.Interfaces;
using PanoramicData.NetFlowServer.Models;

namespace ExampleApp;

internal class ExampleNetFlowApplication(
	IOptions<ExampleNetFlowApplicationConfiguration> options,
	ILogger<Program> logger) : INetFlowApplication
{
	private readonly ExampleNetFlowApplicationConfiguration _config = options.Value;

	public void NetflowRecordReceived(object sender, NetflowV5Record record)
		=> logger.LogInformation(
			"NetFlow message received: {Record}",
			record.ToString()
			);

}