using System;
using System.Collections.Generic;

[Serializable]
public class HostEntry
{
    public string name;
    public string ip;
    public ushort port;
    public string pin;
    public DateTime lastSeen;
}

// Thin wrapper — actual discovery is done by LobbyDiscovery (UDP)
public static class LobbyManager
{
    public static List<HostEntry> GetHosts()
    {
        return LobbyDiscovery.GetDiscoveredHosts();
    }
}
