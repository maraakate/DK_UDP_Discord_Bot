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
        readonly byte[] msQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff',
            (byte)'g', (byte)'e', (byte)'t', (byte)'s', (byte)'e', (byte)'r', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)' ',
            (byte)'d', (byte)'a', (byte)'i', (byte)'k', (byte)'a', (byte)'t', (byte)'a', (byte)'n', (byte)'a', 0};
        readonly int msPort = 27900;
        readonly string msAddr = "master.maraakate.org";
        readonly byte[] serverQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff',
            (byte)'s', (byte)'t', (byte)'a', (byte)'t', (byte)'u', (byte)'s', 0 };

        string DiscordLoginToken = string.Empty;
        ulong DiscordChannelId = 0;

        private void SendUdpMs(int srcPort)
        {
            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;
                const int ipLen = 4;
                const int portLen = 2;

                c.Client.ReceiveTimeout = 1200;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, msPort);

                c.Send(msQuery, msQuery.Length, msAddr, msPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (datarecv[0] != '\xff' && datarecv[1] != '\xff' && datarecv[2] != '\xff' && datarecv[3] != '\xff' && datarecv.Length < 10)
                {
                    return;
                }

                dataconv = Encoding.ASCII.GetString(datarecv).Substring(4);
                if (dataconv.StartsWith("servers ") == false)
                {
                    return;
                }

                int readBytes = 12;
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
                        servers.Add(new dkserver(destAddress, portx));

                    readBytes += ipLen + portLen;
                }
            }
        }

        private void SendUdpServerStatus(ref dkserver server, int srcPort, string dstIp, int dstPort)
        {
            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

                c.Client.ReceiveTimeout = 1200;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, dstPort);

                c.Send(serverQuery, serverQuery.Length, dstIp, dstPort);
                datarecv = c.Receive(ref RemoteIpEndPoint);
                if (datarecv[0] != '\xff' && datarecv[1] != '\xff' && datarecv[2] != '\xff' && datarecv[3] != '\xff' && datarecv.Length < 10)
                {
                    return;
                }

                dataconv = Encoding.ASCII.GetString(datarecv).Substring(4);
                if (dataconv.StartsWith("print\n") == false)
                {
                    return;
                }

                dataconv = dataconv.Substring(6);

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
                        int playersSize = server.players.Count;
                        int _playersSize = _players.Count;

                        for (int i = 0; i < _playersSize; i++)
                        {
                            bool bFound = false;

                            for (int j = 0; j < playersSize; j++)
                            {
                                if (_players[i].netname.Equals(server.players[j].netname))
                                {
                                    bFound = true;
                                    break;
                                }
                            }

                            if (bFound == false)
                            {
                                string str = String.Format("Player {0} joined the server \"{1}\" at {2}:{3}!\n", _players[i].netname, server.serverParams["hostname"], dstIp, dstPort);
                                var chnl = _client.GetChannel(DiscordChannelId) as IMessageChannel;
                                chnl.SendMessageAsync(str);
                            }
                        }
                    }
                    catch
                    {

                    }

                    server.activeplayers = _playerCount;
                    server.players = _players;
                }
                else
                {
                    server.activeplayers = 0;
                    server.players.Clear();
                }

                server.heartbeat = DateTime.UtcNow;
            }
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

        // Discord.Net heavily utilizes TAP for async, so we create
        // an asynchronous context from the beginning.
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public Program()
        {
            // It is recommended to Dispose of a client when you are finished
            // using it, at the end of your app's lifetime.
            _client = new DiscordSocketClient(new DiscordSocketConfig() { LogLevel = LogSeverity.Critical, WebSocketProvider = WS4NetProvider.Instance, GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent });

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;
            //_client.ReactionAdded += ReactionAddedAsync;
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        // The Ready event indicates that the client has opened a
        // connection and it is now safe to access the cache.
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

        //// This is not the recommended way to write a bot - consider
        //// reading over the Commands Framework sample.
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
                int.TryParse(splitStr[1], out port);

                foreach (dkserver server in servers)
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

                            foreach (var x in server.players)
                            {
                                msg += String.Format("**Player {0}**.  Ping: {1}.  Score. {2}\n", x.netname, x.ping, x.score);
                            }

                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                msg += "\nNOTE: stats are delayed by 60 seconds.\n";
                                await message.Author.SendMessageAsync(msg);
                            }
                        }
                    }
                }
            }
            else if (message.Content.Equals("!servers", StringComparison.OrdinalIgnoreCase))
            {
                string msg = string.Empty;

                foreach (var x in servers)
                {
                    msg += string.Format("\"{0}\" at {1}:{2}\n", x.serverParams["hostname"], x.ip, x.port);
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
                        msg += string.Format("\"{0}\" at {1}:{2} with {3} active players.\n", x.serverParams["hostname"], x.ip, x.port, x.activeplayers);
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

        //public async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        //{
        //}

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
        }

        public async Task MainAsync()
        {
            int intPort = 27192;

            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);

            ReadConfig();

            // Tokens should be considered secret data, and never hard-coded.
            await _client.LoginAsync(TokenType.Bot, DiscordLoginToken);
            await _client.StartAsync();
            await _client.SetGameAsync("Daikatana");

            // Block the program until it is closed.
            while (!exitSystem)
            {
                try
                {
                    SendUdpMs(intPort);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                int size = servers.Count;

                for (int i = 0; i < size; i++)
                {
                    try
                    {
                        dkserver server = servers[i];
                        SendUdpServerStatus(ref server, intPort, server.ip, server.port);
                    }
                    catch
                    {
                    }
                }

                CleanUpDeadServers();
                Thread.Sleep(1000 * 60);
            }
        }
    }
}
