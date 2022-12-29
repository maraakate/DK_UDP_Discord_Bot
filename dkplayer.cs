namespace DK_UDP_Bot
{
    class dkplayer
    {
        public int score;
        public int ping;
        public string netname;

        public dkplayer ()
        {
            Reset();
        }

        public dkplayer (int _score, int _ping, string _netname)
        {
            score = _score;
            ping = _ping;
            netname = _netname;
        }

        private void Reset ()
        {
            score = 0;
            ping = 0;
            netname = string.Empty;
        }
    }
}
