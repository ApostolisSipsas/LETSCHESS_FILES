using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class NetMakeMove : NetMessage
{
    public int origX;
    public int origY;
    public int destX;
    public int destY;
    public int team;

    public NetMakeMove()
    {
        Code = OpCode.MAKE_MOVE;
    }
    public NetMakeMove(DataStreamReader reader)
    {
        Code = OpCode.MAKE_MOVE;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
        writer.WriteInt(origX);
        writer.WriteInt(origY);
        writer.WriteInt(destX);
        writer.WriteInt(destY);
        writer.WriteInt(team);
    }

    public override void Deserialize(DataStreamReader reader)
    {
        origX = reader.ReadInt();
        origY = reader.ReadInt();
        destX = reader.ReadInt();
        destY = reader.ReadInt();
        team  = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_MAKE_MOVE?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_MAKE_MOVE?.Invoke(this, cnn);
    }
}
