using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace sharelib
{
    public abstract class ConnectionManager
    {
        private static ConcurrentDictionary<string, SocketConnection> requestSockets = new ConcurrentDictionary<string, SocketConnection>();

        private static ConcurrentDictionary<string, SocketConnection> tunnelSocket = new ConcurrentDictionary<string, SocketConnection>();

        public static bool RegisteTunnel(string domain, SocketConnection conn)
        {
            if (tunnelSocket.ContainsKey(domain))
            {
                return false;
            }
            else
            {

                bool result = tunnelSocket.TryAdd(domain, conn);
                if (result)
                {
                    conn.OnClose += () => tunnelSocket.TryRemove(domain, out var s);
                }

                return result;
            }
        }

        public static bool AddRequest(string remoteEnd, SocketConnection conn)
        {
            if (tunnelSocket.ContainsKey(remoteEnd))
            {
                return false;
            }
            else
            {

                bool result = requestSockets.TryAdd(remoteEnd, conn);
                if (result)
                {
                    conn.OnClose += () => requestSockets.TryRemove(remoteEnd, out var s);
                }

                return result;
            }
        }

        public static SocketConnection GetRequestSocket(string remoteEnd)
        {
            if (requestSockets.TryGetValue(remoteEnd, out var s))
            {
                return s;
            }

            return null;
        }

        public static SocketConnection GetTunnelSocket(string domain)
        {
            if (tunnelSocket.TryGetValue(domain, out var s))
            {
                return s;
            }

            return null;
        }
    }

    public abstract class ClientManager
    {
        public static string ServerDomain;
        public static int ServerPort;
        public static string ForwardHost;
        public static int ForwardPort;
        public static string SubDomain;

        public static TcpClient TunnelClient { get { return tunnelClient; } }
        public static SocketConnection TunnelConnection { get { return tunnelConnection; } }

        private static TcpClient tunnelClient;
        private static SocketConnection tunnelConnection;

        public static async Task InitailTunnel()
        {
            tunnelClient = new TcpClient();
            await tunnelClient.ConnectAsync(ServerDomain, ServerPort);
            tunnelConnection = new SocketConnection(tunnelClient.Client, null);
        }

        private static ConcurrentDictionary<string, ForwardClient> clients = new ConcurrentDictionary<string, ForwardClient>();

        public static ForwardClient GetOrAdd(string remote, ForwardClient c)
        {
            return clients.GetOrAdd(remote, c);
        }

        public static ForwardClient Get(string remote)
        {
            clients.TryGetValue(remote, out var c);
            return c;
        }

        public static void Remove(string remote)
        {
            clients.TryRemove(remote, out var c);
        }
    }
}
