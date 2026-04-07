using PanoramicData.NetFlowServer.Models;

namespace PanoramicData.NetFlowServer.Interfaces;

/// <summary>
/// Interface for applications that process received NetFlow records.
/// </summary>
public interface INetFlowApplication
{
	/// <summary>
	/// Called when a NetFlow record is received.
	/// </summary>
	/// <param name="sender">The source of the event.</param>
	/// <param name="message">The received NetFlow v5 record.</param>
	void NetFlowRecordReceived(object sender, NetFlowV5Record message);
}
