using Unity.Networking.Transport;

public class NetDraw : NetMessage
{
    // 1 = πρόταση ισοπαλίας, 0 = απόρριψη, 2 = αποδοχή
    public int wantDraw;

    public NetDraw()
    {
        Code = OpCode.DRAW;
    }
    public NetDraw(Unity.Collections.DataStreamReader reader)
    {
        Code = OpCode.DRAW;
        Deserialize(reader);
    }

    public override void Serialize(ref Unity.Collections.DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
        writer.WriteInt(wantDraw);
    }

    public override void Deserialize(Unity.Collections.DataStreamReader reader)
    {
        wantDraw = reader.ReadInt();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_DRAW?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        NetUtility.S_DRAW?.Invoke(this, cnn);
    }
}
