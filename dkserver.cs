using System;
using System.Collections.Generic;

namespace DK_UDP_Bot
{
    class dkserver
    {
        public string ip = String.Empty;
        public int port = 0;
        public List<dkplayer> players = new List<dkplayer>();
        public int activeplayers = 0;
        public bool bAlertChanged = false;
        public DateTime heartbeat = DateTime.UtcNow;
        public Dictionary<string, string> serverParams = new Dictionary<string, string>();

        public dkserver(string _ip, int _port)
        {
            ip = _ip;
            port = _port;
            Reset();
        }

        private void Reset()
        {
            players.Clear();
            serverParams.Clear();
            activeplayers = 0;
            bAlertChanged = false;
        }
    }
}
