using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Linux_Commander.common
{
    public enum EVENT_MON
    {
        SHUTDOWN = 0
    }

    internal enum TABLE_NAMES
    {
        HostHistory,
        Configuration,
        CommandTranslation,
        InternalCommands
    }

    internal enum CURSOR_LOC
    {
        LEFT,
        TOP
    }

    internal struct APP_INFO
    {
        public string AppFullPath;
        public string AppName;
        public string AppDIR;
        public string FileName;
        public string FileVersion;
        public string AssemblyVersion;
        public string CompanyName;
        public string Comments;
        public string ServerName;
        public string UserName;
        public string DomainName;
    }

    public abstract class Defs
    {
        [DllImport("user32.dll")]
        internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        internal static byte VK_CAPSLOCK = 0x14;
        internal static uint KEYEVENTF_EXTENDEDKEY = 0x1;
        internal static uint KEYEVENTF_KEYUP = 0x2;

        private static bool _shutDown = false;
        private static string _winVersion = null;
        private static StringBuilder _dataContent = new StringBuilder();
        internal static string IP_REGEX = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        internal static string[] _sizeSuffixes = new string[] { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        internal static TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
        internal static StringBuilder DataContent
        {
            get
            {
                lock (_dataContent)
                    return _dataContent;
            }
            set
            {
                lock (_dataContent)
                    _dataContent = value;
            }
        }
        internal static ManualResetEvent[] EventMonitor { get; set; } = new ManualResetEvent[]
                {
                    new ManualResetEvent(false)     //SHUTDOWN
                };
        internal static List<string> ConfigColumnNames { get; } = new List<string>()
                            {
                                "ConnectionTimeoutSec",
                                "AvailableColors",
                                "DirectoryColor",
                                "StickyColor",
                                "FullPermissionsBGColor",
                                "FullPermissionsFGColor",
                                "BlockSpecialFileColor",
                                "CharacterSpecialFileColor",
                                "SymbolicLinkColor",
                                "PromptColor"
                            };
        internal static List<string> HistoryColumnNames { get; } = new List<string>()
                            {
                                "HostID",
                                "HostName",
                                "UserName",
                                "LocalPath",
                                "RemotePath",
                                "Environment"
                            };
        internal static List<string> TranslationColumnNames { get; } = new List<string>()
                            {
                                "Typed",
                                "ChangeTo",
                                "Options",
                                "Description",
                                "Usage"
                            };
        internal static List<string> InternalCommands { get; } = new List<string>()
                            {
                                "Command",
                                "Description",
                                "Usage"
                            };
        internal static int Port { get; set; } = 22;
        internal static int[] CurrentCursor { get; set; } = new int[] { 0, 0 };
        internal static bool RequiresDirLookup { get; set; } = false;
        internal static bool Shutdown
        { 
            get { return _shutDown; }
            set 
            { 
                _shutDown = value;
                if (_shutDown)
                {
                    Console.Clear();
                    EventMonitor[(int)EVENT_MON.SHUTDOWN].Set();
                }
            }
        }
        internal static bool MissingHeaderShown { get; set; } = false;
        internal static bool NewHost { get; set; } = false;
        internal static bool UserSpecialChars 
        {
            get
            {
                if (WinVersion.Contains("Windows 10"))
                    return true;
                else
                    return false;
            }
        }
        internal static string WinVersion
        {
            get
            {
                if (_winVersion == null)
                {
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                    foreach (ManagementObject os in searcher.Get())
                    {
                        _winVersion = os["Caption"].ToString();
                        break;
                    }
                }

                return _winVersion;
            }
        }
        internal static string CurrentRemotePath { get; set; } = null;
        internal static string ConsoleTitle { get; set; } = null;
        internal static string HostEnv { get; set; } = null;
        internal static string UserName { get; set; } = null;
        internal static string PassWord { get; set; } = null;
        internal static string HostName { get; set; } = null;
        internal static string UserServer { get; set; } = ">:";     //default
        internal static string PromptsRemoteDir { get; set; }
        internal static string LocalPath { get; set; } = $"{General.AppInfo.AppDIR}\\Ansible";
        internal static string RemotePath { get; set; } = "/";
        internal static string TextEditor { get; set; } = "notepad.exe";
        internal static string HostNameShort
        {
            get
            {
                Regex regIP = new Regex(IP_REGEX);
                if (regIP.IsMatch(HostName))
                    return HostName;

                return HostName.Split('.')[0];
            }
        }
    }
}
