using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class Server : MonoBehaviour
{
    public static Server Instance { set; get; }

    private void Awake()
    {
        Instance = this;
        transform.SetParent(null); // πάντα στη ρίζα της σκηνής ώστε το SetActive του μενού να μην σταματά το Update()
    }

    public NetworkDriver driver;
    private NativeList<NetworkConnection> connections;
    private bool isActive = false;
    public bool IsActive => isActive;
    private const float keepAliveTickRate = 20.0f;
    private float lastKeepAlive;

    public Action connectionDropped;

    public string HostName  { get; private set; }
    public ushort BoundPort { get; private set; }

    public ushort Init(ushort port, string hostName = "", string pin = "")
    {
        transform.SetParent(null); // αποσύνδεση από το μενού ώστε η απενεργοποίηση του να μην σταματά το Update()
        var settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(disconnectTimeoutMS: 5000);
        driver = NetworkDriver.Create(settings);
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);

        if (driver.Bind(endpoint) != 0)
        {
            Debug.Log("unable to Connect ");
            driver.Dispose();
            return 0;
        }

        driver.Listen();
        connections = new NativeList<NetworkConnection>(2, Allocator.Persistent);
        HostName  = hostName;
        BoundPort = port;
        isActive  = true;

        if (!string.IsNullOrEmpty(hostName))
            LobbyDiscovery.StartBroadcasting(hostName, port, pin);

        Debug.Log("listening on port " + port);
        return port;
    }

    public void ShutDown()
    {
        if (isActive)
        {
            if (!string.IsNullOrEmpty(HostName))
            {
                Broadcast(new NetLobbyRefresh());
                LobbyDiscovery.StopBroadcasting();
            }
            driver.Dispose();
            if (connections.IsCreated) connections.Dispose();
            isActive = false;
            HostName = "";
        }
    }

    public void OnDestroy()         { ShutDown(); }
    public void OnApplicationQuit() { ShutDown(); }

    public void Update()
    {
        if (!isActive)
            return;
        keepAlive();
        driver.ScheduleUpdate().Complete();
        CleanupConnections();
        AcceptNewConnections();
        UpdateMessagePump();
    }

    private void keepAlive()
    {
        if (Time.time - lastKeepAlive > keepAliveTickRate)
        {
            lastKeepAlive = Time.time;
            Broadcast(new NetKeepAlive());
        }
    }

    private void CleanupConnections()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated)
            {
                connections.RemoveAtSwapBack(i);
                --i;
            }
        }
    }

    private void AcceptNewConnections()
    {
        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection))
        {
            connections.Add(c);
        }
    }

    private void UpdateMessagePump()
    {
        DataStreamReader stream;
        for (int i = 0; i < connections.Length; i++)
        {
            NetworkEvent.Type cmd;
            while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    NetUtility.OnData(stream, connections[i], this);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("client disconnected");
                    connections[i] = default(NetworkConnection);
                    connectionDropped?.Invoke();
                    ShutDown();
                    return;
                }
            }
        }
    }

    public void SendToClient(NetworkConnection connection, NetMessage msg)
    {
        DataStreamWriter writer;
        int result = driver.BeginSend(connection, out writer);
        if (result != 0) { Debug.LogError($"[Server.SendToClient] BeginSend failed: {result}"); return; }
        msg.Serialize(ref writer);
        driver.EndSend(writer);
    }

    public void BroadcastExcept(NetMessage msg, NetworkConnection except)
    {
        if (!connections.IsCreated) return;
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated && connections[i] != except)
            {
                SendToClient(connections[i], msg);
            }
        }
    }

    public void Broadcast(NetMessage msg)
    {
        if (!connections.IsCreated) return;
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                SendToClient(connections[i], msg);
            }
        }
    }
}
