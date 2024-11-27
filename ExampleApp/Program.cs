using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PanoramicData.NetFlowServer;
using PanoramicData.NetFlowServer.Config;
using PanoramicData.NetFlowServer.Interfaces;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp;

partial class Program
{
	static async Task Main()
	{
		var cancellationTokenSource = new CancellationTokenSource();
		await Host.CreateDefaultBuilder()
			.ConfigureServices((hostBuilderContext, serviceCollection) =>
			{
				serviceCollection
					.AddOptions()
					.Configure<NetFlowServerConfiguration>(hostBuilderContext.Configuration.GetSection("NetFlowServer"))
					.Configure<ExampleNetFlowApplicationConfiguration>(hostBuilderContext.Configuration.GetSection("Application"))

					// Register services
					.AddSingleton<IHostedService, NetFlowServer>()
					.AddSingleton<INetFlowApplication, ExampleNetFlowApplication>();
			})
			.UseSerilog((context, _, loggerConfiguration)
				=> loggerConfiguration
					.ReadFrom.Configuration(context.Configuration)
					.Enrich.FromLogContext()
			)
			.Build()
			.StartAsync(cancellationTokenSource.Token);

		// Wait for Ctrl+C
		Console.CancelKeyPress += (_, e) =>
		{
			e.Cancel = true;
			cancellationTokenSource.Cancel();
		};

		await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
	}
}
