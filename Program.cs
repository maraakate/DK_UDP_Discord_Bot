using Discord;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
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

        public dkserver(string _ip, int _port)
        {
            ip = _ip;
            port = _port;
            Reset();
        }

        private void Reset ()
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

        private void SendUdp(ref dkserver server, int srcPort, string dstIp, int dstPort, byte[] data)
        {
            using (UdpClient c = new UdpClient(srcPort))
            {
                byte[] datarecv = new byte[8192];
                string dataconv;
                string hostname;

                c.Client.ReceiveTimeout = 1200;

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, dstPort);

                c.Send(data, data.Length, dstIp, dstPort);
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
                                ulong id = 949387590119862322;
                                var chnl = _client.GetChannel(id) as IMessageChannel;
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
            }
        }

        static void InitServers ()
        {
            for (int i = 27991; i < 28000; i++)
            {
                servers.Add(new dkserver("maraakate.org", i));
            }

            servers.Add(new dkserver("152.67.136.171", 27992));
            servers.Add(new dkserver("dkserver.daikatananews.net", 27992));
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

        public async Task MainAsync()
        {
            byte[] datatest = new byte[11];
            datatest[0] = (byte)'\xff';
            datatest[1] = (byte)'\xff';
            datatest[2] = (byte)'\xff';
            datatest[3] = (byte)'\xff';
            datatest[4] = (byte)'s';
            datatest[5] = (byte)'t';
            datatest[6] = (byte)'a';
            datatest[7] = (byte)'t';
            datatest[8] = (byte)'u';
            datatest[9] = (byte)'s';
            datatest[10] = (byte)'\0';

            int intPort = 27192;

            InitServers();


            // Tokens should be considered secret data, and never hard-coded.
            await _client.LoginAsync(TokenType.Bot, "");
            await _client.StartAsync();

            // Block the program until it is closed.
            while (true)
            {
                int size = servers.Count;

                for (int i = 0; i < size; i++)
                {
                    try
                    {
                        dkserver server = servers[i];
                        SendUdp(ref server, intPort, server.ip, server.port, datatest);
                    }
                    catch
                    {

                    }
                }
                Thread.Sleep(1000 * 60);
            }
        }
    }
}
