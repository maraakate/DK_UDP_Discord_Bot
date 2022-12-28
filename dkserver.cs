using System;
using System.Collections.Generic;

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
}
