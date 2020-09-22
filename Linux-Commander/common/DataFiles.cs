using JSONHelper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Linux_Commander.common
{
    class DataFiles : Defs
    {
        internal static string DataFile { get; set; } = $"{General.AppInfo.AppDIR}\\Data\\{General.AppInfo.AppName}.json";
        internal static JSONData ConfigData { get; set; } = new JSONData($"file={DataFile}");

        /// <summary>
        /// Load Colors from Config File.
        /// </summary>
        /// <returns></returns>
        internal static bool LoadConfig(bool refresh = false)
        {
            bool missingColumn = false;
            bool retVal = true;

            if (!Directory.Exists("Data"))
                Directory.CreateDirectory("Data");

            DataTable dt = GetConfigData(refresh);
            if (dt != null)
            {
                //validate all columns exists
                foreach (string colum in Defs.ConfigColumnNames)
                {
                    if (!dt.Columns.Contains(colum))
                    {
                        Log.Error($"Missing column: {colum} in {TABLE_NAMES.Configuration} from:\n {DataFile}");
                        missingColumn = true;
                        break;
                    }
                }
            }

            if (dt.Rows.Count > 0)
            {
                DataRow dr = dt.Rows[0];

                // ##### ConnectionTimeoutSec ##### 
                if (GetField<int>(dr, "ConnectionTimeoutSec", out int connectionTimeout))
                {
                    if (connectionTimeout > 60)
                        connectionTimeout = 60;
                    else if (connectionTimeout < 5)
                        connectionTimeout = 5;

                    Defs.ConnectionTimeout = TimeSpan.FromSeconds(connectionTimeout);
                }
                else
                    missingColumn = true;

                // ##### ConnectionTimeoutSec ##### 
                if (GetField<string>(dr, "TextEditor", out string textEditor))
                    Defs.TextEditor = textEditor;
                else
                    missingColumn = true;

                // ##### DirectoryColor ##### 
                if (GetField<ConsoleColor>(dr, "DirectoryColor", out ConsoleColor directoryColor))
                    Log.Directories = directoryColor;
                else
                    missingColumn = true;


                // ##### StickyColor ##### 
                if (GetField<ConsoleColor>(dr, "StickyColor", out ConsoleColor stickyColor))
                    Log.Sticky = stickyColor;
                else
                    missingColumn = true;


                // ##### FullPermissionsBGColor ##### 
                if (GetField<ConsoleColor>(dr, "FullPermissionsBGColor", out ConsoleColor fullPermissionsBGColor))
                    Log.FullPermissionsBG = fullPermissionsBGColor;
                else
                    missingColumn = true;


                // ##### FullPermissionsFGColor ##### 
                if (GetField<ConsoleColor>(dr, "FullPermissionsFGColor", out ConsoleColor fullPermissionsFGColor))
                    Log.FullPermissionsFG = fullPermissionsFGColor;
                else
                    missingColumn = true;


                // ##### BlockSpecialFileColor ##### 
                if (GetField<ConsoleColor>(dr, "BlockSpecialFileColor", out ConsoleColor blockSpecialFileColor))
                    Log.BlockSpecialFile = blockSpecialFileColor;
                else
                    missingColumn = true;


                // ##### CharacterSpecialFileColor ##### 
                if (GetField<ConsoleColor>(dr, "CharacterSpecialFileColor", out ConsoleColor characterSpecialFileColor))
                    Log.CharacterSpecialFile = characterSpecialFileColor;
                else
                    missingColumn = true;


                // ##### SymbolicLinkColor ##### 
                if (GetField<ConsoleColor>(dr, "SymbolicLinkColor", out ConsoleColor symbolicLinkColor))
                    Log.SymbolicLink = symbolicLinkColor;
                else
                    missingColumn = true;


                // ##### PromptColor ##### 
                if (GetField<ConsoleColor>(dr, "PromptColor", out ConsoleColor promptColor))
                    Log.Prompt = promptColor;
                else
                    missingColumn = true;
            }
            else
                missingColumn = true;

            if (missingColumn)
            {
                if (dt != null)
                    ConfigData.DropTable(dt);

                List<JSONWorker.FIELD_VALUE> fieldValues = new List<JSONWorker.FIELD_VALUE>();
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "ConnectionTimeoutSec", Value = $"{Defs.ConnectionTimeout.TotalSeconds}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "TextEditor", Value = $"{Defs.TextEditor}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "DirectoryColor", Value = $"{Log.Directories}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "StickyColor", Value = $"{Log.Sticky}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "FullPermissionsBGColor", Value = $"{Log.FullPermissionsBG}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "FullPermissionsFGColor", Value = $"{Log.FullPermissionsFG}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "BlockSpecialFileColor", Value = $"{Log.BlockSpecialFile}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "CharacterSpecialFileColor", Value = $"{Log.CharacterSpecialFile}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "SymbolicLinkColor", Value = $"{Log.SymbolicLink}" });
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "PromptColor", Value = $"{Log.Prompt}" });

                //provide all possible colors in config, so user changing config, knows which colors to choose from.
                StringBuilder sb = new StringBuilder();
                foreach (string name in Enum.GetNames(typeof(ConsoleColor)))
                {
                    if (sb.Length > 0)
                        sb.Append(", ");
                    sb.Append(name);
                }
                fieldValues.Add(new JSONWorker.FIELD_VALUE { FieldName = "AvailableColors", Value = sb.ToString() });

                JSONStatus rs = ConfigData.UpdateRecord(TABLE_NAMES.Configuration.ToString(), fieldValues, null, true);
                if (rs.Status != RESULT_STATUS.OK)
                {
                    Log.Error(rs.Description);
                    retVal = false;
                }
            }

            //During startup only, we want to clear out the table 
            //from this JSON and recreated it, when called apon. 
            //This ensures any new commands to be displayed as well.
            DeleteInternalCommandsHelp();
            SyncTranslationData();

            return retVal;
        }

        /// <summary>
        /// DataRow.Field CAST doesn't seem to work, so I've created this method to resolve.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dr"></param>
        /// <param name="columnName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static bool GetField<T>(DataRow dr, string columnName, out T value)
        {
            bool retVal = true;
            value = default(T);

            if (!dr.Table.Columns.Contains(columnName))
                retVal = false;
            else
            {
                string dataValue = dr.Field<string>(columnName);

                if (typeof(T).BaseType.Name == "Enum")
                    value = (T)Enum.Parse(typeof(T), dataValue);
                else
                    value = (T)Convert.ChangeType(dataValue, typeof(T));
            }

            return retVal;
        }

        /// <summary>
        /// Build out a new DataTable with Headers from configuration.
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columnNames"></param>
        /// <returns></returns>
        internal static DataTable CreateTableHeaders(TABLE_NAMES table, List<string> columnNames)
        {
            //used for new records or new tables only
            DataTable dataTable = new DataTable();

            foreach (string column in columnNames)
                dataTable.Columns.Add(column);

            dataTable.TableName = table.ToString();

            return dataTable;
        }

        /// <summary>
        /// Creates the next available Identifier for Host ID
        /// </summary>
        /// <returns></returns>
        internal static string GetNextHostID()
        {
            DataTable dt = GetHostData();
            int id = 0;

            //find empty record.  
            for (int i = 1; i <= 99; i++)
            {
                DataRow[] dr = dt.Select($"HostID='{string.Format("{0:00}", i)}'", null);
                if (dr.Length == 0)
                {
                    id = i;
                    break;
                }
            }

            return string.Format("{0:00}", id);
        }

        /// <summary>
        /// Save Host Information in Memory..  
        /// </summary>
        internal static void SaveHostHistory()
        {
            DataTable dt = GetHostData();
            DataRow[] dr = dt.Select($"HostName='{Defs.HostName}' AND UserName='{Defs.UserName}'");
            string hostID = dr.Length > 0 ? dr[0].Field<string>("HostID") : GetNextHostID();

            List<JSONWorker.FIELD_VALUE> fieldValues = new List<JSONWorker.FIELD_VALUE>
            {
                new JSONWorker.FIELD_VALUE { FieldName = "HostID", Value = hostID },
                new JSONWorker.FIELD_VALUE { FieldName = "HostName", Value = Defs.HostName },
                new JSONWorker.FIELD_VALUE { FieldName = "UserName", Value = Defs.UserName },
                new JSONWorker.FIELD_VALUE { FieldName = "Environment", Value = Defs.HostEnv },
                new JSONWorker.FIELD_VALUE { FieldName = "LocalPath", Value = Defs.LocalPath },
                new JSONWorker.FIELD_VALUE { FieldName = "RemotePath", Value = Defs.RemotePath }
            };

            _ = ConfigData.UpdateRecord(TABLE_NAMES.HostHistory.ToString(), fieldValues, $"HostID='{hostID}'", true);
        }

        /// <summary>
        /// Load Colors from Memory or File if not already loaded.
        /// </summary>
        /// <returns></returns>
        internal static DataTable GetConfigData(bool refresh = false)
        {
            DataTable dt = ConfigData.GetTable(TABLE_NAMES.Configuration.ToString(), refresh);

            if (dt == null || dt.Columns.Count == 0)
            {
                dt = CreateTableHeaders(TABLE_NAMES.Configuration, Defs.ConfigColumnNames);
                ConfigData.UpdateTable(dt);
            }

            return dt;
        }

        /// <summary>
        /// Load Host History information from memory or file if not already loaded.
        /// </summary>
        /// <param name="refresh"></param>
        /// <returns></returns>
        internal static DataTable GetHostData(bool refresh = false)
        {
            DataTable dt = ConfigData.GetTable(TABLE_NAMES.HostHistory.ToString(), refresh);

            if (dt == null || dt.Columns.Count == 0)
            {
                dt = CreateTableHeaders(TABLE_NAMES.HostHistory, Defs.HistoryColumnNames);
                ConfigData.UpdateTable(dt);
            }

            return dt;
        }

        /// <summary>
        /// Drop table at start, because we want to ensure all the internal commands, possibly new ones, are in the help file.
        /// </summary>
        internal static void DeleteInternalCommandsHelp()
        {
            DataTable dt = ConfigData.GetTable(TABLE_NAMES.InternalCommands.ToString());
            if (dt != null)
                ConfigData.DropTable(dt);
        }

        /// <summary>
        /// At startup, we don't want to delete user commands, but we want to add any that might not be there, we created.
        /// </summary>
        internal static void SyncTranslationData()
        {
            //existing
            DataTable dt = GetTranslationData();
            //what we have configured.
            DataTable confDt = GetTranslationData(true);

            //only what we have configured that isn't in existing.
            List<DataRow> diff = confDt.AsEnumerable().Where(r => !dt.AsEnumerable().Select(x => x["Typed"]).ToList().Contains(r["Typed"])).ToList();
            if (diff.Count > 0)
            {
                //insert each of them into the table.
                foreach (DataRow dr in diff)
                    dt.Rows.Add(dr.ItemArray);

                //update table and json file.
                ConfigData.UpdateTable(dt);
            }
        }

        /// <summary>
        /// Not currently used, but figured I might need it in the future.
        /// </summary>
        /// <param name="dt1"></param>
        /// <param name="dt2"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        internal static DataTable GetMatchingData(DataTable dt1, DataTable dt2, string columnName)
        {
            DataTable dtMerged =
                 (from a in dt1.AsEnumerable()
                  join b in dt2.AsEnumerable()
                  on a[columnName].ToString() equals b[columnName].ToString()
                  into g
                  where g.Count() > 0
                  select a).CopyToDataTable();

            return dtMerged;
        }

        /// <summary>
        /// Pulls all internal commands for Help Display.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal static DataTable GetInternalCommands()
        {
            DataTable dt = ConfigData.GetTable(TABLE_NAMES.InternalCommands.ToString());

            if (dt == null || dt.Rows.Count == 0)
            {
                dt = CreateTableHeaders(TABLE_NAMES.InternalCommands, Defs.InternalCommands);

                //create some defaults
                DataRow dr = dt.NewRow();
                dr["Command"] = "-help";
                dr["Description"] = "Linux Help Screen.";
                dr["Usage"] = "-help";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "edit-config";
                dr["Description"] = $"Edit {General.AppInfo.AppName} configuration file and refresh configuration;" +
                                    $"after editor is closed.";
                dr["Usage"] = "edit-config";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "about";
                dr["Description"] = $"Displays information about {General.AppInfo.AppName} and the Linux host your;" +
                                    $"connected to.";
                dr["Usage"] = "about";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "edit";
                dr["Description"] = $"Will pull file from Linux, open in local windows editor, then upload it back from;" +
                                    $"where it was located.";
                dr["Usage"] = "edit [REMOTE_FILE_NAME]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "install-ansible";
                dr["Description"] = "Will install PIP, Python3, and latest version of Ansible on remote connected;" +
                                    "Linux Server.";
                dr["Usage"] = "install-ansible";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "refresh";
                dr["Description"] = "Pulls all files and folders from set-remote directory and places them in the;" +
                                    "set-local directory.";
                dr["Usage"] = "refresh";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "sync-date";
                dr["Description"] = "Uses your current windows date/time and sets the linux server date/time to match.";
                dr["Usage"] = "sync-date";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "recon";
                dr["Description"] = "If you get kicked or disconnected from the server.;" +
                                    "Quickly reconnect with existing creds.;If already connected, recon;" +
                                    "will be ignored.";
                dr["Usage"] = "recon";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "publish";
                dr["Description"] = "Pushes all files and folders from your [set-local] directory and uploads;" +
                                    "them to your [set-remote] directory.";
                dr["Usage"] = "publish";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "send-file";
                dr["Description"] = "Takes a file from [set-local] and sends it to [set-root] directory.";
                dr["Usage"] = "send-file [LOCAL_FILE_NAME]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "get-file";
                dr["Description"] = "Pulls a file from current remote directory and saves it to;" +
                                    "[set-local] directory.";
                dr["Usage"] = "get-file [REMOTE_FILE_NAME]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "new-host";
                dr["Description"] = "Disconnect from current host and allows connection to another host.";
                dr["Usage"] = "new-host";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "local-dir";
                dr["Description"] = "Display files in the [set-local] folder.";
                dr["Usage"] = "local-dir";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "legend";
                dr["Description"] = "Display all the colors for the directory listing.";
                dr["Usage"] = "legend";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "permissions";
                dr["Description"] = "Help for a better understanding on permissions for directory listing.";
                dr["Usage"] = "permissions";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "whoami";
                dr["Description"] = "Displays the user connected to the server.";
                dr["Usage"] = "whoami";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "whereami";
                dr["Description"] = "Displays the server, port, and current directory.";
                dr["Usage"] = "whereami";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "set-remote";
                dr["Description"] = "Set root remote path where all files and folders are;" +
                                    "published to from [set-local]";
                dr["Usage"] = "set-remote [/REMOTE_PATH]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "set-local";
                dr["Description"] = "Set local path where all files are saved to or sent from.";
                dr["Usage"] = "set-local [[DRIVE][LOCAL_PATH]]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "sets";
                dr["Description"] = "Shows both set-local and set-remote.";
                dr["Usage"] = "sets";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "clear-local";
                dr["Description"] = "Sets [set-local] path back to temp direcotry.";
                dr["Usage"] = "clear-local";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "view";
                dr["Description"] = $"View translates to cat.  View is an application and;" +
                                    $"since {General.AppInfo.AppName} is a Virual UI, it's used;" +
                                    $"for passing command and getting results only.";
                dr["Usage"] = "view [LOCAL_FILENAME];view [[SUBDIR\\][LOCAL_FILENAME]]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "cls";
                dr["Description"] = "Clear the screen.";
                dr["Usage"] = "cls";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Command"] = "exit";
                dr["Description"] = $"Close { General.AppInfo.AppName}.";
                dr["Usage"] = "exit";
                dt.Rows.Add(dr);

                ConfigData.UpdateTable(dt);
            }

            return dt;
        }

        /// <summary>
        /// Load Transaction Data from memory or file if not already in memory.
        /// </summary>
        /// <returns></returns>
        internal static DataTable GetTranslationData(bool refresh = false)
        {
            DataTable dt = ConfigData.GetTable(TABLE_NAMES.CommandTranslation.ToString());

            if (dt == null || dt.Columns.Count == 0 || refresh)
            {
                dt = CreateTableHeaders(TABLE_NAMES.CommandTranslation, Defs.TranslationColumnNames);

                //create some defaults
                DataRow dr = dt.NewRow();
                dr["Typed"] = "cd..";
                dr["ChangeTo"] = "cd ..";
                dr["Options"] = "";
                dr["Description"] = "Corrects 'cd..' which will fail.";
                dr["Usage"] = "cd..";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "datetime";
                dr["ChangeTo"] = "timedatectl";
                dr["Options"] = "";
                dr["Description"] = "Display Date, Time, and Timezone information.";
                dr["Usage"] = "datetime";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "cd/";
                dr["ChangeTo"] = "cd /";
                dr["Options"] = "";
                dr["Description"] = "Corrects 'cd/' which will fail.";
                dr["Usage"] = "cd/";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "dir";
                dr["ChangeTo"] = "ls";
                dr["Options"] = "-ltr";
                dr["Description"] = "Windows command shortcut.";
                dr["Usage"] = "dir -=[Uses default options above;dir -altr -=[Uses only what you pass";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "co";
                dr["ChangeTo"] = "sudo chown";
                dr["Options"] = "";
                dr["Description"] = "Linux shortcut for change owner.";
                dr["Usage"] = "co [USER]:[GROUP] [[DIRECTORY]|[FILE]]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "md";
                dr["ChangeTo"] = "mkdir";
                dr["Options"] = "";
                dr["Description"] = "Windows command shortcut.";
                dr["Usage"] = "md [OPTIONS] [DIRECTORY_NAME]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "ap";
                dr["ChangeTo"] = "ansible-playbook";
                dr["Options"] = "";
                dr["Description"] = "Linux shortcut for ansible command.";
                dr["Usage"] = "ap [OPTIONS] [YAML_FILENAME]";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "rd";
                dr["ChangeTo"] = "rmdir";
                dr["Options"] = "";
                dr["Description"] = "Windows command shortcut.";
                dr["Usage"] = "rd /ME/SubFolder";
                dt.Rows.Add(dr);

                dr = dt.NewRow();
                dr["Typed"] = "del";
                dr["ChangeTo"] = "rm";
                dr["Options"] = "-f -r";
                dr["Description"] = "Windows command shortcut.";
                dr["Usage"] = "del * -=[Deletes all files and folders.;del [FOLDER] -=[Delete a folder.;del [FILENAME] -=[Delete a file.";
                dt.Rows.Add(dr);

                if(!refresh)
                    ConfigData.UpdateTable(dt);
            }

            return dt;
        }

        /// <summary>
        /// Ask for Environment name if not already in memory.
        /// </summary>
        internal static void CheckEnv()
        {
            if (Defs.CurrentCursor[(int)CURSOR_LOC.LEFT] != 0)
            {
                Console.SetCursorPosition(Defs.CurrentCursor[(int)CURSOR_LOC.LEFT], Defs.CurrentCursor[(int)CURSOR_LOC.TOP]);
                Log.Verbose(0, HostName, ConsoleColor.Yellow, true);
            }

            if (NewHost)
                Log.Verbose("-= [ Env Name ]=- (Enter to skip):  ", ConsoleColor.Green, false);
            else
                Log.Verbose("-= [ Env Name ]=- :  ", ConsoleColor.Green, false);
            General.SaveCursor();

            if (NewHost)
                HostEnv = Console.ReadLine();

            if (HostEnv == null)
                HostEnv = "";
        }

        /// <summary>
        /// Ask for Host name/IP if not already in memory.
        /// </summary>
        /// <returns></returns>
        internal static bool CheckHost()
        {
            bool retVal = true;

            Log.Verbose("-= [ HostInfo ]=- :  ", ConsoleColor.Green, false);
            General.SaveCursor();

            while (string.IsNullOrWhiteSpace(HostName))
            {
                General.ClearLine(50, Defs.CurrentCursor[(int)CURSOR_LOC.LEFT], Defs.CurrentCursor[(int)CURSOR_LOC.TOP]);
                HostName = Console.ReadLine();
                NewHost = true;

                if (string.IsNullOrWhiteSpace(HostName))
                {
                    retVal = false;
                    break;
                }
                else
                {
                    string[] hostInfo = HostName.Split(':');
                    if (hostInfo.Length > 1)
                    {
                        if (int.TryParse(hostInfo[1], out int port))
                            Port = port;
                        HostName = hostInfo[0];
                    }

                    if (!Processor.PingHost(HostName, Port))
                    {
                        //padding is to clear out previous port that might be longer in string.  Fixes screen overwrite issue.
                        Log.Error($"Failed to connect to host.  Port is currently set to {General.PadString(Port.ToString() + ".", 10)}\n If this port is incorrect try <HOST_NAME>:<PORT>");
                        HostName = null;
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Ask for username if not already in memory.
        /// </summary>
        /// <returns></returns>
        internal static bool CheckUser()
        {
            bool retVal = true;

            if (Defs.CurrentCursor[(int)CURSOR_LOC.LEFT] != 0)
            {
                Console.SetCursorPosition(Defs.CurrentCursor[(int)CURSOR_LOC.LEFT], Defs.CurrentCursor[(int)CURSOR_LOC.TOP]);
                Log.Verbose(0, HostEnv, ConsoleColor.Yellow, true);
            }

            Log.Verbose("-= [ Username ]=- :  ", ConsoleColor.Green, false);
            General.SaveCursor();

            while (string.IsNullOrWhiteSpace(UserName))
            {
                General.ClearLine(50, Defs.CurrentCursor[(int)CURSOR_LOC.LEFT], Defs.CurrentCursor[(int)CURSOR_LOC.TOP]);
                UserName = Console.ReadLine();
            }

            return retVal;
        }

        /// <summary>
        /// Ask for password.
        /// </summary>
        /// <param name="reattempt"></param>
        /// <returns></returns>
        internal static bool CheckPass(bool reattempt = false)
        {
            bool retVal = true;
            int[] PrevCursor = Defs.CurrentCursor;

            Console.SetCursorPosition(Defs.CurrentCursor[(int)CURSOR_LOC.LEFT], Defs.CurrentCursor[(int)CURSOR_LOC.TOP]);
            Log.Verbose(0, UserName, ConsoleColor.Yellow, true);
            Log.Verbose("-= [ Password ]=- :  ", ConsoleColor.Green, false);
            General.SaveCursor();

            while (string.IsNullOrWhiteSpace(PassWord) && retVal)
            {
                //check if capslock is on.
                if (Console.CapsLock)
                {
                    Console.SetCursorPosition(0, Defs.CurrentCursor[(int)CURSOR_LOC.TOP] + 2);
                    Log.Verbose("## CapsLock was ON ##", ConsoleColor.Red);
                    //if this is a reattmpt, then stay out of a loop.
                    if (!reattempt)
                    {
                        Thread.Sleep(1000);
                        //turn capslock off (keydown)
                        keybd_event(VK_CAPSLOCK, 0x45, KEYEVENTF_EXTENDEDKEY, (UIntPtr)0);
                        //turn capslock off (keyup)
                        keybd_event(VK_CAPSLOCK, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, (UIntPtr)0);
                        Console.SetCursorPosition(0, Defs.CurrentCursor[(int)CURSOR_LOC.TOP] + 2);
                        Log.Verbose("## CapsLock has been turned OFF ##", ConsoleColor.Red);
                        Thread.Sleep(1000);
                        Console.SetCursorPosition(0, Defs.CurrentCursor[(int)CURSOR_LOC.TOP] + 2);
                        Log.Verbose("## CapsLock has been turned OFF ##", ConsoleColor.Black);  //eraser
                        //let them see, caps lock has been turned off
                        Console.SetCursorPosition(PrevCursor[(int)CURSOR_LOC.LEFT], PrevCursor[(int)CURSOR_LOC.TOP] - 1);
                        General.SaveCursor();
                        return CheckPass(true);
                    }
                }

                bool catchBreak = false;
                do
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);

                    switch (key.Key)
                    {
                        case ConsoleKey.Escape:
                            PassWord = null;
                            retVal = false;
                            catchBreak = true;
                            break;
                        case ConsoleKey.Enter:
                            catchBreak = true;
                            break;
                        case ConsoleKey.Backspace:
                            if (PassWord.Length > 0)
                            {
                                PassWord = PassWord.Substring(0, (PassWord.Length - 1));
                                Console.Write("\b \b");
                            }
                            break;
                        default:
                            PassWord += key.KeyChar;
                            Console.Write("*");
                            break;
                    }
                } while (!catchBreak);
            }

            if (string.IsNullOrWhiteSpace(PassWord))
                retVal = false;

            return retVal;
        }

        /// <summary>
        /// Open the Configuration file for Edit in the configured text editor.
        /// </summary>
        internal static void EditConfig()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Defs.TextEditor;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = false;
            psi.Arguments = DataFile;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();
                process.WaitForExit();
            }
        }

        /// <summary>
        /// Show list of hosts used in the past if already accessed.
        /// </summary>
        /// <param name="refresh"></param>
        /// <returns></returns>
        internal static bool PromptForHosts(bool refresh = false)
        {
            bool retVal = true;
            bool exitLoops = false;
            //if hardcoded
            if (!string.IsNullOrWhiteSpace(Defs.HostName))
                return retVal;

            //get possible history of servers connected too...
            DataTable dt = GetHostData(refresh);

            //if any records exist
            if (dt != null)
            {
                bool hasRecord = false;
                bool hasUnreachable = false;

                //reset dictionary
                List<string> unreachable = new List<string>();

                string sort = "HostID ASC, HostName ASC, UserName ASC";
                if (!dt.Columns.Contains("HostID"))
                    sort = null;

                //add new column if not existing.
                if (!dt.Columns.Contains("Environment"))
                {
                    dt.Columns.Add("Environment");
                    ConfigData.UpdateTable(dt);
                }

                int maxHostNameSize = 0;
                int maxEnvSize = 0;
                int maxuserNameSize = 0;
                //get max size of each column and add 1.
                foreach (DataRow dr in dt.Select(null, sort))
                {
                    hasRecord = true;
                    string hostName = dr.Field<string>("HostName");
                    string userName = dr.Field<string>("UserName");
                    string env = dr.Field<string>("Environment");

                    if (maxHostNameSize < hostName.Length + 1)
                        maxHostNameSize = hostName.Length + 1;
                    if (maxuserNameSize < userName.Length + 1)
                        maxuserNameSize = userName.Length + 1;
                    if (maxEnvSize < env.Length + 1)
                        maxEnvSize = env.Length + 1;
                }

                //display records.
                foreach (DataRow dr in dt.Select(null, sort))
                {
                    bool canConnect = true;
                    string hostId = dr.Field<string>("HostID");
                    string hostName = dr.Field<string>("HostName");
                    string userName = dr.Field<string>("UserName");
                    string env = dr.Field<string>("Environment");
                    string[] hostInfo = hostName.Split(':');

                    int port = Defs.Port;
                    if (hostInfo.Length > 1)
                    {
                        if (!int.TryParse(hostInfo[1], out port))
                            port = Defs.Port;
                    }

                    if (!Processor.PingHost(hostName, port))
                    {
                        unreachable.Add(hostId);
                        canConnect = false;
                        hasUnreachable = true;
                    }

                    Log.Verbose($"[", ConsoleColor.Gray, false);
                    Log.Verbose($"{hostId}", canConnect ? ConsoleColor.Yellow : ConsoleColor.Red, false);
                    Log.Verbose($"]\t", ConsoleColor.Gray, false);
                    Log.Verbose($"{General.PadString(hostName, maxHostNameSize)}", canConnect ? ConsoleColor.Green : ConsoleColor.Red, false);
                    if (!string.IsNullOrWhiteSpace(env))
                        Log.Verbose($"- {General.PadString(env, maxEnvSize)}", canConnect ? ConsoleColor.Green : ConsoleColor.Red, false);
                    Log.Verbose($"- {General.PadString(userName, maxuserNameSize)}", ConsoleColor.Gray, true);
                }

                if (hasRecord)
                {
                    Console.CursorVisible = false;

                    Log.Verbose($"\n Select a host from above.\n - Press 'N' to add new Host\n - Press 'R' to refresh existing list.\n - Press 'E' to edit config.\n - Press 'D' to delete one host.\n - Press 'Escape' to exit {General.AppInfo.AppName}.\n", ConsoleColor.Yellow);
                    if (hasUnreachable)
                        Log.Verbose("\n #### Red Hosts above means host is unreachable. ####\n", ConsoleColor.Red);

                    string selection = "";

                    while (true)
                    {
                        //prompt for 1 - #;
                        ConsoleKeyInfo selectedKey = Console.ReadKey(true);
                        if (selection.Length == 1)
                        {
                            selection += selectedKey.KeyChar.ToString();

                            if (int.TryParse(selection, out _))
                            {
                                DataRow[] dr = dt.Select($"HostID='{selection}'", null);
                                if (dr.Length > 0 && !unreachable.Contains(selection))
                                {
                                    Defs.HostName = dr[0].Field<string>("HostName");
                                    if (Defs.HostName.IndexOf(':') > -1)
                                    {
                                        if (int.TryParse(Defs.HostName.Split(':')[1], out int port))
                                            Defs.Port = port;
                                        Defs.HostName = Defs.HostName.Split(':')[0];
                                    }
                                    Defs.UserName = dr[0].Field<string>("UserName");

                                    //new columns, may not exist, but as soone as it's set the first time, It will.                                
                                    if (dt.Columns.Contains("LocalPath"))
                                        Defs.LocalPath = dr[0].Field<string>("LocalPath").Trim();
                                    if (dt.Columns.Contains("RemotePath"))
                                        Defs.RemotePath = dr[0].Field<string>("RemotePath").Trim();
                                    if (dt.Columns.Contains("Environment"))
                                        Defs.HostEnv = dr[0].Field<string>("Environment").Trim();

                                    Processor.CreateDirectory(Defs.LocalPath);
                                    break;
                                }
                                else
                                    Log.Error($"[ {selection} ] not found.  Try a {(hasUnreachable ? "reachable host" : "valid")} # above.", 1);
                            }
                            else
                                Log.Error($"[ {selection} ] not found.  Try again.", 1);

                            //reset
                            selection = "";
                        }
                        else if (selectedKey.Key == ConsoleKey.Escape)
                        {
                            retVal = false;
                            exitLoops = true;
                        }
                        else
                        {
                            if (selectedKey.Key == ConsoleKey.N)
                            {
                                //lets add a new host.
                                break;
                            }
                            else if (selectedKey.Key == ConsoleKey.E)
                            {
                                //Edit the configuration file, then refresh the screen.
                                EditConfig();
                                General.ApplicationHeader(true);
                                retVal = PromptForHosts(true);
                                exitLoops = true;
                            }
                            else if (selectedKey.Key == ConsoleKey.R)
                            {
                                //Refresh the screen
                                General.ApplicationHeader(true);
                                retVal = PromptForHosts(true);
                                exitLoops = true;
                            }
                            else if (selectedKey.Key == ConsoleKey.D)
                            {
                                //lets delete a host.
                                selection = "";
                                Log.Verbose("Select # above to Delete from list: ", ConsoleColor.Green, false);
                                while (true)
                                {
                                    ConsoleKeyInfo deleteKey = Console.ReadKey(true);
                                    if (selection.Length == 1)
                                    {
                                        selection += deleteKey.KeyChar.ToString();
                                        //pull from json
                                        DataRow[] dr = dt.Select($"HostID='{selection}'", sort);
                                        if (dr.Length == 1)
                                        {
                                            //delete it
                                            dt.Rows.Remove(dr[0]);
                                            //update table
                                            ConfigData.UpdateTable(dt);
                                            //ensure json is saved.
                                            ConfigData.SaveData();
                                            //reset prompts
                                            PromptForHosts(true);
                                            exitLoops = true;
                                        }
                                        else
                                        {
                                            Log.Error($"[ {selection} ] not found.  Try again.", 1);
                                            selection = "";
                                        }
                                    }
                                    else if (!int.TryParse(deleteKey.KeyChar.ToString(), out int isKey))
                                        Log.Error($"[ {deleteKey.KeyChar} ] is invalid.");
                                    else
                                        selection += deleteKey.KeyChar.ToString();

                                    if (exitLoops)
                                        break;
                                }

                                //backup no extra row
                                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
                            }
                            else if (!int.TryParse(selectedKey.KeyChar.ToString(), out int isKey))
                                Log.Error($"[ {selectedKey.KeyChar} ] is invalid.");
                            else
                                selection += selectedKey.KeyChar.ToString();
                        }

                        if (exitLoops)
                            break;
                    }

                    Console.CursorVisible = true;
                }
            }

            General.ApplicationHeader(true);
            return retVal;
        }
    }
}
