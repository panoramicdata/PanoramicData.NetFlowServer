﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PanoramicData.NetFlowServer.Interfaces;
using PanoramicData.NetFlowServer.Models;

namespace ExampleApp;

internal class ExampleNetFlowApplication(
	IOptions<ExampleNetFlowApplicationConfiguration> options,
	ILogger<Program> logger) : INetFlowApplication
{
	private readonly ExampleNetFlowApplicationConfiguration _config = options.Value;

	public void NetFlowRecordReceived(object sender, NetFlowV5Record record)
		=> logger.LogInformation(
			"NetFlow message received: {Record}",
			record.ToString()
			);

}