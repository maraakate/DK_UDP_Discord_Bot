using System;
using System.Collections.Generic;

namespace DK_UDP_Bot
{
    enum serverType
    {
        Unknown,
        Daikatana,
        Hexen2,
        Heretic2,
        HexenWorld,
        SiN
    }

    class dkserver
    {
        public string ip = String.Empty;
        public ushort port = 0;
        public serverType serverType = serverType.Unknown;
        public List<dkplayer> dkPlayers = new List<dkplayer>();
        public List<hwplayer> hwPlayers = new List<hwplayer>();
        public int activeplayers = 0;
        public bool bAlertChanged = false;
        public DateTime heartbeat = DateTime.UtcNow;
        public Dictionary<string, string> serverParams = new Dictionary<string, string>();

        public dkserver(string _ip, ushort _port, serverType _serverType)
        {
            serverType = _serverType;
            ip = _ip;
            port = _port;
            Reset();
        }

        private void Reset()
        {
            dkPlayers.Clear();
            hwPlayers.Clear();
            serverParams.Clear();
            activeplayers = 0;
            bAlertChanged = false;
        }
    }
}
