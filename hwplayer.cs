using System;
using System.Collections.Generic;
using System.Text;

namespace DK_UDP_Bot
{
    enum hwSkinColour
    {
        white = 0,
        dark_green = 1,
        violet = 2,
        green = 3,
        red = 4,
        light_green = 5,
        gold = 6,
        peach = 7,
        purple = 8,
        light_purple = 9,
        tan = 10,
        seafoam = 11,
        yellow = 12,
        blue = 13,
    };

    class hwplayer
    {
        // Con_Printf ("%i %i %i %i \"%s\" \"%s\" %i %i\n", cl->userid, 
        //              cl->old_frags, (int)(realtime - cl->connection_started)/60,
        //              ping, cl->name, Info_ValueForKey (cl->userinfo, "skin"), top, bottom);

        public int userid;
        public int frags;
        public TimeSpan connectTime;
        public int ping;
        public string name;
        public string skin;
        public hwSkinColour top;
        public hwSkinColour bottom;

        public hwplayer()
        {
            Reset();
        }

        public hwplayer(int _userid, int _frags, int _connectTime, int _ping, string _name, string _skin, int _top, int _bottom)
        {
            userid = _userid;
            frags = _frags;
            connectTime = new TimeSpan(0, _connectTime, 0);
            ping = _ping;
            name = _name;
            skin = _skin;
            top = (hwSkinColour)_top;
            bottom = (hwSkinColour)_bottom;
        }

        private void Reset ()
        {
            userid = 0;
            frags = 0;
            connectTime = new TimeSpan(0, 0, 0);
            ping = 0;
            name = "Player";
            skin = "";
            top = hwSkinColour.white;
            bottom = hwSkinColour.white;
        }
    }
}
