using System;
using System.Collections.Generic;
using System.IO;

namespace DK_UDP_Bot
{
    public class BanList
    {
        public string ip;
        public ushort port;

        public BanList ()
        {
            reset();
        }

        private void reset ()
        {
            ip = string.Empty;
            port = 0;
        }
    }

    public partial class Program
    {
        List<BanList> bannedIPs = new List<BanList>();
        string BanListFileName = string.Empty;

        public void Init_BanList ()
        {
            if (String.IsNullOrWhiteSpace(BanListFileName))
            {
                return;
            }

            if (!System.IO.File.Exists(BanListFileName))
            {
                Console.Write("Ban list file {0} does not exist.\n", BanListFileName);
            }

            using (StreamReader sr = new StreamReader(BanListFileName))
            {
                while (!sr.EndOfStream)
                {
                    ushort _port = 0;
                    var line = sr.ReadLine().Split(':');
                    if (line.Length != 2)
                    {
                        continue;
                    }
                    ushort.TryParse(line[1], out _port);
                    bannedIPs.Add(new BanList { ip = line[0], port = _port });
                }
            }
        }
    }
}
