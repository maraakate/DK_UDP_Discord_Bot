using System;
using System.Collections.Generic;
using System.Text;

/*
GSMSALG 0.3.3
by Luigi Auriemma
e-mail: aluigi@autistici.org
web:    aluigi.org


INTRODUCTION
============
With the name Gsmsalg I define the challenge-response algorithm needed
to query the master servers that use the GameSpy "secure" protocol (like
master.gamespy.com for example).
This algorithm is not only used for this type of query but also in other
situations like the so called "GameSpy Firewall Probe Packet" and the
master server hearbeat that is the challenge string sent by the master
servers of the games that use the GameSpy SDK when game servers want to
be included in the online servers list (UDP port 27900).


HOW TO USE
==========
The function needs 4 parameters:
- dst:     the destination buffer that will contain the calculated
           response. Its length is 4/3 of the challenge size so if the
           challenge is 6 bytes long, the response will be 8 bytes long
           plus the final NULL byte which is required (to be sure of the
           allocated space use 89 bytes or "((len * 4) / 3) + 3")
           if this parameter is NULL the function will allocate the
           memory for a new one automatically
- src:     the source buffer containing the challenge string received
           from the server.
- key:     the gamekey or any other text string used as algorithm's
           key, usually it is the gamekey but "might" be another thing
           in some cases. Each game has its unique GameSpy gamekey which
           are available here:
           http://aluigi.org/papers/gslist.cfg
- enctype: are supported 0 (plain-text used in old games, heartbeat
           challenge respond, enctypeX and more), 1 (GameSpy3D) and 2
           (old GameSpy Arcade or something else).

The return value is a pointer to the destination buffer.


EXAMPLE
=======
  #include "gsmsalg.h"

  char  *dest;
  dest = gsseckey(
    NULL,       // dest buffer, NULL for auto allocation
    "ABCDEF",   // the challenge received from the server
    "kbeafe",   // kbeafe of Doom 3 and enctype set to 0
    0);         // enctype 0


LICENSE
=======
    Copyright 2004,2005,2006,2007,2008 Luigi Auriemma

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307 USA

    http://www.gnu.org/licenses/gpl.txt
*/

namespace DK_UDP_Bot
{
    public class gsmalg
    {
        private static byte gsvalfunc(int reg)
        {
            if (reg < 26)
                return (byte)(reg + 'A');
            if (reg < 52)
                return (byte)(reg + 'G');
            if (reg < 62)
                return (byte)(reg - 4);
            if (reg == 62)
                return (byte)('+');
            if (reg == 63)
                return (byte)('/');

            return 0;
        }

        public byte[] gsseckey(byte[] dst, byte[] src, byte[] key)
        {
            int i, size, keysz;
            byte[] enctmp = new byte[256];
            byte[] tmp = new byte[66];
            byte x, y, z, a = 0, b = 0;

            if (dst == null)
            {
                dst = new byte[89];
            }

            size = src.Length;
            if (size < 1 || size > 65)
            {
                dst[0] = 0;
                return dst;
            }

            keysz = key.Length;

            for (i = 0; i < 256; i++)
            {
                enctmp[i] = (byte)i;
            }

            for (i = 0; i < 256; i++)
            {
                a += (byte)(enctmp[i] + key[i % keysz]);
                x = enctmp[a];
                enctmp[a] = enctmp[i];
                enctmp[i] = x;
            }

            a = 0;
            b = 0;
            for (i = 0; i < size; i++)
            {
                a += (byte)(src[i] + 1);
                x = enctmp[a];
                b += x;
                y = enctmp[b];
                enctmp[b] = x;
                enctmp[a] = y;
                tmp[i] = (byte)(src[i] ^ enctmp[(x + y) & 0xff]);
            }

            for (size = i; size % 3 != 0; size++)
            {
                tmp[size] = 0;
            }

            int pIndex = 0;
            for (i = 0; i < size; i += 3)
            {
                x = tmp[i];
                y = tmp[i + 1];
                z = tmp[i + 2];
                dst[pIndex++] = gsvalfunc(x >> 2);
                dst[pIndex++] = gsvalfunc(((x & 3) << 4) | (y >> 4));
                dst[pIndex++] = gsvalfunc(((y & 15) << 2) | (z >> 6));
                dst[pIndex++] = gsvalfunc(z & 63);
            }

            dst[pIndex] = 0;

            return dst;
        }
    }
}
