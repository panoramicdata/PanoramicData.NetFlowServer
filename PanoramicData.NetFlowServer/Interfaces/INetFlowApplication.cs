using PanoramicData.NetFlowServer.Models;

namespace PanoramicData.NetFlowServer.Interfaces;

public interface INetFlowApplication
{
	void NetflowRecordReceived(object sender, NetflowV5Record message);
}
