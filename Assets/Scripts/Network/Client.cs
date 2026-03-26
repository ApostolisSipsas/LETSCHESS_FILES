using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class Client : MonoBehaviour
{
    public static Client Instance { set; get; }

    private void Awake()
    {
        Instance = this;
        transform.SetParent(null); // πάντα στη ρίζα της σκηνής ώστε το SetActive του μενού να μην σταματά το Update()
    }

    public NetworkDriver driver;
    private NetworkConnection connection;
    private bool isActive = false;
    public bool IsActive => isActive;
    public Action connectionDropped;

    public void Init(string ip, ushort port)
    {
        transform.SetParent(null); // αποσύνδεση από το μενού ώστε η απενεργοποίησή του να μην σταματά το Update()
        var settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(disconnectTimeoutMS: 5000);
        driver = NetworkDriver.Create(settings);
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(ip, port);
        connection = driver.Connect(endpoint);
        Debug.Log("attemping to coonect to Server on" + endpoint.Address);
        isActive = true;
        RegisterToEvent();
    }

    public void ShutDown()
    {
        if (isActive)
        {
            driver.Dispose();
            isActive = false;
            connection = default(NetworkConnection);
        }
    }

    public void OnDestroy()         { ShutDown(); }
    public void OnApplicationQuit() { ShutDown(); }

    public void Update()
    {
        if (!isActive)
            return;
        driver.ScheduleUpdate().Complete();
        CheckAlive();
        UpdateMessagePump();
    }

    private void CheckAlive()
    {
        if (!connection.IsCreated && isActive)
        {
            Debug.Log("Something went wrong , lost connection to server");
            connectionDropped?.Invoke();
            ShutDown();
        }
    }

    private void UpdateMessagePump()
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = connection.PopEvent(driver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                SendToServer(new NetWelcome());
                Debug.Log("we connected");
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                NetUtility.OnData(stream, default(NetworkConnection));
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Client got Disconnected from server");
                connection = default(NetworkConnection);
                connectionDropped?.Invoke();
                ShutDown();
            }
        }
    }

    public void SendToServer(NetMessage msg)
    {
        DataStreamWriter writer;
        int result = driver.BeginSend(connection, out writer);
        if (result != 0) { Debug.LogError($"[Client.SendToServer] BeginSend failed: {result} isActive={isActive}"); return; }
        msg.Serialize(ref writer);
        driver.EndSend(writer);
    }

    private void RegisterToEvent()   { NetUtility.C_KEEP_ALIVE += OnKeepAlive; }
    private void UnRegisterToEvent() { NetUtility.C_KEEP_ALIVE -= OnKeepAlive; }
    private void OnKeepAlive(NetMessage nm) { SendToServer(nm); }
}
