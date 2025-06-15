using Discord;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DK_UDP_Bot
{
    class IUserEqualityComparer : IEqualityComparer<IUser>
    {
        public bool Equals(IUser? b1, IUser? b2)
        {
            if (ReferenceEquals(b1, b2))
                return true;

            if (b2 is null || b1 is null)
                return false;

            return b1.Id == b2.Id;
        }

        public int GetHashCode(IUser box) => box.GetHashCode();
    }

    public partial class Program
    {
        readonly DiscordSocketClient _client;
        List<dkserver> servers = new List<dkserver>();
        readonly byte[] dkMsQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff',
            (byte)'g', (byte)'e', (byte)'t', (byte)'s', (byte)'e', (byte)'r', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)' ',
            (byte)'d', (byte)'a', (byte)'i', (byte)'k', (byte)'a', (byte)'t', (byte)'a', (byte)'n', (byte)'a', 0};
        readonly byte[] hwMsQuery = { (byte)'\xff', (byte)'c', (byte)'\n' };
        readonly byte[] dkMsResponse = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'s', (byte)'e', (byte)'r', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)' ' };
        readonly byte[] hwMsResponse = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'d', (byte)'\n' };

        readonly byte[] dkServerQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'s', (byte)'t', (byte)'a', (byte)'t', (byte)'u', (byte)'s', 0 };
        readonly byte[] hwServerQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'s', (byte)'t', (byte)'a', (byte)'t', (byte)'u', (byte)'s', 0 };
        readonly byte[] hexen2ServerQuery = { (byte)'\x80', (byte)'\x00', (byte)'\x00', (byte)'\x0D', (byte)'\x02', (byte)'H', (byte)'E', (byte)'X', (byte)'E', (byte)'N', (byte)'I', (byte)'I', 0 };

        readonly byte[] dkServerRespone = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'p', (byte)'r', (byte)'i', (byte)'n', (byte)'t', (byte)'\n' };
        readonly byte[] hwServerRespone = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'n' };
        readonly byte[] hexen2ServerRespone = { (byte)'\x80', (byte)'\x00', (byte)'\x00', (byte)'\x99', (byte)'\x83' }; /* FS: NOTE: 4th bit is length. */

        readonly byte[] hexen2ServerRulesQuery = { (byte)'\x80', (byte)'\x00', (byte)'\x00', (byte)'\x06', (byte)'\x04', 0 };

        ushort msUDPPort = 27900;
        ushort msTCPPort = 28900;
        string msAddr = "master.maraakate.org";
        ushort utilityQueryPort = 27912;
        int serverPingTimeout = 1200;
        int querySleep = 10;
        string DiscordLoginToken = string.Empty;
        string DiscordCurrentGame = string.Empty;
        ulong DiscordChannelId = 0;
        bool scanDaikatanaServers = false;
        bool scanHexenWorldServers = false;
        bool scanHexen2Servers = false;
        bool scanHeretic2Servers = false;

        #region "Send TCP request to GameSpy Master Server"

        private void SendHeretic2TCPMs()
        {
            if (!scanHeretic2Servers)
            {
                return;
            }

            using (TcpClient c = new TcpClient(msAddr, msTCPPort))
            {
                int len = 0;
                byte[] datarecv = new byte[4096];
                len = c.Client.Receive(datarecv);
                if (len > 0)
                {
                    string dataconv = Encoding.ASCII.GetString(datarecv).Substring(0, len);
                    string seckey = dataconv.Substring(15);
                    byte[] seckeyByte = Encoding.ASCII.GetBytes(seckey);
                    gsmalg gsm = new gsmalg();
                    byte[] secKeyOutByte = gsm.gsseckey(null, seckeyByte, Encoding.ASCII.GetBytes("2iuCAS"));
                    string secKeyOutStr = Encoding.ASCII.GetString(secKeyOutByte).Substring(0, 8);
                    string incomingTcpValidate = string.Format(@"\gamename\heretic2\validate\{0}\final\\queryid\1.1\\list\cmp\gamename\heretic2\", secKeyOutStr);
                    c.Client.Send(Encoding.ASCII.GetBytes(incomingTcpValidate));
                    len = c.Client.Receive(datarecv);
                    if (len > 0)
                    {
                        int ipLen = 4;
                        int portLen = 2;
                        int readBytes = 0;
                        string str = Encoding.ASCII.GetString(datarecv).Substring(0, len);
                        if (str.EndsWith("\\final\\"))
                        {
                            len -= 7;
                        }

                        while (true)
                        {
                            if (readBytes >= len)
                                break;

                            byte[] temp = new byte[4];
                            Array.Copy(datarecv, readBytes, temp, 0, temp.Length);

                            ushort port = BitConverter.ToUInt16(datarecv, readBytes + ipLen);
                            ushort portx = (ushort)IPAddress.HostToNetworkOrder((short)port);
                            portx -= 1;

                            IPAddress ip = new IPAddress(temp);
                            string destAddress = ip.ToString();

                            bool bAdd = true;
                            foreach (dkserver server in servers)
                            {
                                if (server.ip == destAddress && server.port == portx)
                                {
                                    bAdd = false;
                                    break;
                                }
                            }

                            foreach (BanList banned in bannedIPs)
                            {
                                if (destAddress == banned.ip && portx == banned.port)
                                {
                                    bAdd = false;
                                    break;
                                }
                            }

                            if (bAdd)
                                servers.Add(new dkserver(destAddress, portx, serverType.Heretic2));

                            readBytes += ipLen + portLen;
                        }
                    }
                    else
                    {
                        Console.Write("No list response received from master server while querying for Heretic II.\n");
                    }
                }
                else
                {
                    Console.Write("No challenge received from master server while querying for Heretic II.\n");
                }
            }
        }

        private void SendHexen2TCPMs()
        {
            if (!scanHexen2Servers)
            {
                return;
            }

            using (TcpClient c = new TcpClient(msAddr, msTCPPort))
            {
                int len = 0;
                byte[] datarecv = new byte[4096];
                len = c.Client.Receive(datarecv);
                if (len > 0)
                {
                    string dataconv = Encoding.ASCII.GetString(datarecv).Substring(0, len);
                    string seckey = dataconv.Substring(15);
                    byte[] seckeyByte = Encoding.ASCII.GetBytes(seckey);
                    gsmalg gsm = new gsmalg();
                    byte[] secKeyOutByte = gsm.gsseckey(null, seckeyByte, Encoding.ASCII.GetBytes("FAKEKEY"));
                    string secKeyOutStr = Encoding.ASCII.GetString(secKeyOutByte).Substring(0, 8);
                    string incomingTcpValidate = string.Format(@"\gamename\hexen2\validate\{0}\final\\queryid\1.1\\list\cmp\gamename\hexen2\", secKeyOutStr);
                    c.Client.Send(Encoding.ASCII.GetBytes(incomingTcpValidate));
                    len = c.Client.Receive(datarecv);
                    if (len > 0)
                    {
                        int ipLen = 4;
                        int portLen = 2;
                        int readBytes = 0;
                        string str = Encoding.ASCII.GetString(datarecv).Substring(0, len);
                        if (str.EndsWith("\\final\\"))
                        {
                            len -= 7;
                        }

                        while (true)
                        {
                            if (readBytes >= len)
                                break;

                            byte[] temp = new byte[4];
                            Array.Copy(datarecv, readBytes, temp, 0, temp.Length);

                            ushort port = BitConverter.ToUInt16(datarecv, readBytes + ipLen);
                            ushort portx = (ushort)IPAddress.HostToNetworkOrder((short)port);

                            IPAddress ip = new IPAddress(temp);
                            string destAddress = ip.ToString();

                            bool bAdd = true;
                            foreach (dkserver server in servers)
                            {
                                if (server.ip == destAddress && server.port == portx)
                                {
                                    bAdd = false;
                                    break;
                                }
                            }

                            foreach (BanList banned in bannedIPs)
                            {
                                if (destAddress == banned.ip && portx == banned.port)
                                {
                                    bAdd = false;
                                    break;
                                }
                            }

                            if (bAdd)
                                servers.Add(new dkserver(destAddress, portx, serverType.Hexen2));

                            readBytes += ipLen + portLen;
                        }
                    }
                    else
                    {
                        Console.Write("No list response received from master server while querying for Hexen II.\n");
                    }
                }
                else
                {
                    Console.Write("No challenge received from master server while querying for Hexen II.\n");
                }
            }
        }

        #endregion

        #region "Send UDP request to Master Server"

        private void SendDaikatanaKUDPMs(ushort srcPort)
        {
            if (!scanDaikatanaServers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                const int ipLen = 4;
                const int portLen = 2;

                c.Client.ReceiveTimeout = serverPingTimeout;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, msUDPPort);

                c.Send(dkMsQuery, dkMsQuery.Length, msAddr, msUDPPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (!MemCmp(datarecv, dkMsResponse, dkMsResponse.Length))
                {
                    return;
                }

                int readBytes = dkMsResponse.Length;
                int totalLen = datarecv.Length;

                while (true)
                {
                    if (readBytes >= totalLen)
                        break;

                    byte[] temp = new byte[4];
                    Array.Copy(datarecv, readBytes, temp, 0, temp.Length);

                    ushort port = BitConverter.ToUInt16(datarecv, readBytes + ipLen);
                    ushort portx = (ushort)IPAddress.HostToNetworkOrder((short)port);
                    portx += 10;

                    IPAddress ip = new IPAddress(temp);
                    string destAddress = ip.ToString();

                    bool bAdd = true;
                    foreach (dkserver server in servers)
                    {
                        if (server.ip == destAddress && server.port == portx)
                        {
                            bAdd = false;
                            break;
                        }
                    }

                    foreach (BanList banned in bannedIPs)
                    {
                        if (destAddress == banned.ip && portx == banned.port)
                        {
                            bAdd = false;
                            break;
                        }
                    }

                    if (bAdd)
                        servers.Add(new dkserver(destAddress, portx, serverType.Daikatana));

                    readBytes += ipLen + portLen;
                }
            }
        }

        private void SendHexenWorldUDPMs(ushort srcPort)
        {
            if (!scanHexenWorldServers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                const int ipLen = 4;
                const int portLen = 2;

                c.Client.ReceiveTimeout = serverPingTimeout;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, msUDPPort);

                c.Send(hwMsQuery, hwMsQuery.Length, msAddr, msUDPPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (!MemCmp(datarecv, hwMsResponse, hwMsResponse.Length))
                {
                    return;
                }

                int readBytes = hwMsResponse.Length;
                int totalLen = datarecv.Length;

                while (true)
                {
                    if (readBytes >= totalLen)
                        break;

                    byte[] temp = new byte[4];
                    Array.Copy(datarecv, readBytes, temp, 0, temp.Length);

                    ushort port = BitConverter.ToUInt16(datarecv, readBytes + ipLen);
                    ushort portx = (ushort)IPAddress.HostToNetworkOrder((short)port);

                    IPAddress ip = new IPAddress(temp);
                    string destAddress = ip.ToString();

                    bool bAdd = true;
                    foreach (dkserver server in servers)
                    {
                        if (server.ip == destAddress && server.port == portx)
                        {
                            bAdd = false;
                            break;
                        }
                    }

                    foreach (BanList banned in bannedIPs)
                    {
                        if (destAddress == banned.ip && portx == banned.port)
                        {
                            bAdd = false;
                            break;
                        }
                    }

                    if (bAdd)
                        servers.Add(new dkserver(destAddress, portx, serverType.HexenWorld));

                    readBytes += ipLen + portLen;
                }
            }
        }

        #endregion

        #region "Send UDP request to Game Server"

        private void SendDaikatanaUDPServerStatus(ref dkserver server, ushort srcPort, string dstIp, ushort dstPort)
        {
            if (!scanDaikatanaServers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = serverPingTimeout;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, dstPort);

                c.Send(dkServerQuery, dkServerQuery.Length, dstIp, dstPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (!MemCmp(datarecv, dkServerRespone, dkServerRespone.Length))
                {
                    return;
                }

                dataconv = Encoding.ASCII.GetString(datarecv).Substring(dkServerRespone.Length);

                Dictionary<string, string> _serverParams = new Dictionary<string, string>();
                var dict = dataconv.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                int totalLen = dict.Length;
                for (int x = 0; x < totalLen; x += 2)
                {
                    if (x + 1 > totalLen)
                        break;

                    if (dict[x + 1].Contains("\n"))
                    {
                        int trim = dict[x + 1].IndexOf('\n');
                        string tempVal = dict[x + 1].Substring(0, trim);
                        if (!_serverParams.ContainsKey(dict[x]))
                        {
                            _serverParams.Add(dict[x], tempVal);
                        }
                        break;
                    }

                    if (!_serverParams.ContainsKey(dict[x])) /* FS: Older versions of DK 1.3 stupidly sent hostname and maxclients twice. */
                    {
                        _serverParams.Add(dict[x], dict[x + 1]);
                    }
                }

                server.serverParams = _serverParams;

                if (dataconv.Contains('\n'))
                {
                    int index = dataconv.IndexOf('\n') + 1;
                    int _playerCount = 0;
                    List<dkplayer> _players = new List<dkplayer>();
                    while (true)
                    {
                        string substr = dataconv.Substring(index);
                        index = substr.IndexOf('\n') + 1;
                        substr = substr.Substring(0, index);
                        if (substr.Length == 0)
                            break;

                        int scoreLen = substr.IndexOf(" ");
                        string scoreStr = substr.Substring(0, scoreLen);
                        int pingLen = substr.IndexOf(" ", scoreLen + 1);

                        string pingStr = substr.Substring(scoreLen + 1, pingLen - 1);
                        int playerEnd = substr.IndexOf('\n');

                        string player = substr.Substring(pingLen + 1);
                        player = player.Substring(0, player.Length - 1);

                        index = dataconv.IndexOf(substr) + substr.Length;

                        int score, ping;

                        int.TryParse(scoreStr, out score);
                        int.TryParse(pingStr, out ping);

                        _playerCount++;
                        _players.Add(new dkplayer(score, ping, player));
                    }

                    try
                    {
                        int playersSize = server.dkPlayers.Count;
                        int _playersSize = _players.Count;

                        for (int i = 0; i < _playersSize; i++)
                        {
                            bool bFound = false;

                            for (int j = 0; j < playersSize; j++)
                            {
                                if (_players[i].netname.Equals(server.dkPlayers[j].netname))
                                {
                                    bFound = true;
                                    break;
                                }
                            }

                            if ((bFound == false)
                                && (string.Equals(_players[i].netname, "\"WallFly[BZZZ]\"", StringComparison.OrdinalIgnoreCase) == false))
                            {
                                try
                                {
                                    string str = String.Format("Player {0} joined the Daikatana server \"{1}\" at {2}:{3}!  Total Players: {4}.\n", _players[i].netname, server.serverParams["hostname"], dstIp, dstPort, _playerCount);
                                    var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                                    chnl.SendMessageAsync(str);
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("Failed to send player joined message from Daikatana Server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Failed to parse player data from Daikatana server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                    }

                    if (server.activeplayers > 0 && _playerCount <= 0)
                    {
                        try
                        {
                            string str = String.Format("Daikatana server \"{0}\" at {1}:{2} is now empty.\n", server.serverParams["hostname"], dstIp, dstPort);
                            var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                            chnl.SendMessageAsync(str);
                        }
                        catch (Exception ex)
                        {
                            Console.Write("Failed to send server now empty message from Daikatana server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                        }
                    }
                    server.activeplayers = _playerCount;
                    server.dkPlayers = _players;
                }
                else
                {
                    server.activeplayers = 0;
                    server.dkPlayers.Clear();
                }

                server.heartbeat = DateTime.UtcNow;
            }
        }

        private void SendHeretic2UDPServerStatus(ref dkserver server, ushort srcPort, string dstIp, ushort dstPort)
        {
            if (!scanHeretic2Servers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = serverPingTimeout;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, dstPort);

                c.Send(dkServerQuery, dkServerQuery.Length, dstIp, dstPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (!MemCmp(datarecv, dkServerRespone, dkServerRespone.Length))
                {
                    return;
                }

                dataconv = Encoding.ASCII.GetString(datarecv).Substring(dkServerRespone.Length);

                Dictionary<string, string> _serverParams = new Dictionary<string, string>();
                var dict = dataconv.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                int totalLen = dict.Length;
                for (int x = 0; x < totalLen; x += 2)
                {
                    if (x + 1 > totalLen)
                        break;

                    if (dict[x + 1].Contains("\n"))
                    {
                        int trim = dict[x + 1].IndexOf('\n');
                        string tempVal = dict[x + 1].Substring(0, trim);
                        if (!_serverParams.ContainsKey(dict[x]))
                        {
                            _serverParams.Add(dict[x], tempVal);
                        }
                        break;
                    }

                    if (!_serverParams.ContainsKey(dict[x])) /* FS: Older versions of DK 1.3 stupidly sent hostname and maxclients twice. */
                    {
                        _serverParams.Add(dict[x], dict[x + 1]);
                    }
                }

                server.serverParams = _serverParams;

                if (dataconv.Contains('\n'))
                {
                    int index = dataconv.IndexOf('\n') + 1;
                    int _playerCount = 0;
                    List<dkplayer> _players = new List<dkplayer>();
                    while (true)
                    {
                        string substr = dataconv.Substring(index);
                        index = substr.IndexOf('\n') + 1;
                        substr = substr.Substring(0, index);
                        if (substr.Length == 0)
                            break;

                        int scoreLen = substr.IndexOf(" ");
                        string scoreStr = substr.Substring(0, scoreLen);
                        int pingLen = substr.IndexOf(" ", scoreLen + 1);

                        string pingStr = substr.Substring(scoreLen + 1, pingLen - 1);
                        int playerEnd = substr.IndexOf('\n');

                        string player = substr.Substring(pingLen + 1);
                        player = player.Substring(0, player.Length - 1);

                        index = dataconv.IndexOf(substr) + substr.Length;

                        int score, ping;

                        int.TryParse(scoreStr, out score);
                        int.TryParse(pingStr, out ping);

                        _playerCount++;
                        _players.Add(new dkplayer(score, ping, player));
                    }

                    try
                    {
                        int playersSize = server.dkPlayers.Count;
                        int _playersSize = _players.Count;

                        for (int i = 0; i < _playersSize; i++)
                        {
                            bool bFound = false;

                            for (int j = 0; j < playersSize; j++)
                            {
                                if (_players[i].netname.Equals(server.dkPlayers[j].netname))
                                {
                                    bFound = true;
                                    break;
                                }
                            }

                            if ((bFound == false)
                                && (string.Equals(_players[i].netname, "\"WallFly[BZZZ]\"", StringComparison.OrdinalIgnoreCase) == false))
                            {
                                try
                                {
                                    string str = String.Format("Player \"{0}\" joined the Heretic II server \"{1}\" at {2}:{3}!  Total Players: {4}.\n", _players[i].netname, server.serverParams["hostname"], dstIp, dstPort, _playerCount);
                                    var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                                    chnl.SendMessageAsync(str);
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("Failed to send player joined message from Heretic II Server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Failed to parse player data from Heretic II server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                    }

                    if (server.activeplayers > 0 && _playerCount <= 0)
                    {
                        try
                        {
                            string str = String.Format("Heretic II server \"{0}\" at {1}:{2} is now empty.\n", server.serverParams["hostname"], dstIp, dstPort);
                            var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                            chnl.SendMessageAsync(str);
                        }
                        catch (Exception ex)
                        {
                            Console.Write("Failed to send server now empty message from Daikatana server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                        }
                    }
                    server.activeplayers = _playerCount;
                    server.dkPlayers = _players;
                }
                else
                {
                    server.activeplayers = 0;
                    server.dkPlayers.Clear();
                }

                server.heartbeat = DateTime.UtcNow;
            }
        }

        private void SendHexenWorldUDPServerStatus(ref dkserver server, ushort srcPort, string dstIp, ushort dstPort)
        {
            if (!scanHexenWorldServers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = serverPingTimeout;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, dstPort);

                c.Send(hwServerQuery, hwServerQuery.Length, dstIp, dstPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (!MemCmp(datarecv, hwServerRespone, hwServerRespone.Length))
                {
                    return;
                }

                dataconv = Encoding.ASCII.GetString(datarecv).Substring(hwServerRespone.Length);

                Dictionary<string, string> _serverParams = new Dictionary<string, string>();
                var dict = dataconv.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                int totalLen = dict.Length;
                for (int x = 0; x < totalLen; x += 2)
                {
                    if (x + 1 > totalLen)
                        break;

                    if (dict[x + 1].Contains("\n"))
                    {
                        int trim = dict[x + 1].IndexOf('\n');
                        string tempVal = dict[x + 1].Substring(0, trim);
                        if (!_serverParams.ContainsKey(dict[x]))
                        {
                            _serverParams.Add(dict[x], tempVal);
                        }
                        break;
                    }

                    if (!_serverParams.ContainsKey(dict[x])) /* FS: Older versions of DK 1.3 stupidly sent hostname and maxclients twice. */
                    {
                        _serverParams.Add(dict[x], dict[x + 1]);
                    }
                }

                server.serverParams = _serverParams;

                if (dataconv.Contains('\n'))
                {
                    int index = dataconv.IndexOf('\n') + 1;
                    int _playerCount = 0;
                    List<hwplayer> _players = new List<hwplayer>();
                    while (true)
                    {
                        int userid, frags, connectTime, ping, top, bottom;
                        string name, skin;
                        string substr = dataconv.Substring(index);
                        index = substr.IndexOf('\n') + 1;
                        substr = substr.Substring(0, index);
                        if (substr.Length == 0)
                            break;

                        string[] tok = substr.Split(' ', StringSplitOptions.None);
                        if (tok.Length != 8)
                            break;

                        int.TryParse(tok[0], out userid);
                        int.TryParse(tok[1], out frags);
                        int.TryParse(tok[2], out connectTime);
                        int.TryParse(tok[3], out ping);
                        try
                        {
                            name = tok[4].Split('\"', StringSplitOptions.RemoveEmptyEntries)[0];
                        }
                        catch
                        {
                            name = "Player";
                        }

                        try
                        {
                            skin = tok[5].Split('\"', StringSplitOptions.RemoveEmptyEntries)[0];
                        }
                        catch
                        {
                            skin = "";
                        }
                        int.TryParse(tok[6], out top);
                        int.TryParse(tok[7], out bottom);

                        _playerCount++;
                        index = dataconv.IndexOf(substr) + substr.Length;

                        _players.Add(new hwplayer(userid, frags, connectTime, ping, name, skin, top, bottom));
                    }

                    try
                    {
                        int playersSize = server.hwPlayers.Count;
                        int _playersSize = _players.Count;

                        for (int i = 0; i < _playersSize; i++)
                        {
                            bool bFound = false;

                            for (int j = 0; j < playersSize; j++)
                            {
                                if (_players[i].name.Equals(server.hwPlayers[j].name))
                                {
                                    bFound = true;
                                    break;
                                }
                            }

                            if ((bFound == false)
                                && (string.Equals(_players[i].name, "\"WallFly[BZZZ]\"", StringComparison.OrdinalIgnoreCase) == false))
                            {
                                /* FS: Only alert if it's not the WallFly[BZZZ] bot. */
                                try
                                {
                                    string str = String.Format("Player \"{0}\" joined the HexenWorld server \"{1}\" at {2}:{3}!  Total Players: {4}.\n", _players[i].name, server.serverParams["hostname"], dstIp, dstPort, _playerCount);
                                    var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                                    chnl.SendMessageAsync(str);
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("Failed to send player joined message from HexenWorld server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Failed to parse player data from HexenWorld server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                    }

                    if (server.activeplayers > 0 && _playerCount <= 0)
                    {
                        try
                        {
                            string str = String.Format("HexenWorld server \"{0}\" at {1}:{2} is now empty.\n", server.serverParams["hostname"], dstIp, dstPort);
                            var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                            chnl.SendMessageAsync(str);
                        }
                        catch (Exception ex)
                        {
                            Console.Write("Failed to send server now empty message from HexenWorld server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                        }
                    }
                    server.activeplayers = _playerCount;
                    server.hwPlayers = _players;
                }
                else
                {
                    server.activeplayers = 0;
                    server.hwPlayers.Clear();
                }

                server.heartbeat = DateTime.UtcNow;
            }
        }

        private void SendHexen2UDPServerStatus(ref dkserver server, ushort srcPort, string dstIp, ushort dstPort)
        {
            if (!scanHexen2Servers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = serverPingTimeout;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, dstPort);

                c.Send(hexen2ServerQuery, hexen2ServerQuery.Length, dstIp, dstPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if ((datarecv[0] != (byte)hexen2ServerRespone[0])
                    && (datarecv[1] != (byte)hexen2ServerRespone[1])
                    && (datarecv[2] != (byte)hexen2ServerRespone[2])
                    && (datarecv.Length != (byte)datarecv[3])
                    && (datarecv[4] != (byte)hexen2ServerRespone[4]))
                {
                    return;
                }

                dataconv = Encoding.ASCII.GetString(datarecv).Substring(hexen2ServerRespone.Length);

                Dictionary<string, string> _serverParams = new Dictionary<string, string>();
                var dict = dataconv.Split(new[] { '\0' }, StringSplitOptions.None);

                _serverParams["hostname"] = dict[1];
                _serverParams["gamemamp"] = dict[2];
                if (string.IsNullOrWhiteSpace(dict[3]))
                {
                    byte[] remaining = Encoding.ASCII.GetBytes(dict[4]);
                    //server.activeplayers = 0; /* FS: Don't modify it here because we scan cache player changes below. */
                    _serverParams["maxplayers"] = remaining[0].ToString();
                    _serverParams["protocol"] = remaining[1].ToString();
                }
                else
                {
                    byte[] remaining = Encoding.ASCII.GetBytes(dict[3]);
                    //server.activeplayers = remaining[0]; /* FS: Don't modify it here because we scan cache player changes below. */
                    _serverParams["maxplayers"] = remaining[1].ToString();
                    _serverParams["protocol"] = remaining[2].ToString();
                }

                int phase = 0;
                byte[] header = { (byte)'\x80', (byte)'\x00', (byte)'\x00', (byte)'\x06', (byte)'\x04', 0 };
                byte[] buffer = new byte[4096];
                int buffLen = 0;

                System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
                buffLen = header.Length;

                while (true)
                {
                    try
                    {
                        if (phase == 0)
                        {
                            c.Send(hexen2ServerRulesQuery, hexen2ServerRulesQuery.Length, dstIp, dstPort);
                        }
                        else
                        {
                            c.Send(buffer, buffLen, dstIp, dstPort);
                        }
                        datarecv = c.Receive(ref RemoteIpEndPoint);
                        if (datarecv[0] == '\x80' && datarecv[1] == '\x00' && datarecv[2] == '\x00' && datarecv[4] == '\x85')
                        {
                            int end = 0;

                            dataconv = Encoding.ASCII.GetString(datarecv);
                            dataconv = dataconv.Substring(5);

                            string[] kvp = dataconv.Split('\0');
                            if (!string.IsNullOrWhiteSpace(kvp[0]))
                            {
                                _serverParams.Add(kvp[0], kvp[1]);
                                System.Buffer.BlockCopy(Encoding.ASCII.GetBytes(kvp[0]), 0, buffer, 5, kvp[0].Length);
                                end = header.Length + kvp[0].Length;
                                buffLen = end;
                                buffer[3] = (byte)end;
                                buffer[end - 1] = 0;
                            }
                            else
                            {
                                break; /* FS: Reached the end. */
                            }
                        }

                        phase++;
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Exception when reading CCREQ_RULE_INFO for Hexen 2.  Reason: {0}.\n", ex.Message);
                        break;
                    }
                }

                server.serverParams = _serverParams;

                header[3] = (byte)'\x06';
                header[4] = (byte)'\x03';

                int _playerCount = 0;
                List<hwplayer> _players = new List<hwplayer>();
                while (true)
                {
                    try
                    {
                        c.Send(header, header.Length, dstIp, dstPort);
                        datarecv = c.Receive(ref RemoteIpEndPoint);
                        if (datarecv[0] == '\x80' && datarecv[1] == '\x00' && datarecv[2] == '\x00' && datarecv[4] == '\x84')
                        {
                            hwplayer player = new hwplayer();
                            string[] temp;
                            int offset = 0;

                            //MSG_WriteLong(&net_message, 0);
                            //MSG_WriteByte(&net_message, CCREP_PLAYER_INFO);
                            //MSG_WriteByte(&net_message, playerNumber);
                            //MSG_WriteString(&net_message, client->name);
                            //MSG_WriteLong(&net_message, client->colors);
                            //MSG_WriteLong(&net_message, (int)client->edict->v.frags);
                            //MSG_WriteLong(&net_message, (int)(net_time - client->netconnection->connecttime));
                            //MSG_WriteString(&net_message, client->netconnection->address);

                            int colors = 0;
                            offset = header.Length - 1;
                            player.userid = datarecv[offset];
                            offset++;
                            dataconv = Encoding.ASCII.GetString(datarecv);
                            dataconv = dataconv.Substring(offset);
                            temp = dataconv.Split('\0');
                            player.name = temp[0];
                            offset += temp[0].Length + 1;
                            colors = BitConverter.ToInt32(new byte[] { datarecv[offset], datarecv[offset + 1], datarecv[offset + 2], datarecv[offset + 3] });
                            player.top = (hwSkinColour)(colors >> 4);
                            player.bottom = (hwSkinColour)(colors & 0x0F);
                            offset += 4;
                            player.frags = BitConverter.ToInt32(new byte[] { datarecv[offset], datarecv[offset + 1], datarecv[offset + 2], datarecv[offset + 3] });
                            offset += 4;
                            player.connectTime = new TimeSpan(0, 0, BitConverter.ToInt32(new byte[] { datarecv[offset], datarecv[offset + 1], datarecv[offset + 2], datarecv[offset + 3] }));
                            offset += 4;
                            //dataconv = Encoding.ASCII.GetString(datarecv).Substring(offset); /* FS: IP address. */

                            _playerCount++;

                            _players.Add(player);
                            header[5] = (byte)(player.userid + 1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }

                try
                {
                    int playersSize = server.hwPlayers.Count;
                    int _playersSize = _players.Count;

                    for (int i = 0; i < _playersSize; i++)
                    {
                        bool bFound = false;

                        for (int j = 0; j < playersSize; j++)
                        {
                            if (_players[i].name.Equals(server.hwPlayers[j].name))
                            {
                                bFound = true;
                                break;
                            }
                        }

                        if ((bFound == false)
                            && (string.Equals(_players[i].name, "\"WallFly[BZZZ]\"", StringComparison.OrdinalIgnoreCase) == false))
                        {
                            /* FS: Only alert if it's not the WallFly[BZZZ] bot. */
                            try
                            {
                                string str = String.Format("Player \"{0}\" joined the Hexen 2 server \"{1}\" at {2}:{3}!  Total Players: {4}.\n", _players[i].name, server.serverParams["hostname"], dstIp, dstPort, _playerCount);
                                var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                                chnl.SendMessageAsync(str);
                            }
                            catch (Exception ex)
                            {
                                Console.Write("Failed to send player joined message from Hexen 2 server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Write("Failed to parse player data from Hexen 2 server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                }

                if (server.activeplayers > 0 && _playerCount <= 0)
                {
                    try
                    {
                        string str = String.Format("Hexen 2 server \"{0}\" at {1}:{2} is now empty.\n", server.serverParams["hostname"], dstIp, dstPort);
                        var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                        chnl.SendMessageAsync(str);
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Failed to send server now empty message from Hexen 2 server {0}:{1}.  Reason: {2}.\n", dstIp, dstPort, ex.Message);
                    }
                }

                server.activeplayers = _playerCount;
                server.hwPlayers = _players;

                server.heartbeat = DateTime.UtcNow;
            }
        }

        #endregion

        #region "Discord Async Functions"

        private Task ReadyAsync()
        {
            Console.WriteLine($"{_client.CurrentUser} is connected!");

            string str = ConfigurationManager.AppSettings["DiscordReadyMessage"];
            if (String.IsNullOrWhiteSpace(str))
                return Task.CompletedTask;

            string temp = ConfigurationManager.AppSettings["DiscordReadyChannelId"];
            ulong id = 0;
            if ((ulong.TryParse(temp, out id) == false) || (id == 0))
            {
                return Task.CompletedTask;
            }

            var chnl = _client.GetChannel(id) as IMessageChannel;
            chnl.SendMessageAsync(str);

            return Task.CompletedTask;
        }

        private async void FixRoles()
        {
            int sleeptime = 1;

            while (true)
            {
                string temp = ConfigurationManager.AppSettings["DiscordGuildId"];
                ulong guildId = 0;
                if ((ulong.TryParse(temp, out guildId) == false) || (guildId == 0))
                {
                    return;
                }

                temp = ConfigurationManager.AppSettings["DiscordRoleId"];
                ulong roleId = 0;
                if ((ulong.TryParse(temp, out roleId) == false) || (roleId == 0))
                {
                    return;
                }

                var chnl = _client.GetChannel(1060241702738206802) as IMessageChannel;
                if (chnl == null)
                {
                    sleeptime = 1;
                    continue;
                }
                var message = chnl.GetMessageAsync(1060390605865365535);
                if (message == null)
                {
                    sleeptime = 1;
                    continue;
                }

                Console.Write("Fixing Roles.\n");
                sleeptime = 3600; /* FS: One hour. */

                var reactions = message.Result.Reactions;

                var guild = _client.GetGuild(guildId);
                await guild.DownloadUsersAsync();

                foreach (var emote in reactions)
                {
                    if (emote.Key.Name == "daikatana_mp")
                    {
                        var users = message.Result.GetReactionUsersAsync(emote.Key, 100000).FlattenAsync().Result;
                        foreach (var user in users)
                        {
                            try
                            {
                                var guildUser = guild.GetUser(user.Id);
                                if (guildUser != null)
                                {
                                    bool bFound = false;
                                    foreach (var role in guildUser.Roles)
                                    {
                                        if (role.Id == roleId)
                                        {
                                            bFound = true;
                                            break;
                                        }
                                    }
                                    if (bFound == false)
                                    {
                                        Console.Write("Adding role to {0}.\n", guildUser.DisplayName);
                                        await guildUser.AddRoleAsync(roleId);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Write("Failed to parsing roles from {0}.  Reason: {1}.\n", user.Username, ex.Message);
                            }
                        }

                        var guildRole = guild.GetRole(roleId);
                        IUserEqualityComparer comparator = new IUserEqualityComparer();

                        foreach (var guildUser in guild.Users)
                        {
                            if (users.Contains(guildUser, comparator) == false)
                            {
                                if (guildUser.Roles.Contains(guildRole))
                                {
                                    try
                                    {
                                        Console.Write("Removing role from {0}.\n", guildUser.DisplayName);
                                        await guildUser.RemoveRoleAsync(guildRole);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.Write("Failed to remove role {0} from {1}.  Reason: {2}.\n", guildRole.Name, guildUser.DisplayName, ex.Message);
                                    }
                                }
                            }
                        }
                    }
                }
                Thread.Sleep(1000 * sleeptime);
            }
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == _client.CurrentUser.Id)
                return;

            if (message.Content.Equals("!help", StringComparison.OrdinalIgnoreCase))
            {
                string msg = "Commands:\n";
                msg += "!info <server ip>:<server port>\n";
                msg += "!servers\n";
                msg += "!activeservers\n";

                await message.Author.SendMessageAsync(msg);
            }
            else if (message.Content.StartsWith("!info "))
            {
                if (!message.Content.Contains(':'))
                {
                    return;
                }

                var splitStr = message.Content.Substring(6).Split(':');
                string addr = splitStr[0];
                int port = 0;
                bool bSent = false;
                int.TryParse(splitStr[1], out port);

                try
                {
                    IPAddress[] addresslist = Dns.GetHostAddresses(addr);
                    if (addresslist.Length > 0)
                    {
                        addr = addresslist[0].ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.Write("Unable to convert hostname to IP for !info command.  Reason: {0}.\n", ex.Message);
                }

                foreach (dkserver server in servers)
                {
                    try
                    {
                        if (String.Equals(server.ip, addr))
                        {
                            if (server.port == port)
                            {
                                string msg = string.Empty;

                                foreach (var x in server.serverParams)
                                {
                                    if (msg.Length >= 1500)
                                    {
                                        await message.Author.SendMessageAsync(msg);
                                        msg = string.Empty;
                                    }
                                    msg += String.Format("**{0}**: {1}\n", x.Key, x.Value);
                                }

                                switch (server.serverType)
                                {
                                    case serverType.Daikatana:
                                        {
                                            foreach (var x in server.dkPlayers)
                                            {
                                                if (msg.Length >= 1500)
                                                {
                                                    await message.Author.SendMessageAsync(msg);
                                                    msg = string.Empty;
                                                }
                                                msg += String.Format("**Player {0}**.  Ping: {1}.  Score: {2}.\n", x.netname, x.ping, x.score);
                                            }
                                            break;
                                        }
                                    case serverType.HexenWorld:
                                        {
                                            foreach (var x in server.hwPlayers)
                                            {
                                                if (msg.Length >= 1500)
                                                {
                                                    await message.Author.SendMessageAsync(msg);
                                                    msg = string.Empty;
                                                }
                                                msg += String.Format("**Player {0}**.  Ping: {1}.  Score: {2}.\n", x.name, x.ping, x.frags);
                                            }
                                            break;
                                        }
                                    case serverType.Heretic2:
                                        {
                                            foreach (var x in server.dkPlayers)
                                            {
                                                if (msg.Length >= 1500)
                                                {
                                                    await message.Author.SendMessageAsync(msg);
                                                    msg = string.Empty;
                                                }
                                                msg += String.Format("**Player {0}**.  Ping: {1}.  Score: {2}.\n", x.netname, x.ping, x.score);
                                            }
                                            break;
                                        }
                                    case serverType.Hexen2:
                                        {
                                            foreach (var x in server.hwPlayers)
                                            {
                                                if (msg.Length >= 1500)
                                                {
                                                    await message.Author.SendMessageAsync(msg);
                                                    msg = string.Empty;
                                                }
                                                msg += String.Format("**Player {0}**.  Score: {1}.\n", x.name, x.frags);
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }
                                }

                                if (!string.IsNullOrWhiteSpace(msg))
                                {
                                    if (msg.Length >= 1500)
                                    {
                                        await message.Author.SendMessageAsync(msg);
                                        msg = string.Empty;
                                    }
                                    msg += string.Format("\nNOTE: stats are delayed by {0} seconds.\n", querySleep);
                                    await message.Author.SendMessageAsync(msg);
                                    bSent = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Failed to parse !info command.  Reason: {0}\n", ex.Message);
                        await message.Author.SendMessageAsync("Unable to parse serverinfo!\n");
                    }
                }

                if (bSent == false)
                {
                    await message.Author.SendMessageAsync("Server not in list!\n");
                }
            }
            else if (message.Content.Equals("!servers", StringComparison.OrdinalIgnoreCase))
            {
                string msg = string.Empty;

                foreach (var x in servers)
                {
                    try
                    {
                        if (msg.Length >= 1500)
                        {
                            await message.Author.SendMessageAsync(msg);
                            msg = string.Empty;
                        }
                        msg += string.Format("{0} server \"{1}\" at {2}:{3} with {4} active players.\n",
                        x.serverType.ToString(),
                        x.serverParams["hostname"],
                        x.ip,
                        x.port,
                        x.activeplayers);
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Failed to parse !servers message from {0} server {1}:{2}.  Reason: {3}.\n", x.serverType.ToString(), x.ip, x.port, ex.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    await message.Author.SendMessageAsync(msg);
                }
            }
            else if (message.Content.Equals("!activeservers", StringComparison.OrdinalIgnoreCase))
            {
                string msg = string.Empty;

                foreach (var x in servers)
                {
                    if (x.activeplayers > 0)
                    {
                        try
                        {
                            if (msg.Length >= 1500)
                            {
                                await message.Author.SendMessageAsync(msg);
                                msg = string.Empty;
                            }
                            msg += string.Format("{0} server \"{1}\" at {2}:{3} with {4} active players.\n",
                            x.serverType.ToString(),
                            x.serverParams["hostname"],
                            x.ip,
                            x.port,
                            x.activeplayers);
                        }
                        catch (Exception ex)
                        {
                            Console.Write("Failed to parse !activeservers message from {0} server {1}:{2}.  Reason: {3}.\n", x.serverType.ToString(), x.ip, x.port, ex.Message);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(msg))
                {
                    await message.Author.SendMessageAsync("No active servers!");
                }
                else
                {
                    await message.Author.SendMessageAsync(msg);
                }
            }
        }

        public async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        {
            var m2 = await arg1.GetOrDownloadAsync();
            var emote = arg3.Emote;
            string emoteRecvdStr = emote.Name.ToLower();

            if (arg3.UserId == _client.CurrentUser.Id)
                return;

            string temp = ConfigurationManager.AppSettings["DiscordGuildId"];
            ulong guildId = 0;
            if ((ulong.TryParse(temp, out guildId) == false) || (guildId == 0))
            {
                return;
            }

            temp = ConfigurationManager.AppSettings["DiscordRoleId"];
            ulong roleId = 0;
            if ((ulong.TryParse(temp, out roleId) == false) || (roleId == 0))
            {
                return;
            }

            if (string.Equals(emoteRecvdStr, "daikatana_mp") == false || arg2.Id != 1060241702738206802 || arg3.MessageId != 1060390605865365535)
            {
                return;
            }

            try
            {
                var guild = _client.GetGuild(guildId);
                var guildUser = guild.GetUser(arg3.UserId);
                await guildUser.AddRoleAsync(roleId);
            }
            catch (Exception ex)
            {
                Console.Write("Failed to add role to user.  Reason: {0}", ex.Message);
            }
        }

        private async Task _ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        {
            var m2 = await arg1.GetOrDownloadAsync();
            var emote = arg3.Emote;
            string emoteRecvdStr = emote.Name.ToLower();

            if (arg3.UserId == _client.CurrentUser.Id)
                return;

            string temp = ConfigurationManager.AppSettings["DiscordGuildId"];
            ulong guildId = 0;
            if ((ulong.TryParse(temp, out guildId) == false) || (guildId == 0))
            {
                return;
            }

            temp = ConfigurationManager.AppSettings["DiscordRoleId"];
            ulong roleId = 0;
            if ((ulong.TryParse(temp, out roleId) == false) || (roleId == 0))
            {
                return;
            }

            if (string.Equals(emoteRecvdStr, "daikatana_mp") == false || arg2.Id != 1060241702738206802 || arg3.MessageId != 1060390605865365535)
            {
                return;
            }

            try
            {
                var guild = _client.GetGuild(guildId);
                var guildUser = guild.GetUser(arg3.UserId);
                await guildUser.RemoveRoleAsync(roleId);
            }
            catch (Exception ex)
            {
                Console.Write("Failed to remove role from user.  Reason: {0}", ex.Message);
            }
        }

        #endregion

        #region "Maintenance Functions"

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private bool MemCmp(byte[] a, byte[] b, int count)
        {
            int aLen = a.Length;
            int bLen = b.Length;

            for (int i = 0; i < count; i++)
            {
                if (aLen <= i || bLen <= i)
                    return false;

                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private void CleanUpDeadServers()
        {
            try
            {
            start:
                foreach (dkserver server in servers)
                {
                    var offset = DateTime.UtcNow - server.heartbeat;
                    if (offset.TotalMinutes >= 5)
                    {
                        servers.Remove(server);
                        goto start;
                    }
                }
            }
            catch
            {

            }
        }

        private void ReadConfig()
        {
            DiscordLoginToken = ConfigurationManager.AppSettings["DiscordLoginToken"];
            if (String.IsNullOrWhiteSpace(DiscordLoginToken))
            {
                throw new Exception("You must set DiscordLoginToken!");
            }

            string temp = ConfigurationManager.AppSettings["DiscordChannelId"];
            if (String.IsNullOrWhiteSpace(temp))
            {
                throw new Exception("You must set DiscordChannelId!");
            }
            if (ulong.TryParse(temp, out DiscordChannelId) == false)
            {
                throw new Exception("Failed to convert DiscordChannelId to ulong!");
            }

            temp = ConfigurationManager.AppSettings["ScanDaikatanaServers"];
            if (!string.IsNullOrWhiteSpace(temp) && temp.StartsWith("1"))
            {
                scanDaikatanaServers = true;
            }

            temp = ConfigurationManager.AppSettings["ScanHexenWorldServers"];
            if (!string.IsNullOrWhiteSpace(temp) && temp.StartsWith("1"))
            {
                scanHexenWorldServers = true;
            }

            temp = ConfigurationManager.AppSettings["ScanHexen2Servers"];
            if (!string.IsNullOrWhiteSpace(temp) && temp.StartsWith("1"))
            {
                scanHexen2Servers = true;
            }
            temp = ConfigurationManager.AppSettings["ScanHeretic2Servers"];
            if (!string.IsNullOrWhiteSpace(temp) && temp.StartsWith("1"))
            {
                scanHeretic2Servers = true;
            }

            temp = ConfigurationManager.AppSettings["DiscordCurrentGame"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                DiscordCurrentGame = temp;
            }

            if (!scanDaikatanaServers && !scanHexenWorldServers && !scanHexen2Servers && !scanHeretic2Servers)
            {
                throw new Exception("You must set a server type to scan!");
            }

            temp = ConfigurationManager.AppSettings["MasterServerIP"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                msAddr = temp;
            }

            temp = ConfigurationManager.AppSettings["UtilityQueryPort"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                if (ushort.TryParse(temp, out utilityQueryPort) == false)
                {
                    throw new Exception("Failed to convert UtilityQueryPort to ushort!");
                }
            }

            temp = ConfigurationManager.AppSettings["MasterServerUDPPort"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                if (ushort.TryParse(temp, out msUDPPort) == false)
                {
                    throw new Exception("Failed to convert MasterServerUDPPort to ushort!");
                }
            }

            temp = ConfigurationManager.AppSettings["MasterServerTCPPort"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                if (ushort.TryParse(temp, out msTCPPort) == false)
                {
                    throw new Exception("Failed to convert MasterServerTCPPort to ushort!");
                }
            }

            temp = ConfigurationManager.AppSettings["ServerPingTimeout"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                if (int.TryParse(temp, out serverPingTimeout) == false)
                {
                    throw new Exception("Failed to convert ServerPingTimeout to int!");
                }
            }

            temp = ConfigurationManager.AppSettings["QuerySleep"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                if (int.TryParse(temp, out querySleep) == false)
                {
                    throw new Exception("Failed to convert QuerySleep to int!");
                }
            }

            temp = ConfigurationManager.AppSettings["BanListFileName"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                BanListFileName = temp;
            }
        }

        #endregion

        #region "Main Loop"

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public Program()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Critical,
                WebSocketProvider = WS4NetProvider.Instance,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers
            });

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.ReactionAdded += ReactionAddedAsync;
            _client.ReactionRemoved += _ReactionRemoved;
        }

        public async Task MainAsync()
        {
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            ReadConfig();
            Init_BanList();

            // Tokens should be considered secret data, and never hard-coded.
            await _client.LoginAsync(TokenType.Bot, DiscordLoginToken);
            await _client.StartAsync();
            if (!string.IsNullOrWhiteSpace(DiscordCurrentGame))
            {
                await _client.SetGameAsync(DiscordCurrentGame);
            }

            Thread fixRolesThread = new Thread(FixRoles);
            fixRolesThread.Start();

            // Block the program until it is closed.
            while (!exitSystem)
            {
                try
                {
                    SendDaikatanaKUDPMs(utilityQueryPort);
                    SendHexenWorldUDPMs(utilityQueryPort);
                    SendHeretic2TCPMs();
                    SendHexen2TCPMs();
                }
                catch (Exception ex)
                {
                    Console.Write("Main SendUdpMS loop exception.  Reason: {0}\n.", ex.Message);
                }

                int size = servers.Count;

                for (int i = 0; i < size; i++)
                {
                    try
                    {
                        dkserver server = servers[i];
                        switch (server.serverType)
                        {
                            case serverType.Daikatana:
                                SendDaikatanaUDPServerStatus(ref server, utilityQueryPort, server.ip, server.port);
                                break;
                            case serverType.Heretic2:
                                SendHeretic2UDPServerStatus(ref server, utilityQueryPort, server.ip, server.port);
                                break;
                            case serverType.HexenWorld:
                                SendHexenWorldUDPServerStatus(ref server, utilityQueryPort, server.ip, server.port);
                                break;
                            case serverType.Hexen2:
                                SendHexen2UDPServerStatus(ref server, utilityQueryPort, server.ip, server.port);
                                break;
                            default:
                                break;

                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Write("Main SendUDPServerStatus loop exception.  Reason: {0}.\n", ex.Message);
                    }
                }

                CleanUpDeadServers();
                Thread.Sleep(1000 * querySleep);
            }
            fixRolesThread.Join();
        }

        #endregion
    }
}
