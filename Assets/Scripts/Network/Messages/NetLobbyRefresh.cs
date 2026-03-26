using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class NetLobbyRefresh : NetMessage
{
    public NetLobbyRefresh()
    {
        Code = OpCode.LOBBY_REFRESH;
    }
    public NetLobbyRefresh(DataStreamReader reader)
    {
        Code = OpCode.LOBBY_REFRESH;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
    }

    public override void Deserialize(DataStreamReader reader)
    {
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_LOBBY_REFRESH?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_LOBBY_REFRESH?.Invoke(this, cnn);
    }
}
