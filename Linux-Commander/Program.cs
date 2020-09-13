using Linux_Commander.common;
using System;
using System.Threading;

namespace Linux_Commander
{
    class Program
    {
        static void Main(string[] args)
        {
            General.ApplicationHeader(true);
            LoadArgs(args);

            if (!DataFiles.LoadConfig())
                return;

            try
            {
                Ssh ssh = new Ssh();
                Thread tt = new Thread(new ThreadStart(ssh.ProptForCommands));
                tt.Start();

                while (WaitHandle.WaitAny(Defs.EventMonitor) != (int)EVENT_MON.SHUTDOWN) ;

                DateTime startDate = DateTime.UtcNow;
                //give it 3 seconds, if still running.
                while (tt.IsAlive && tt.ThreadState == ThreadState.Running && DateTime.UtcNow.Subtract(startDate).TotalSeconds < 3)
                    Thread.Sleep(1000);

                //kill the thread if still alive.
                if (tt.IsAlive && tt.ThreadState == ThreadState.Running)
                    tt.Abort();
            }
            finally
            {
                Log.Verbose("Thread Closed..", ConsoleColor.DarkYellow);
            }
        }

        static void LoadArgs(string[] args)
        {
            foreach (string arg in args)
            {
                string[] argVal = arg.Split(':');
                if (argVal.Length == 2)
                {
                    switch (argVal[0].ToLower())
                    {
                        case "-h":
                            string[] host = argVal[1].Trim().Split(':');
                            if (host.Length > 1)
                            {
                                if (int.TryParse(host[1], out int port))
                                    Defs.Port = port;
                            }
                            Defs.HostName = host[0];
                            break;
                        case "-u":
                            Defs.UserName = argVal[1].Trim();
                            break;
                        case "-p":
                            Defs.PassWord = argVal[1].Trim();
                            break;
                    }
                }
            }
        }
    }
}
