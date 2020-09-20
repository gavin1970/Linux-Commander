using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;

namespace Linux_Commander.common
{
    class General : Defs
    {
        static private APP_INFO _appInfo = new APP_INFO();
        static private object _lock = new object();

        internal static APP_INFO AppInfo
        {
            get
            {
                lock (_lock)
                {
                    if (_appInfo.AppFullPath == null)
                    {
                        string appFullPath = Assembly.GetEntryAssembly().Location;

                        _appInfo = new APP_INFO
                        {
                            AppFullPath = appFullPath,
                            AppDIR = Path.GetDirectoryName(appFullPath),
                            AppName = FileVersionInfo.GetVersionInfo(appFullPath).ProductName,
                            FileName = Path.GetFileName(appFullPath),
                            FileVersion = FileVersionInfo.GetVersionInfo(appFullPath).FileVersion,
                            CompanyName = FileVersionInfo.GetVersionInfo(appFullPath).CompanyName,
                            Comments = FileVersionInfo.GetVersionInfo(appFullPath).Comments,
                            AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                            ServerName = Dns.GetHostEntry("").HostName,
                            UserName = Environment.UserName,
                            DomainName = Environment.UserDomainName
                        };
                    }
                }

                return _appInfo;
            }
        }

        internal static string GetTzAbbreviation(string timeZoneName)
        {
            string output = string.Empty;

            string[] timeZoneWords = timeZoneName.Split(' ');
            foreach (string timeZoneWord in timeZoneWords)
            {
                if (timeZoneWord[0] != '(')
                {
                    output += timeZoneWord[0];
                }
                else
                {
                    output += timeZoneWord;
                }
            }
            return output;
        }

        /// <summary>
        /// Display the application header text
        /// </summary>
        /// <param name="clear"></param>
        internal static void ApplicationHeader(bool clear = false)
        {
            if (clear)
                Console.Clear();

            if (ConsoleTitle == null)
            {
                ConsoleTitle = $"{AppInfo.AppName} - v{AppInfo.FileVersion}";
                Console.Title = ConsoleTitle;
            }

            Log.Verbose("");
            Log.Verbose($"Welcome to {AppInfo.AppName} for Windows!", ConsoleColor.DarkCyan, true, true);
            Log.Verbose("");
        }

        /// <summary>
        /// Display all Internal and User Translation commands.
        /// </summary>
        /// <param name="helpFor"></param>
        internal static void DisplayHelp(string helpFor = null)
        {
            if (helpFor == null)
            {
                ApplicationHeader(true);

                Log.Verbose("\n");

                Log.Verbose("      ###   ###   ###  #########  ###        ##########   ###      ", ConsoleColor.Cyan, true, true);
                Log.Verbose("    ###     ###   ###  #########  ###        ##########     ###    ", ConsoleColor.Cyan, true, true);
                Log.Verbose("  ###       ###   ###  ###        ###        ###    ###       ###  ", ConsoleColor.Cyan, true, true);
                Log.Verbose("###         #########  #########  ###        ##########         ###", ConsoleColor.Cyan, true, true);
                Log.Verbose("###         #########  #########  ###        ##########         ###", ConsoleColor.Cyan, true, true);
                Log.Verbose("  ###       ###   ###  ###        ###        ###              ###  ", ConsoleColor.Cyan, true, true);
                Log.Verbose("    ###     ###   ###  #########  #########  ###            ###    ", ConsoleColor.Cyan, true, true);
                Log.Verbose("      ###   ###   ###  #########  #########  ###          ###      ", ConsoleColor.Cyan, true, true);

                Log.Verbose("\n\n");

                Log.Verbose("Please note, all Linux commands are accepted.  These are shortcuts or", ConsoleColor.White, true, true);
                Log.Verbose("windows calls, translated into linux commands or a mixture of both.", ConsoleColor.White, true, true);

                Log.Verbose("\n\n");

                //##################################################################################################
                Log.Verbose("-=[ INTERNAL COMMANDS ]=-\n", ConsoleColor.Green, true);
            }
            else
            {
                Log.Verbose($"-=[ Example of Usage for {helpFor} ]=-\n", ConsoleColor.Cyan, true);
            }

            DataTable dtInternCmd = DataFiles.GetInternalCommands();
            foreach (DataRow dr in dtInternCmd.Select(helpFor == null ? null : $"Command='{helpFor}'", "Command"))
            {
                if (DataFiles.GetField<string>(dr, "Command", out string command))
                {
                    if (!DataFiles.GetField<string>(dr, "Description", out string descriptions))
                        descriptions = "";
                    if (!DataFiles.GetField<string>(dr, "Usage", out string usages))
                        usages = "";

                    Log.Verbose($"\t{PadString($"{command}:", 30)}", ConsoleColor.Yellow, false);

                    int dLine = 0;
                    foreach (string description in descriptions.Split(';'))
                    {
                        if (description.Trim().Length > 0) { 
                            if(dLine == 0)
                                Log.Verbose(0, description, ConsoleColor.Gray);
                            else
                                Log.Verbose($"\t{PadString(" ", 30)}{description}", ConsoleColor.Gray);
                            dLine++;
                        }
                    }

                    foreach (string usage in usages.Split(';'))
                    {
                        if (usage.Trim().Length > 0)
                            Log.Verbose($"\t{PadString(" ", 30)}e.g. {usage}", ConsoleColor.Gray);
                    }

                    Log.Verbose($"\t{new string('-', 75)}", ConsoleColor.Gray);
                }
            }

            //extra \r\n
            Console.WriteLine("");

            if (helpFor == null)
            {
                //##################################################################################################
                Log.Verbose("-=[ USER COMMANDS ]=-", ConsoleColor.Green, true);
                Log.Verbose($"Can be edited or added to here:", ConsoleColor.Cyan, true);
                Log.Verbose(5, $"{DataFiles.DataFile}\n", ConsoleColor.Cyan, true);

                DataTable dtTransData = DataFiles.GetTranslationData();
                foreach (DataRow dr in dtTransData.Select(null, "Typed"))
                {
                    if (DataFiles.GetField<string>(dr, "Typed", out string command))
                    {
                        if (DataFiles.GetField<string>(dr, "ChangeTo", out string changeTo))
                        {
                            if (!DataFiles.GetField<string>(dr, "Options", out string options))
                                options = "";
                            if (!DataFiles.GetField<string>(dr, "Description", out string description))
                                description = "";
                            if (!DataFiles.GetField<string>(dr, "Usage", out string usages))
                                usages = "";

                            Log.Verbose($"\t{PadString($"{command}:", 30)}", ConsoleColor.Yellow, false);
                            Log.Verbose(0, description, ConsoleColor.Gray);
                            Log.Verbose($"\t{PadString(" ", 30)}Translates To: {changeTo} {options}", ConsoleColor.Gray);

                            foreach (string usage in usages.Split(';'))
                            {
                                if (usage.Trim().Length > 0)
                                    Log.Verbose($"\t{PadString(" ", 30)}e.g. {usage}", ConsoleColor.Gray);
                            }

                            Log.Verbose($"\t{new string('-', 75)}", ConsoleColor.Gray);
                        }
                    }
                }

                //extra \r\n
                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Pad a string in front or behind text
        /// </summary>
        /// <param name="str"></param>
        /// <param name="size"></param>
        /// <param name="inFront"></param>
        /// <returns></returns>
        internal static string PadString(string str, int size, bool inFront = false)
        {
            int len = str.Length;

            if (inFront && (size >= len))
                str = new string(' ', size - len) + str;
            else if (size >= len)
                str += new string(' ', size - len);
            else
                str = $"{str.Substring(0, size - 4)}... ";

            return str;
        }

        /// <summary>
        /// Display Linux Permission information to help new linux users.
        /// </summary>
        internal static void ShowPermissionHelp()
        {
            const int maxLineLength = 100;

            ApplicationHeader(true);
            Log.Verbose("===========[ Linux Permissions Help ]===========", ConsoleColor.Yellow);

            Log.Verbose(2, "User Permissions:", ConsoleColor.Cyan);
            Log.Verbose(5, "The owner of the file or directory.", ConsoleColor.Cyan);

            Log.Verbose(2, "Group Permissions:", ConsoleColor.Green);
            Log.Verbose(5, "Group of the file or directory.  This must be one of the owner's groups.", ConsoleColor.Green);

            Log.Verbose(2, "Other Permissions:", ConsoleColor.DarkYellow);
            Log.Verbose(5, "Everyone else.\n", ConsoleColor.DarkYellow);

            if (UserSpecialChars)
            {
                Log.Verbose(10, "╔═ User Permissions", ConsoleColor.Cyan);
                Log.Verbose(10, "║ ", ConsoleColor.Cyan, false);
                Log.Verbose("╔═ Group Permissions", ConsoleColor.Green);

                Log.Verbose(2, "Type ═╗ ", ConsoleColor.Red, false);
                Log.Verbose(0, "║  ", ConsoleColor.Cyan, false);
                Log.Verbose(0, "║  ", ConsoleColor.Green, false);
                Log.Verbose(0, "╔═ Other Permissions", ConsoleColor.DarkYellow);

                Log.Verbose(8, "║ ", ConsoleColor.Red, false);
                Log.Verbose(0, "║  ", ConsoleColor.Cyan, false);
                Log.Verbose(0, "║  ", ConsoleColor.Green, false);
                Log.Verbose(0, "║", ConsoleColor.DarkYellow);

                Log.Verbose(8, "║", ConsoleColor.Red, false);
                Log.Verbose(0, "╔╬╗", ConsoleColor.Cyan, false);
                Log.Verbose(0, "╔╬╗", ConsoleColor.Green, false);
                Log.Verbose(0, "╔╬╗", ConsoleColor.DarkYellow);
            }
            else
            {
                Log.Verbose(11, "_ User Permissions", ConsoleColor.Cyan);
                Log.Verbose(10, "| ", ConsoleColor.Cyan, false);
                Log.Verbose(2, "_ Group Permissions", ConsoleColor.Green);

                Log.Verbose(2, "Type _  ", ConsoleColor.Red, false);
                Log.Verbose(0, "|  ", ConsoleColor.Cyan, false);
                Log.Verbose(0, "|  ", ConsoleColor.Green, false);
                Log.Verbose(1, "_ Other Permissions", ConsoleColor.DarkYellow);

                Log.Verbose(8, "| ", ConsoleColor.Red, false);
                Log.Verbose(0, "|  ", ConsoleColor.Cyan, false);
                Log.Verbose(0, "|  ", ConsoleColor.Green, false);
                Log.Verbose(0, "|", ConsoleColor.DarkYellow);

                Log.Verbose(8, "|", ConsoleColor.Red, false);
                Log.Verbose(0, "/|\\", ConsoleColor.Cyan, false);
                Log.Verbose(0, "/|\\", ConsoleColor.Green, false);
                Log.Verbose(0, "/|\\", ConsoleColor.DarkYellow);
            }

            Log.Verbose(8, "-", ConsoleColor.Red, false);
            Log.Verbose(0, "rwx", ConsoleColor.Cyan, false);
            Log.Verbose(0, "r-x", ConsoleColor.Green, false);
            Log.Verbose(0, "r-x", ConsoleColor.DarkYellow, false);

            if (UserSpecialChars)
            {
                Log.Verbose(0, ". 2 root  root     6 Aug 25 06:06 FileOrDirecotryName", ConsoleColor.Blue);
                Log.Verbose(18, "║ ║  ║     ║  ╚═╦══╝ ╚════╦═════╝ ╚═══════╦═════════╝", ConsoleColor.Blue);
                Log.Verbose(18, "║ ║  ║     ║    ║         ║               ╚ File or Directory", ConsoleColor.Blue);
                Log.Verbose(18, "║ ║  ║     ║    ║         ╚ Date and Time Stamp", ConsoleColor.Blue);
                Log.Verbose(18, "║ ║  ║     ║    ╚ File or Directory Size", ConsoleColor.Blue);
                Log.Verbose(18, "║ ║  ║     ╚ Group Name", ConsoleColor.Blue);
                Log.Verbose(18, "║ ║  ╚ Owner Name", ConsoleColor.Blue);
                Log.Verbose(18, "║ ╚ The number of hard links to this file", ConsoleColor.Blue);
                Log.Verbose(18, "╚ . is normal, @ or + means extended permissions.  Look up 'setfacl', 'getfacl', and 'xattr' for more information about this.\n", ConsoleColor.Blue);
            }
            else
            {
                Log.Verbose(0, ". 2 root  root     6 Aug 25 06:06 FileOrDirecotryName", ConsoleColor.Blue);
                Log.Verbose(18, "| |  |     |  |____| |__________| |_________________|", ConsoleColor.Blue);
                Log.Verbose(18, "| |  |     |    |         |               |_ File or Directory", ConsoleColor.Blue);
                Log.Verbose(18, "| |  |     |    |         |_ Date and Time Stamp", ConsoleColor.Blue);
                Log.Verbose(18, "| |  |     |    |_ File or Directory Size", ConsoleColor.Blue);
                Log.Verbose(18, "| |  |     |_ Group Name", ConsoleColor.Blue);
                Log.Verbose(18, "| |  |_ Owner Name", ConsoleColor.Blue);
                Log.Verbose(18, "| |_ The number of hard links to this file", ConsoleColor.Blue);
                Log.Verbose(18, "|_ . is normal, @ or + means extended permissions.  Look up 'setfacl', 'getfacl', and 'xattr' for more information about this.\n", ConsoleColor.Blue);
            }

            Log.Verbose(2, "Type:", ConsoleColor.Red);
            Log.Verbose(5, "- - standard file", ConsoleColor.Red);
            Log.Verbose(5, "d - directory", ConsoleColor.Red);
            Log.Verbose(5, "c - character special file", ConsoleColor.Red);
            Log.Verbose(5, "l - symbolic link", ConsoleColor.Red);
            Log.Verbose(5, "p - named pipe (FIFO)", ConsoleColor.Red);
            Log.Verbose(5, "n - Network file", ConsoleColor.Red);
            Log.Verbose(5, "s - socket", ConsoleColor.Red);
            Log.Verbose(5, "b - block special file\n", ConsoleColor.Red);

            Log.Verbose("Press any key to continue...", ConsoleColor.Green);
            Console.ReadKey(true);
            ApplicationHeader(true);
            Log.Verbose("===========[ Linux Permissions Help ]===========", ConsoleColor.Yellow);

            Log.Verbose(2, "Permission Meaning:", ConsoleColor.White);
            Log.Verbose(5, "r = read (4)", ConsoleColor.Cyan);
            Log.Verbose(5, "w = write (2)", ConsoleColor.Cyan);
            Log.Verbose(5, "x = execute (1)", ConsoleColor.Cyan);
            Log.Verbose(5, "- = no permissions (0)", ConsoleColor.Cyan);
            Log.Verbose(5, "s = setuid/setgid bit.  (setuid - user setting) (setgid - group setting)", ConsoleColor.Cyan);
            Log.Verbose(8, "setuid permission:  [chmod u+s myfile] to add or [chmod u+x myfile] to remove it.", ConsoleColor.DarkCyan);
            Log.Verbose(10, "When set-user identification (setuid) permission is set on an executable file, a process that runs this file is granted access based on the owner of the file (usually root), rather than the user who is running the executable file. This special permission allows a user to access files and directories that are normally only available to the owner. For example, the setuid permission on the passwd command makes it possible for a user to change passwords, assuming the permissions of the root ID.", ConsoleColor.Gray, true, false, maxLineLength);
            Log.Verbose(10, "This special permission presents a security risk, because some determined users can find a way to maintain the permissions that are granted to them by the setuid process even after the process has finished executing.", ConsoleColor.Gray, true, false, maxLineLength);
            Log.Verbose(8, "setgid permission:  [chmod g+s myfile] to add or [chmod g+x myfile] to remove it.", ConsoleColor.DarkCyan);
            Log.Verbose(10, "The set-group identification (setgid) permission is similar to setuid, except that the process's effective group ID (GID) is changed to the group owner of the file, and a user is granted access based on permissions granted to that group. The /usr/bin/mail command has setgid permissions.", ConsoleColor.Gray, true, false, maxLineLength);
            Log.Verbose(10, "When setgid permission is applied to a directory, files that were created in this directory belong to the group to which the directory belongs, not the group to which the creating process belongs. Any user who has write and execute permissions in the directory can create a file there. However, the file belongs to the group that owns the directory, not to the user's group ownership.", ConsoleColor.Gray, true, false, maxLineLength);
            Log.Verbose(10, "You should monitor your system for any unauthorized use of the setuid and setgid permissions to gain superuser privileges. To search for and list all of the files that use these permissions, see How to Find Files With setuid Permissions. A suspicious listing grants group ownership of such a program to a user rather than to root or bin.", ConsoleColor.Gray, true, false, maxLineLength);
            Log.Verbose(5, "t = sticky bit (other only setting))", ConsoleColor.Cyan);
            Log.Verbose(8, "sticky bit: [chmod +t myfile] to add or [chmod +x myfile] to remove it.", ConsoleColor.DarkCyan);
            Log.Verbose(10, "The sticky bit is a permission bit that protects the files within a directory. If the directory has the sticky bit set, a file can be deleted only by the owner of the file, the owner of the directory, or by root.This special permission prevents a user from deleting other users' files from public directories such as /tmp\n", ConsoleColor.Gray, true, false, maxLineLength);

            Log.Verbose("Press any key to continue...", ConsoleColor.Green);
            Console.ReadKey(true);
            ApplicationHeader(true);
            Log.Verbose("===========[ Linux Permissions Help ]===========", ConsoleColor.Yellow);

            Log.Verbose(2, "Permission Numeric Values and Meaning:", ConsoleColor.White);
            Log.Verbose(5, "read (4) + write (2) + execute (1) = 7 (full permissions)", ConsoleColor.Cyan);
            Log.Verbose(5, "read (4) + write (0) + execute (1) = 5 (read/execute permissions)", ConsoleColor.Cyan);
            Log.Verbose(5, "read (4) + write (0) + execute (0) = 4 (read only permissions)\n", ConsoleColor.Cyan);

            if (UserSpecialChars)
            {
                Log.Verbose(5, "usr", ConsoleColor.Cyan, false);
                Log.Verbose(1, "│", ConsoleColor.Yellow, false);
                Log.Verbose(1, "grp", ConsoleColor.Cyan, false);
                Log.Verbose(1, "│", ConsoleColor.Yellow, false);
                Log.Verbose(1, "other", ConsoleColor.Cyan);

                Log.Verbose(5, "━━━━┿━━━━━┿━━━━━", ConsoleColor.Yellow);

                Log.Verbose(5, "rwx", ConsoleColor.Cyan, false);
                Log.Verbose(1, "│", ConsoleColor.Yellow, false);
                Log.Verbose(1, "r-x", ConsoleColor.Cyan, false);
                Log.Verbose(1, "│", ConsoleColor.Yellow, false);
                Log.Verbose(1, "r--", ConsoleColor.Cyan);

                Log.Verbose(5, "└╁┘", ConsoleColor.Cyan, false);
                Log.Verbose(1, "│", ConsoleColor.Yellow, false);
                Log.Verbose(1, "└╁┘", ConsoleColor.Cyan, false);
                Log.Verbose(1, "│", ConsoleColor.Yellow, false);
                Log.Verbose(1, "└╁┘", ConsoleColor.Cyan);

                Log.Verbose(6, "7", ConsoleColor.Cyan, false);
                Log.Verbose(2, "│", ConsoleColor.Yellow, false);
                Log.Verbose(2, "5", ConsoleColor.Cyan, false);
                Log.Verbose(2, "│", ConsoleColor.Yellow, false);
                Log.Verbose(2, "4\n", ConsoleColor.Cyan);
            }
            else
            {
                Log.Verbose(5, "usr", ConsoleColor.Cyan, false);
                Log.Verbose(1, "|", ConsoleColor.Yellow, false);
                Log.Verbose(1, "grp", ConsoleColor.Cyan, false);
                Log.Verbose(1, "|", ConsoleColor.Yellow, false);
                Log.Verbose(1, "other", ConsoleColor.Cyan);

                Log.Verbose(5, "----|-----|-----", ConsoleColor.Yellow);

                Log.Verbose(5, "rwx", ConsoleColor.Cyan, false);
                Log.Verbose(1, "|", ConsoleColor.Yellow, false);
                Log.Verbose(1, "r-x", ConsoleColor.Cyan, false);
                Log.Verbose(1, "|", ConsoleColor.Yellow, false);
                Log.Verbose(1, "r--", ConsoleColor.Cyan);

                Log.Verbose(5, "\\|/", ConsoleColor.Cyan, false);
                Log.Verbose(1, "|", ConsoleColor.Yellow, false);
                Log.Verbose(1, "\\|/", ConsoleColor.Cyan, false);
                Log.Verbose(1, "|", ConsoleColor.Yellow, false);
                Log.Verbose(1, "\\|/", ConsoleColor.Cyan);

                Log.Verbose(6, "7", ConsoleColor.Cyan, false);
                Log.Verbose(2, "|", ConsoleColor.Yellow, false);
                Log.Verbose(2, "5", ConsoleColor.Cyan, false);
                Log.Verbose(2, "|", ConsoleColor.Yellow, false);
                Log.Verbose(2, "4\n", ConsoleColor.Cyan);
            }

            Log.Verbose(6, "7 - The owner has full rights.", ConsoleColor.White);
            Log.Verbose(6, "5 - The group has read/execute rights.", ConsoleColor.White);
            Log.Verbose(6, "4 - Others have read only access.\n", ConsoleColor.White);

            Log.Verbose("Press any key to continue...", ConsoleColor.Green);
            Console.ReadKey(true);
            ApplicationHeader(true);
        }

        /// <summary>
        /// Display all colors for directory listings and where they can be changed.
        /// </summary>
        internal static void ShowLegend()
        {
            ApplicationHeader(true);

            Log.Verbose($"The prompt and directory listing colors can be changed by editing the following file:", ConsoleColor.Yellow, false);
            Log.Verbose($"\n {DataFiles.DataFile}\n", ConsoleColor.Gray, true);

            Log.Verbose($"\t{PadString(Log.Directories.ToString(), 15)}", Log.Directories, false);
            Log.Verbose($"= Directories", ConsoleColor.Gray, true);

            Log.Verbose($"\t{PadString(Log.Sticky.ToString(), 15)}", Log.Sticky, false);
            Log.Verbose($"= Sticky", ConsoleColor.Gray, true);

            Log.Verbose(0, "\t", ConsoleColor.Gray, false);
            Console.BackgroundColor = Log.FullPermissionsBG;
            Log.Verbose(0, $"{Log.FullPermissionsBG}", Log.FullPermissionsFG, false);
            Console.BackgroundColor = ConsoleColor.Black;
            Log.Verbose(0, $"{PadString("", 15 - Log.FullPermissionsBG.ToString().Length)}", ConsoleColor.Black, false);
            Log.Verbose($"= Full Permissions", ConsoleColor.Gray, true);

            Log.Verbose($"\t{PadString(Log.BlockSpecialFile.ToString(), 15)}", Log.BlockSpecialFile, false);
            Log.Verbose($"= Block Special File", ConsoleColor.Gray, true);

            Log.Verbose($"\t{PadString(Log.CharacterSpecialFile.ToString(), 15)}", Log.CharacterSpecialFile, false);
            Log.Verbose($"= Character Special File", ConsoleColor.Gray, true);

            Log.Verbose($"\t{PadString(Log.SymbolicLink.ToString(), 15)}", Log.SymbolicLink, false);
            Log.Verbose($"= Symbolic Link", ConsoleColor.Gray, true);

            Log.Verbose($"\t{PadString(Log.Prompt.ToString(), 15)}", Log.Prompt, false);
            Log.Verbose($"= Prompt\n", ConsoleColor.Gray, true);
        }

        /// <summary>
        /// Automatically use ZB,PB,TB,GB,MB,KB,bytes based on the size of Int64
        /// </summary>
        /// <param name="value"></param>
        /// <param name="decimalPlaces"></param>
        /// <returns></returns>
        internal static string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            if (value < 0) { return "-" + SizeSuffix(-value); }

            int i = 0;
            decimal dValue = (decimal)value;
            while (Math.Round(dValue, decimalPlaces) >= 1000)
            {
                dValue /= 1024;
                i++;
            }

            if (_sizeSuffixes[i] == "bytes")
                decimalPlaces = 0; //no decimal for bytes

            return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, _sizeSuffixes[i]);
        }

        /// <summary>
        /// Save in memory where the cursor is currenly.  This is used to move the 
        /// cursor back to a previous position and overwrite what was there.
        /// </summary>
        internal static void SaveCursor()
        {
            CurrentCursor[(int)CURSOR_LOC.LEFT] = Console.CursorLeft;
            CurrentCursor[(int)CURSOR_LOC.TOP] = Console.CursorTop;
        }

        /// <summary>
        /// Wipe out anything that might be in the path.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="left"></param>
        /// <param name="top"></param>
        internal static void ClearLine(int size, int left, int top)
        {
            Console.SetCursorPosition(left, top);
            Console.Write(new string(' ', size));
            Console.SetCursorPosition(left, top);
        }

        /// <summary>
        /// Show error or show PSCP Required if missing.
        /// </summary>
        /// <param name="msg"></param>
        internal static void PSCPRequired(string msg)
        {
            if (msg.ToLower().Contains("not found") || msg.ToLower().Contains("cannot find"))
            {
                Log.Verbose($"PSCP is used in {General.AppInfo.AppName} and required to pull or publish files/folders with Linux.");
                Log.Verbose($"Download it, and place it in your path or add it to the same folder as {General.AppInfo.AppName}.\n");
                Log.Verbose("Simon Tatham publishes new PSCP versions on his personal home page as he is the creator of Putty.");
                Log.Verbose("https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html \n", ConsoleColor.White);
            }
            else
                Log.Error(msg);
        }
    }
}
