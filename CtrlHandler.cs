using System;
using System.Runtime.InteropServices;

namespace DK_UDP_Bot
{
    public partial class Program
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);
        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;
        static bool exitSystem = false;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private bool Handler(CtrlType sig)
        {
            exitSystem = true;

            _client.StopAsync();
            _client.Dispose();

            //shutdown right away so there are no lingering threads
            Environment.Exit(-1);

            return true;
        }
    }
}
