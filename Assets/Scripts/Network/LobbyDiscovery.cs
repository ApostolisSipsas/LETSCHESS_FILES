using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;


public static class LobbyDiscovery
{
    private const int UDP_PORT        = 8008;  
    private const int DISCOVER_PORT   = 8009;  
    public  const int GAME_CLIENT_PORT = 8010; 
    private const string MAGIC        = "CHESS_HOST";
    private const string DISCOVER     = "CHESS_DISCOVER";

    // Πλευρά host
    private static Thread broadcastThread;
    private static Thread responderThread;
    private static UdpClient responderClient;
    private static volatile bool isBroadcasting = false;

    //client
    private static Thread listenThread;
    private static Thread proberThread;
    private static UdpClient listenClient;
    private static UdpClient proberClient;
    private static volatile bool isListening = false;

    private static readonly object lockObj = new object();
    private static readonly Dictionary<string, HostEntry> discovered = new Dictionary<string, HostEntry>();
    public static event Action OnHostListChanged;
    // HOST
    public static void StartBroadcasting(string hostName, ushort gamePort, string pin = "")
    {
        StopBroadcasting();
        isBroadcasting = true;

        broadcastThread = new Thread(() => BroadcastLoop(hostName, gamePort, pin));
        broadcastThread.IsBackground = true;
        broadcastThread.Start();

        responderThread = new Thread(() => ResponderLoop(hostName, gamePort, pin));
        responderThread.IsBackground = true;
        responderThread.Start();
    }

    public static void StopBroadcasting()
    {
        isBroadcasting = false;
        broadcastThread?.Join(500);
        broadcastThread = null;
        try { responderClient?.Close(); } catch { }
        responderThread?.Join(500);
        responderThread = null;
    }

    // broadcast 
    private static void BroadcastLoop(string hostName, ushort gamePort, string pin)
    {
        string localIP = GetLocalIP();
        string msg = $"{MAGIC}|{hostName}|{localIP}|{gamePort}|{pin}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        using UdpClient sender = new UdpClient();
        sender.EnableBroadcast = true;

        IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, UDP_PORT);
        IPEndPoint localhostEP = new IPEndPoint(IPAddress.Loopback, UDP_PORT);
        IPEndPoint subnetEP    = GetSubnetEP(localIP, UDP_PORT);

        while (isBroadcasting)
        {
            try
            {
                sender.Send(data, data.Length, broadcastEP);
                sender.Send(data, data.Length, localhostEP);
                if (subnetEP != null) sender.Send(data, data.Length, subnetEP);
            }
            catch { }
            Thread.Sleep(2000);
        }
    }

    private static void ResponderLoop(string hostName, ushort gamePort, string pin)
    {
        try
        {
            responderClient = new UdpClient();
            responderClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            responderClient.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVER_PORT));
            responderClient.Client.ReceiveTimeout = 1000;

            string localIP = GetLocalIP();
            string response = $"{MAGIC}|{hostName}|{localIP}|{gamePort}|{pin}";
            byte[] responseData = Encoding.UTF8.GetBytes(response);

            while (isBroadcasting)
            {
                try
                {
                    IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] req = responderClient.Receive(ref clientEP);
                    if (Encoding.UTF8.GetString(req) == DISCOVER)
                    {
                        // 1. Send lobby info back to client
                        responderClient.Send(responseData, responseData.Length, clientEP);
                        PrePunchGamePort(gamePort, clientEP.Address);
                    }
                }
                catch (SocketException) { }
            }
        }
        catch { }
        finally { try { responderClient?.Close(); } catch { } }
    }

    private static void PrePunchGamePort(ushort gamePort, IPAddress clientIP)
    {
        try
        {
            using Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Any, gamePort));
            s.SendTo(new byte[] { 0 }, new IPEndPoint(clientIP, GAME_CLIENT_PORT));
        }
        catch { }  // Silently fails 
    }

    // ΠΛΕΥΡΑ CLIENT

    public static void StartListening()
    {
        if (isListening) return;
        lock (lockObj) discovered.Clear();
        isListening = true;

        listenThread = new Thread(ListenLoop);
        listenThread.IsBackground = true;
        listenThread.Start();

        proberThread = new Thread(ProberLoop);
        proberThread.IsBackground = true;
        proberThread.Start();
    }

    public static void StopListening()
    {
        isListening = false;
        try { listenClient?.Close(); } catch { }
        try { proberClient?.Close(); } catch { }
        listenThread?.Join(500);
        proberThread?.Join(500);
        listenThread = null;
        proberThread = null;
        lock (lockObj) discovered.Clear();
    }

    private static void ListenLoop()
    {
        try
        {
            listenClient = new UdpClient(AddressFamily.InterNetwork);
            listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));
            listenClient.Client.ReceiveTimeout = 1000;

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            while (isListening)
            {
                try
                {
                    byte[] data = listenClient.Receive(ref sender);
                    ProcessPacket(data);
                }
                catch (SocketException) { }
            }
        }
        catch { }
        finally { try { listenClient?.Close(); } catch { } }
    }

    private static void ProberLoop()
    {
        try
        {
            // Το OS επιλέγει τυχαία ephemeral θύρα 
            proberClient = new UdpClient();
            proberClient.EnableBroadcast = true;
            proberClient.Client.ReceiveTimeout = 400;

            byte[] probe = Encoding.UTF8.GetBytes(DISCOVER);

            while (isListening)
            {
                // Αποστολή probe
                try
                {
                    string localIP = GetLocalIP();
                    proberClient.Send(probe, probe.Length,
                        new IPEndPoint(IPAddress.Broadcast, DISCOVER_PORT));
                    IPEndPoint subnetEP = GetSubnetEP(localIP, DISCOVER_PORT);
                    if (subnetEP != null)
                        proberClient.Send(probe, probe.Length, subnetEP);
                }
                catch { }

                // GET unicast απαντήσεων 
                long windowEnd = DateTime.UtcNow.AddSeconds(1.5).Ticks;
                while (isListening && DateTime.UtcNow.Ticks < windowEnd)
                {
                    try
                    {
                        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = proberClient.Receive(ref ep);
                        ProcessPacket(data);
                    }
                    catch (SocketException) { }  // ReceiveTimeout 
                }

                Thread.Sleep(500);  
            }
        }
        catch { }
        finally { try { proberClient?.Close(); } catch { } }
    }

    private static void ProcessPacket(byte[] data)
    {
        try
        {
            string msg = Encoding.UTF8.GetString(data);
            if (!msg.StartsWith(MAGIC)) return;
            string[] parts = msg.Split('|');
            if (parts.Length < 4) return;

            string name = parts[1];
            string ip   = parts[2];
            ushort port = ushort.Parse(parts[3]);
            string pin  = parts.Length >= 5 ? parts[4] : "";

            bool changed = false;
            lock (lockObj)
            {
                if (!discovered.ContainsKey(ip) ||
                    discovered[ip].name != name  ||
                    discovered[ip].port != port  ||
                    discovered[ip].pin  != pin)
                {
                    discovered[ip] = new HostEntry { name = name, ip = ip, port = port, pin = pin, lastSeen = DateTime.UtcNow };
                    changed = true;
                }
                else
                {
                    discovered[ip].lastSeen = DateTime.UtcNow;
                }
            }
            if (changed)
                MainThreadDispatcher.Enqueue(() => OnHostListChanged?.Invoke());
        }
        catch { }
    }


    // ΒΟΗΘΗΤΙΚΕΣ ΜΕΘΟΔΟΙ
    private static IPEndPoint GetSubnetEP(string localIP, int port)
    {
        try
        {
            string[] parts = localIP.Split('.');
            if (parts.Length == 4)
                return new IPEndPoint(
                    IPAddress.Parse($"{parts[0]}.{parts[1]}.{parts[2]}.255"), port);
        }
        catch { }
        return null;
    }

    public static string GetLocalIP()
    {
        string udpIP = null;
        try
        {
            using Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            s.Connect("8.8.8.8", 65530);
            udpIP = ((IPEndPoint)s.LocalEndPoint).Address.ToString();
        }
        catch { }
        if (!string.IsNullOrEmpty(udpIP)
            && !udpIP.StartsWith("169.254.")   
            && !udpIP.StartsWith("172.")        
            && udpIP != "127.0.0.1")
            return udpIP;

        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;

                var t = ni.NetworkInterfaceType;
                if (t != NetworkInterfaceType.Ethernet &&
                    t != NetworkInterfaceType.Wireless80211)
                    continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = ua.Address.ToString();
                    if (!ip.StartsWith("169.254.") && ip != "127.0.0.1")
                        return ip;
                }
            }
        }
        catch { }

        return udpIP ?? "127.0.0.1";
    }

    // Επιστρέφει true αν ο host εντοπίστηκε εντός των τελευταίων maxAgeSeconds (default 6s).
    public static bool HasFreshHost(string ip, int maxAgeSeconds = 6)
    {
        lock (lockObj)
        {
            if (!discovered.ContainsKey(ip)) return false;
            return (DateTime.UtcNow - discovered[ip].lastSeen).TotalSeconds <= maxAgeSeconds;
        }
    }

    public static List<HostEntry> GetDiscoveredHosts()
    {
        lock (lockObj)
            return new List<HostEntry>(discovered.Values);
    }
}
