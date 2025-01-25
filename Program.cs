using Discord;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DK_UDP_Bot
{
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
        readonly byte[] dkServerRespone = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'p', (byte)'r', (byte)'i', (byte)'n', (byte)'t', (byte)'\n' };
        readonly byte[] hwServerRespone = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'n' };

        ushort msPort = 27900;
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

        #region "Send UDP request to Master Server "
        private void SendDKUdpMs(ushort srcPort)
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

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, msPort);

                c.Send(dkMsQuery, dkMsQuery.Length, msAddr, msPort);
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

                    if (bAdd)
                        servers.Add(new dkserver(destAddress, portx, serverType.Daikatana));

                    readBytes += ipLen + portLen;
                }
            }
        }

        private void SendHWUdpMs(ushort srcPort)
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

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, msPort);

                c.Send(hwMsQuery, hwMsQuery.Length, msAddr, msPort);
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

                    if (bAdd)
                        servers.Add(new dkserver(destAddress, portx, serverType.HexenWorld));

                    readBytes += ipLen + portLen;
                }
            }
        }

        #endregion

        #region "Send UDP request to Game Server"

        private void SendDKUdpServerStatus(ref dkserver server, ushort srcPort, string dstIp, ushort dstPort)
        {
            if (!scanDaikatanaServers)
            {
                return;
            }

            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = 1200;

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

        private void SendHWUdpServerStatus(ref dkserver server, ushort srcPort, string dstIp, ushort dstPort)
        {
            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = 1200;

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
                                    string str = String.Format("Player {0} joined the HexenWorld server \"{1}\" at {2}:{3}!  Total Players: {4}.\n", _players[i].name, server.serverParams["hostname"], dstIp, dstPort, _playerCount);
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
            //chnl.SendMessageAsync(str);

            return Task.CompletedTask;
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
                                    msg += String.Format("**{0}**: {1}\n", x.Key, x.Value);
                                }

                                switch (server.serverType)
                                {
                                    case serverType.Daikatana:
                                        {
                                            foreach (var x in server.dkPlayers)
                                            {
                                                msg += String.Format("**Player {0}**.  Ping: {1}.  Score: {2}.\n", x.netname, x.ping, x.score);
                                            }
                                            break;
                                        }
                                    case serverType.HexenWorld:
                                        {
                                            foreach (var x in server.hwPlayers)
                                            {
                                                msg += String.Format("**Player {0}**.  Ping: {1}.  Score: {2}.\n", x.name, x.ping, x.frags);
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

            if (string.Equals(emoteRecvdStr, "daikatana_mp") == false)
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

            if (string.Equals(emoteRecvdStr, "daikatana_mp") == false)
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

            temp = ConfigurationManager.AppSettings["MasterServerPort"];
            if (!string.IsNullOrWhiteSpace(temp))
            {
                if (ushort.TryParse(temp, out msPort) == false)
                {
                    throw new Exception("Failed to convert MasterServerPort to ushort!");
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
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
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

            // Tokens should be considered secret data, and never hard-coded.
            await _client.LoginAsync(TokenType.Bot, DiscordLoginToken);
            await _client.StartAsync();
            if (!string.IsNullOrWhiteSpace(DiscordCurrentGame))
            {
                await _client.SetGameAsync(DiscordCurrentGame);
            }

            // Block the program until it is closed.
            while (!exitSystem)
            {
                try
                {
                    SendDKUdpMs(utilityQueryPort);
                    SendHWUdpMs(utilityQueryPort);
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
                                SendDKUdpServerStatus(ref server, utilityQueryPort, server.ip, server.port);
                                break;
                            case serverType.HexenWorld:
                                SendHWUdpServerStatus(ref server, utilityQueryPort, server.ip, server.port);
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
        }

        #endregion
    }
}
