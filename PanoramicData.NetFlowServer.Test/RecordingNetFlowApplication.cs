using PanoramicData.NetFlowServer.Interfaces;
using System.Collections.Concurrent;

namespace PanoramicData.NetFlowServer.Test;

/// <summary>
/// An <see cref="INetFlowApplication"/> that records what it is given, so a test can wait for
/// records to arrive rather than sleep for an arbitrary period.
/// </summary>
internal sealed class RecordingNetFlowApplication : INetFlowApplication, IDisposable
{
	private readonly ConcurrentQueue<NetFlowV5Record> _records = new();
	private readonly SemaphoreSlim _received = new(0);

	public void NetFlowRecordReceived(object sender, NetFlowV5Record message)
	{
		_records.Enqueue(message);
		_received.Release();
	}

	/// <summary>
	/// Waits for the given number of records to arrive and returns them.
	/// </summary>
	public async Task<IReadOnlyList<NetFlowV5Record>> WaitForRecordsAsync(
		int count,
		CancellationToken cancellationToken)
	{
		for (var i = 0; i < count; i++)
		{
			await _received.WaitAsync(cancellationToken);
		}

		return [.. _records];
	}

	/// <summary>
	/// Returns true if no record arrived within the given period.
	/// </summary>
	public async Task<bool> NoRecordArrivesWithinAsync(TimeSpan period)
		=> !await _received.WaitAsync(period);

	public void Dispose() => _received.Dispose();
}
