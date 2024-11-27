using PanoramicData.NetFlowServer.Models;

namespace PanoramicData.NetFlowServer.Interfaces;

public interface INetFlowApplication
{
	void NetFlowRecordReceived(object sender, NetFlowV5Record message);
}
