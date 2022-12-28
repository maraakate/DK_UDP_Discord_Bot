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
    class dkserver
    {
        public string ip = String.Empty;
        public int port = 0;
        public List<string> players = new List<string>();
        public int activeplayers = 0;
        public bool bAlertChanged = false;
        public DateTime heartbeat = DateTime.UtcNow;

        public dkserver(string _ip, int _port)
        {
            ip = _ip;
            port = _port;
            Reset();
        }

        private void Reset()
        {
            players.Clear();
            activeplayers = 0;
            bAlertChanged = false;
        }
    }

    class Program
    {
        private readonly DiscordSocketClient _client;
        static List<dkserver> servers = new List<dkserver>();
        static readonly byte[] msQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff',
            (byte)'g', (byte)'e', (byte)'t', (byte)'s', (byte)'e', (byte)'r', (byte)'v', (byte)'e', (byte)'r', (byte)'s', (byte)' ',
            (byte)'d', (byte)'a', (byte)'i', (byte)'k', (byte)'a', (byte)'t', (byte)'a', (byte)'n', (byte)'a', 0};
        static readonly int msPort = 27900;
        static readonly string msAddr = "master.maraakate.org";
        static readonly byte[] serverQuery = { (byte)'\xff', (byte)'\xff', (byte)'\xff', (byte)'\xff',
            (byte)'s', (byte)'t', (byte)'a', (byte)'t', (byte)'u', (byte)'s', 0 };

        string DiscordLoginToken = string.Empty;
        ulong DiscordChannelId = 0;

        private void SendUdpMs(int srcPort)
        {
            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv;
                string dataconv;

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

                const int ipLen = 4;
                const int portLen = 2;
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
                string hostname;

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
                //Console.WriteLine("{0}", dataconv);
                hostname = dataconv.Substring(dataconv.IndexOf("\\hostname\\") + 10);
                hostname = hostname.Substring(0, hostname.IndexOf("\\"));

                if (dataconv.Contains('\n'))
                {
                    int index = dataconv.IndexOf('\n') + 1;
                    int _playerCount = 0;
                    List<string> _players = new List<string>();
                    while (true)
                    {
                        string substr = dataconv.Substring(index);
                        index = substr.IndexOf('\n') + 1;
                        substr = substr.Substring(0, index);
                        if (substr.Length == 0)
                            break;

                        int scoreLen = substr.IndexOf(" ");
                        string score = substr.Substring(0, scoreLen);
                        int pingLen = substr.IndexOf(" ", scoreLen + 1);
                        string ping = substr.Substring(scoreLen + 1, pingLen - 1);
                        int playerEnd = substr.IndexOf('\n');
                        string player = substr.Substring(pingLen + 1);
                        player = player.Substring(0, player.Length - 1);

                        //Console.Write("Player {0} joined the server \"{1}\" at {2}:{3}!\n", player, hostname, dstIp, dstPort);
                        index = dataconv.IndexOf(substr) + substr.Length;

                        _playerCount++;
                        _players.Add(player);
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
                                if (_players[i].Equals(server.players[j]))
                                {
                                    bFound = true;
                                    break;
                                }
                            }

                            if (bFound == false)
                            {
                                string str = String.Format("Player {0} joined the server \"{1}\" at {2}:{3}!\n", _players[i], hostname, dstIp, dstPort);
                                Console.Write(str);
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

        private void CleanUpDeadServers ()
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
            _client = new DiscordSocketClient(new DiscordSocketConfig() { LogLevel = LogSeverity.Verbose, WebSocketProvider = WS4NetProvider.Instance });

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.ReactionAdded += ReactionAddedAsync;
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

        // This is not the recommended way to write a bot - consider
        // reading over the Commands Framework sample.
        private async Task MessageReceivedAsync(SocketMessage message)
        {
        }

        public async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2, SocketReaction arg3)
        {
        }

        private void ReadConfig ()
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

            ReadConfig();

            // Tokens should be considered secret data, and never hard-coded.
            await _client.LoginAsync(TokenType.Bot, DiscordLoginToken);
            await _client.StartAsync();
            await _client.SetGameAsync("Daikatana");

            // Block the program until it is closed.
            while (true)
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
