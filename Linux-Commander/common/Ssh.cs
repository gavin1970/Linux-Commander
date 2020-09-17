using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace Linux_Commander.common
{
    class Ssh
    {
        //Looks for ?: just in case there is a prompt.   
        //Also looks for [user@server ~]# or [user@server ~]$
        //will pick up :
        //  what is your name?
        //  [root@localhost ~]#
        //  [root@localhost ~]$
        const string Command_Prompt_Only = @"[$#]|\[.*@(.*?)\][$%#]";
        const string Command_Prompt_Question = @".*\?:|" + Command_Prompt_Only;
        private object _sendLock = new object();

        private static DataTable _dataTranslation { get; set; } = null;
        private static SshClient _sshClient { get; set; } = null;
        private static ShellStream _shellStream { get; set; } = null;
        private static string _lastCommand { get; set; } = null;
        private static bool _displayResults { get; set; } = true;           // here so we can call a command and get data in memory, but not display it on the UI.
        private static EventWaitHandle TransComplete { get; set; } = new EventWaitHandle(false, EventResetMode.AutoReset);

        /// <summary>
        /// Not sure this works as we have not needed it.  
        /// However, if password is requested by server, it will pass what was used.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void HandleKeyEvent(object sender, AuthenticationPromptEventArgs e)
        {
            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    if (DataFiles.CheckPass())
                        prompt.Response = Defs.PassWord;
                }
            }
        }

        /// <summary>
        /// Primary Method for Command Prompt.
        /// </summary>
        public void ProptForCommands()
        {
            if (!DataFiles.PromptForHosts() || !SetupConnection())
            {
                Defs.Shutdown = true;
                return;
            }

            if (_dataTranslation == null)
                DataFiles.GetTranslationData();

            //change to the remote directory
            if (Defs.RemotePath.Length > 4)
            {
                SendCommand($"cd {Defs.RemotePath}", false);
                Defs.DataContent.Clear();
            }

            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);

            while (true)
            {
                if (Defs.Shutdown)
                    break;

                if (Defs.RequiresDirLookup)
                {
                    Regex regIP = new Regex(Defs.IP_REGEX);
                    if (regIP.IsMatch(Defs.HostName))
                        GetHostInfo();
                    if (Defs.PromptsRemoteDir == Defs.UserName)
                        Defs.PromptsRemoteDir = "~";

                    Log.Verbose($"[{Defs.UserName}@{Defs.HostNameShort} {Defs.PromptsRemoteDir}]$ ", Log.Prompt, false);
                }
                else if (!Log.IsPrompt)
                    Log.Verbose($"{Defs.UserServer} ", Log.Prompt, false);

                _lastCommand = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(_lastCommand))
                    continue;

                try
                {
                    switch (_lastCommand.ToLower().Trim())
                    {
                        case "exit":
                            Defs.Shutdown = true;
                            break;
                        case "legend":
                            General.ShowLegend();
                            break;
                        case "permissions":
                            General.ShowPermissionHelp();
                            break;
                        case "whoami":
                            Log.Verbose($"{Defs.UserName}\n", ConsoleColor.Yellow);
                            break;
                        case "cls":         //windows
                        case "clear":       //linux
                            General.ApplicationHeader(true);
                            break;
                        case "help":
                            General.DisplayHelp();
                            break;
                        case "-help":
                            SendCommand("help");
                            break;
                        default:
                            if (ExtendedCommands())
                                SendCommand(_lastCommand);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception - {ex.Message}", 1);
                }
            }

            Defs.Shutdown = true;
        }

        /// <summary>
        /// Ask Linux for server details.
        /// </summary>
        /// <returns></returns>
        private string GetHostInfo()
        {
            //get linux information for internal use..
            SendCommand("hostnamectl", false);
            string retVal = Defs.DataContent.ToString();
            Defs.DataContent.Clear();
            string[] host = retVal.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            foreach (string s in host)
            {
                if (!string.IsNullOrWhiteSpace(s) && s.IndexOf("Static hostname:") > -1)
                {
                    Defs.HostName = s.Split(':')[1].Trim();
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Setup SSH Connection to given Host.
        /// </summary>
        /// <returns></returns>
        private bool SetupConnection()
        {
            bool retVal = true;

            if (_sshClient == null || !_sshClient.IsConnected)
            {
                try
                {
                    //make sure a host exists, if not ask for one.
                    if (!DataFiles.CheckHost())
                        return false;

                    //make sure an environment exists, if not ask for one.
                    DataFiles.CheckEnv();

                    //make sure a username exists, if not ask for one.
                    if (!DataFiles.CheckUser())
                        return false;

                    //make sure a password exists, if not ask for one.
                    if (!DataFiles.CheckPass())
                        return false;

                    //display welcome text for application.
                    General.ApplicationHeader(true);

                    //let the user know what we are doing.
                    Log.Verbose("\n\n Setting authentication...", ConsoleColor.Yellow);

                    //account setup..
                    KeyboardInteractiveAuthenticationMethod kauth = new KeyboardInteractiveAuthenticationMethod(Defs.UserName);
                    PasswordAuthenticationMethod pauth = new PasswordAuthenticationMethod(Defs.UserName, Defs.PassWord);
                    kauth.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>(HandleKeyEvent);

                    //setup connection
                    ConnectionInfo connectionInfo = new ConnectionInfo(Defs.HostName, Defs.Port, Defs.UserName, pauth, kauth);
                    connectionInfo.Timeout = Defs.ConnectionTimeout;

                    //pass connection data
                    _sshClient = new SshClient(connectionInfo);

                    //let the user know what we are doing.
                    Log.Verbose("Attempting to connect...", ConsoleColor.Yellow);

                    //connect to linux
                    _sshClient.Connect();

                    //let the user know what we are doing.
                    Log.Verbose("Connected, creating shell stream...", ConsoleColor.Green);

                    //lets build out a stream that will each back what we request.
                    var terminalMode = new Dictionary<TerminalModes, uint>();
                    //terminalMode.Add(TerminalModes.ECHO, 53);

                    //create shell stream
                    _shellStream = _sshClient.CreateShellStream("input", 255, 50, 400, 600, 4096, terminalMode);

                    //keep track of remote directory.  Can be useful, in case response of User@Server isn't configured to return.
                    SendCommand("pwd -P", false);
                    //it is possible that sometimes Messages come back on first connection to linux machine, before the information.
                    if (Defs.DataContent.Length > 50)
                    {
                        Defs.DataContent.Clear();
                        SendCommand("pwd -P", false);
                    }
                    //break it down for prompt.
                    string[] remote = Defs.DataContent.ToString().Replace(Environment.NewLine, "").Split('/');
                    Defs.DataContent.Clear();

                    //Don't set prompt if still showing message.
                    if (string.Join("/", remote).Trim().Length < 40 && remote.Length <= 3)
                        Defs.PromptsRemoteDir = remote[remote.Length - 1].Trim();

                    //if default Remote Path not setup, give it the current folder.  don't set remote path if still showing message.
                    if (Defs.RemotePath.Equals("/") && string.Join("/", remote).Trim().Length < 40 && remote.Length <= 3)
                        Defs.RemotePath = string.Join("/", remote).Trim();

                    //save host and user to history.
                    if (Defs.NewHost)
                        DataFiles.SaveHostHistory();

                    //let the user know what we are doing.
                    Log.Verbose("Successful Connection Established...\n", ConsoleColor.Green);

                    //set the current folder as local path 
                    Directory.SetCurrentDirectory(Defs.LocalPath);
                    Console.Title = $"{Defs.ConsoleTitle}     -=[{Defs.UserName}@{Defs.HostName}]=-";

                    //clear screen and display new header 
                    General.ApplicationHeader(true);

                    //let the user know what's configured.
                    Log.Verbose($"Local Path has been set to {Defs.LocalPath}...", ConsoleColor.Yellow);
                    Log.Verbose($"Remote Path has been set to {Defs.RemotePath}...\n", ConsoleColor.Yellow);
                    Log.Verbose($"Change these paths with \"set-local\" and \"set-remote\"...\n\n", ConsoleColor.Yellow);
                }
                catch (Exception ex)
                {
                    retVal = false;
                    Log.Error($"Exception - {ex.Message}", 1);
                    Log.Verbose("Press any key to continues.", ConsoleColor.White);
                    Console.ReadKey();
                    //connection didn't work right, shut it down.
                    Defs.Shutdown = true;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Disconnect from host.
        /// </summary>
        private void Disconnect()
        {
            Defs.HostName = null;
            Defs.UserName = null;
            Defs.PassWord = null;

            Log.Verbose("Disonnecting Shell from Linux server", ConsoleColor.Yellow);
            if (_shellStream != null)
                _shellStream.Close();

            Log.Verbose("Disonnecting SSH from Linux server", ConsoleColor.Yellow);
            if (_sshClient != null && _sshClient.IsConnected)
                _sshClient.Disconnect();

            Log.Verbose("Disconnected Successful", ConsoleColor.Yellow);
        }

        /// <summary>
        /// Polymorphic method
        /// </summary>
        /// <param name="command"></param>
        /// <param name="displayResults"></param>
        /// <param name="timeOutSec"></param>
        /// <param name="minWaitSec"></param>
        /// <returns></returns>
        private bool SendCommand(string command, bool displayResults = true, int timeOutSec = 10, int minWaitSec = 0)
        {
            return SendCommand(Command_Prompt_Question, command, displayResults, timeOutSec, minWaitSec);
        }

        /// <summary>
        /// Send a command to linux, with a timeout option.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="autoPromptAnswers"></param>
        /// <param name="displayResults"></param>
        /// <param name="timeOutSec"></param>
        /// <param name="minWaitSec"></param>
        /// <returns></returns>
        private bool SendCommand(string expect, string command, bool displayResults = true, int timeOutSec = 10, int minWaitSec = 0)
        {
            bool retVal = true;
            bool pwd = false;
            //we don't want during DST changes, to mess this up.
            DateTime startTime = DateTime.UtcNow;

            //in case you get them backwards, we will reset them.
            if (minWaitSec >= timeOutSec)
            {
                minWaitSec = 0;
                timeOutSec = 10;
            }

            //save for thread to know to display results or save to internal use.
            _displayResults = displayResults;
            command = command.Trim();

            //remove any special character off the end.
            if (command.EndsWith("\\"))
                command = command.Substring(0, command.Length - 1);

            //we want to get the new full remote path, if change directory is being called.
            if (command.StartsWith("cd "))
                pwd = true;

            //do one send at a time and wait for response, before sending another.
            lock (_sendLock)
            {
                try
                {
                    _lastCommand = command;

                    //prep for threading.
                    TransComplete.Reset();

                    //we had an issue with timeout, not working with expect.  This is why we thread it.
                    Thread t = new Thread(() => ThreadProc(expect, TimeSpan.FromSeconds(timeOutSec)));
                    t.Start();

                    DateTime endTime = startTime;
                    //set min time.
                    while (endTime.Subtract(startTime).TotalSeconds <= minWaitSec)
                    {
                        retVal = true;
                        //wait for thread to finish or timeout.
                        if (!TransComplete.WaitOne(TimeSpan.FromSeconds(timeOutSec)))
                            retVal = false;

                        endTime = DateTime.UtcNow;
                        TransComplete.Reset();
                    }

                    //lets wait until thread closes property.
                    while (t != null && t.IsAlive)
                        Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Thread abort"))
                        Log.Error($"Exception: - {ex.Message}", 1);
                    retVal = false;
                }
            }

            //keeping track of existing remote folder.
            if (pwd)
            {
                bool prevDisplayRessults = displayResults;
                string prevDataContent = Defs.DataContent.ToString();
                SendCommand("pwd", false);
                Defs.CurrentRemotePath = Defs.DataContent.ToString().Replace("\n", "").Replace("\r", "").Trim();

                //legacy linux
                if (Defs.CurrentRemotePath.EndsWith("$"))
                    Defs.CurrentRemotePath = Defs.CurrentRemotePath.Substring(0, Defs.CurrentRemotePath.Length - 1);

                Defs.DataContent.Clear();
                Defs.DataContent.Append(prevDataContent);
                _displayResults = prevDisplayRessults;
            }

            return retVal;
        }

        /// <summary>
        /// Validate if the existing command is an internal commands or a user defined translation command.
        /// </summary>
        /// <returns></returns>
        private bool ExtendedCommands()
        {
            bool callExecute = false;
            string[] command = _lastCommand.Split(' ');

            if (command[0].Equals("local-dir"))
            {
                string filePath = null;

                //if path is being passed.
                if (command.Length > 1)
                {
                    filePath = command[1];
                    for (int i = 2; i < command.Length; i++)
                        filePath += command[i];
                }

                //display files and folders within selected path, set-local path if no path passed.
                Processor.ShowLocalDirectory(filePath);
            }
            else if (command[0].Equals("edit"))
            {
                //make sure file name is passed.
                if (command.Length > 1)
                    //download, open in editor, then upload back to where it came from.
                    Processor.EditFile(command[1]);
            }
            else if (command[0].Equals("edit-config"))
            {
                //opens config in editor, then reload config.
                DataFiles.EditConfig();
                DataFiles.LoadConfig(true);
            }
            else if (command[0].Equals("about"))
            {
                //display all information about the application and all information about the remote server your connected too.
                Log.Verbose($"-=#[ {General.AppInfo.AppName} Information ]#=-", ConsoleColor.DarkCyan);
                foreach (var field in typeof(APP_INFO).GetFields(BindingFlags.Instance |
                                                                 BindingFlags.NonPublic |
                                                                 BindingFlags.Public))
                {
                    Log.Verbose($"{General.PadString(field.Name.Trim() + ": ", 20, true)}", ConsoleColor.Cyan, false);
                    Log.Verbose(0, $"{field.GetValue(General.AppInfo)}", ConsoleColor.DarkYellow);
                }

                Log.Verbose("\n");
                Log.Verbose($"-=#[ {Defs.HostName} Information ]#=-", ConsoleColor.DarkCyan);
                _lastCommand = "whereami --more";
                ExtendedCommands();
            }
            else if (command[0].Equals("new-host"))
            {
                //disconnect from remote host.
                Disconnect();
                Defs.MissingHeaderShown = false;

                //lets go back prompt for all previous hosts or add new host.
                General.ApplicationHeader(true);
                if (DataFiles.PromptForHosts())
                {
                    //set connection based on prompts.
                    if (!SetupConnection())
                        Defs.Shutdown = true;
                }
                else
                    Defs.Shutdown = true;
            }
            else if (command[0].Equals("sync-date"))
            {
                //set the remote linux machine date/time to the same as your local windows machine.
                callExecute = true;
                string date = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
                _lastCommand = $"date -s \"{date}\"";
            }
            else if (command[0].Equals("recon"))
            {
                //reconnect to the server, you might of gotten disconnected from.
                General.ApplicationHeader(true);
                SetupConnection();
            }
            else if (command[0].Equals("install-ansible"))
            {
                //install PIP, and Ansible-Playbook
                InstallAnsibleWithPIP();
            }
            else if (command[0].Equals("clear-local"))
            {
                //reset local path to default.
                Defs.LocalPath = $"{General.AppInfo.AppDIR}\\Ansible";

                if (!Directory.Exists(Defs.LocalPath))
                    Processor.CreateDirectory(Defs.LocalPath);

                Log.Verbose($"-------------\n {Defs.LocalPath} has been set...\n", ConsoleColor.Yellow);
            }
            else if (command[0].Equals("publish"))
            {
                //uploads all files/folders from set-local and saves it to set-remote path.
                Publish();
            }
            else if (command[0].Equals("refresh"))
            {
                //downloads all files/folders from set-remote and saves it to set-local path.
                RefreshLocal();
            }
            else if (command[0].Equals("whereami"))
            {
                //display some information of server, port and curent directory.
                bool more = _lastCommand.IndexOf("--more") > -1 ? true : false;
                SendCommand("pwd", false);
                Log.Verbose($"{General.PadString("Server: ", 20, true)}", ConsoleColor.Cyan, false);
                Log.Verbose(0, $"{Defs.HostName}", ConsoleColor.DarkYellow);
                Log.Verbose($"{General.PadString("Port: ", 20, true)}", ConsoleColor.Cyan, false);
                Log.Verbose(0, $"{Defs.Port}", ConsoleColor.DarkYellow);
                Log.Verbose($"{General.PadString("Directory: ", 20, true)}", ConsoleColor.Cyan, false);
                Log.Verbose(0, $"{Defs.DataContent.Replace(Environment.NewLine, "")}", ConsoleColor.DarkYellow);
                Defs.DataContent.Clear();

                if (more)
                {
                    string[] info = GetHostInfo().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (string s in info)
                        Log.Verbose(s, ConsoleColor.Yellow);
                }
            }
            else if (command[0].Equals("sets"))
            {
                //lets display what has been set.
                Log.Verbose($"-------------", ConsoleColor.Green);
                Log.Verbose($"Current: {Defs.LocalPath}", ConsoleColor.Yellow);
                Log.Verbose($"Change by using: 'set-local <drive>:\\<dir>\\<dir>'\n", ConsoleColor.Yellow);

                Log.Verbose($"-------------", ConsoleColor.Green);
                Log.Verbose($"Current: {Defs.RemotePath}", ConsoleColor.Yellow);
                Log.Verbose($"Change by using: 'set-remote /<dir>/<dir>'\n", ConsoleColor.Yellow);
            }
            else if (command[0].Equals("shell") || command[0].Equals("set"))
            {
                //since this is a virual UI, shell and set, holds up the command.  We don't want 
                //to use these.  There might be more, but at the moment, these are a known issue.
                //eventually, we will put all commands that cause hang up, into a List<string>.
                Log.Error($"{General.AppInfo.AppName} cannot use the '{command[0]}' at this time.");
            }
            else if (command[0].Equals("get-file"))
            {
                //if file name is being passed.
                if (command.Length > 1)
                {
                    //pulls from from existing remote dir and saves to set-local path.
                    Processor.GetFile(command[1]);
                    Log.Verbose("");
                }
                else
                {
                    General.DisplayHelp("get-file");
                }
            }
            else if (command[0].Equals("send-file"))
            {
                //if file name is being passed.
                if (command.Length > 1)
                {
                    //build possible path with spaces and file name.
                    string filePath = command[1];
                    for (int i = 2; i < command.Length; i++)
                        filePath += command[i];

                    //send file to current folder.
                    string toFile = Processor.SendFile(filePath, Defs.CurrentRemotePath);
                    if (toFile.Length > 0)
                    {
                        //set permission to remote file to rwxr-xr-x
                        SendCommand($"chmod 755 {toFile}", false);
                        Defs.DataContent.Clear();
                    }
                    Log.Verbose("");
                }
                else
                {
                    General.DisplayHelp("send-file");
                }
            }
            else if (command[0].Equals("set-local"))
            {
                //if path behind set-local
                if (command.Length > 1)
                {
                    //lets build the dir, just incase there are spaces.
                    string dir = command[1];
                    for (int i = 2; i < command.Length; i++)
                        dir += $" {command[i]}";

                    //strip slash on the end, if exists.
                    if (dir.EndsWith("\\"))
                        dir = dir.Substring(0, dir.Length - 1);

                    //verify dir exists
                    if (Directory.Exists(dir))
                    {
                        //set path as requested.
                        Defs.LocalPath = dir.Trim();
                        //save to json for future access to this server.
                        DataFiles.SaveHostHistory();
                        Log.Verbose($"-------------\n Local path '{Defs.LocalPath}' has been set...\n", ConsoleColor.Yellow);
                    }
                    else
                        Log.Error($"{dir} does not exist.", 0);

                    Log.Verbose("");
                }
                else
                {
                    General.DisplayHelp("set-local");
                }
            }
            else if (command[0].Equals("set-remote"))
            {
                //if path behind set-remote
                if (command.Length > 1)
                {
                    //lets build the dir, just in case there are spaces
                    string dir = command[1];
                    for (int i = 2; i < command.Length; i++)
                        dir += command[i];

                    //strip slash on end, if exists.
                    if (dir.EndsWith("/"))
                        dir = dir.Substring(0, dir.Length - 1);

                    if (!dir.StartsWith("/"))
                        Log.Error($"{dir} must have a slash (on the front)", 0);
                    else
                    {
                        //set path as requested.
                        Defs.RemotePath = dir.Trim();
                        //save to json for future access to this server.
                        DataFiles.SaveHostHistory();
                        Log.Verbose($"-------------\n Remote path '{Defs.RemotePath}' has been set...\n", ConsoleColor.Yellow);
                    }

                    Log.Verbose("");
                }
                else
                {
                    General.DisplayHelp("set-remote");
                }
            }
            else if (command[0].Equals("cat"))
            {
                //When calling CAT w/o parameters, no prompt is coming back.
                if (command.Length > 1)
                    callExecute = true;
                else
                    Log.Verbose("The command 'cat' requires a filename.  e.g. cat [FILE_NAME]");
            }
            else if (command[0].Equals("view"))
            {
                //When calling CAT w/o parameters, no prompt is coming back.
                if (command.Length > 1)
                {
                    _lastCommand = $"cat {_lastCommand.Substring(5).Trim()}";
                    callExecute = true;
                }
                else
                    Log.Verbose("The command 'cat' requires a filename.  e.g. cat [FILE_NAME]");
            }
            else
            {
                callExecute = true;

                //if calling LS, lets show the user a legend for the colors exist.
                if (command[0].Equals("dir"))
                    Log.Verbose("\n Type 'legend' for more information on colors.\n", ConsoleColor.Yellow);

                //if calling ansible-playbook, clear the screen, so we see results on new screen.
                if (command[0].Equals("ap"))
                    General.ApplicationHeader(true);

                if (_dataTranslation == null)
                    _dataTranslation = DataFiles.GetTranslationData();

                //lets get any possible tanslation
                DataRow[] rows = _dataTranslation.Select($"Typed='{command[0]}'");
                if (rows != null && rows.Length == 1)
                {
                    //get first row, which only should be one row.
                    DataRow dr = rows[0];

                    //see what to translate too.
                    if (DataFiles.GetField<string>(dr, "ChangeTo", out string changeTo))
                    {
                        //pull any options.
                        if (!DataFiles.GetField<string>(dr, "Options", out string options))
                            options = "";

                        //process command request.
                        TranslateUserCommand(command[0], changeTo, options);
                    }
                }
            }

            return callExecute;
        }

        /// <summary>
        /// Take the set-local path and upload it to the set-remote path.
        /// </summary>
        private void Publish()
        {
            //make sure the local and the remote and pointing at root.
            if (Defs.LocalPath.Length > 3 && Defs.RemotePath.Length > 1)
            {
                //upload set-local to set-remote folder.
                if (Processor.SendDirectory(Defs.LocalPath, Defs.RemotePath, true))
                {
                    //set permissions to rwxr-xr-x
                    SendCommand($"chmod 755 -R {Defs.RemotePath}", false);
                    //clear screen, show header
                    General.ApplicationHeader(true);
                    Log.Verbose($"'{Defs.LocalPath}' has been uploaded to '{Defs.RemotePath}'.\n", ConsoleColor.Yellow);
                    Log.Verbose($"List of files in {Defs.RemotePath}", ConsoleColor.Yellow);
                    SendCommand($"ls -ltr {Defs.RemotePath}");
                }
            }
            else
            {
                Log.Error($"Your local path is set to {Defs.LocalPath}.\n Please change using \"set-local\", before attempting\n to upload a whole drive and folders to Linux.");
            }
        }

        /// <summary>
        /// Get all files and folders from set-remote and pull down to set-local
        /// </summary>
        private void RefreshLocal()
        {
            //make sure the local and the remote and pointing at root.
            if (Defs.LocalPath.Length > 3 && Defs.RemotePath.Length > 1)
            {
                int lastSlash = Defs.LocalPath.LastIndexOf("\\");
                string lastFolderName = Defs.LocalPath.Substring(lastSlash, Defs.LocalPath.Length - lastSlash);
                string local = Defs.LocalPath.Substring(0, lastSlash);
                string remote = Defs.RemotePath + "/" + lastFolderName.Replace("\\", "/");

                //pull set-remote to set-local folder.
                if (Processor.SendDirectory(remote, local, false))
                {
                    General.ApplicationHeader(true);
                    Log.Verbose($"'{remote}' has been downloaded to '{local}'.\n", ConsoleColor.Yellow);
                    Log.Verbose($"List of files in {local}", ConsoleColor.Yellow);
                    Processor.ShowLocalDirectory(Defs.LocalPath);
                }
            }
            else
            {
                Log.Error($"Your local path is set to {Defs.LocalPath}.\n Please change using \"set-local\", before attempting\n to download to a root drive.");
            }
        }

        /// <summary>
        /// Install PIP and Ansible
        /// </summary>
        private void InstallAnsibleWithPIP()
        {
            General.ApplicationHeader(true);

            Log.Verbose($"{General.PadString("[ .. ] Installing Python3.", 30)}", ConsoleColor.White, false);
            Console.CursorLeft = 3;
            if (SendCommand("sudo yum install python3-pip", false, 60))
                Log.Verbose(0, $"OK", ConsoleColor.Green);
            else
                Log.Verbose(0, $"TO", ConsoleColor.Red);

            Log.Verbose($"{General.PadString("[ .. ] Downloading get-pip.py.", 30)}", ConsoleColor.White, false);
            Console.CursorLeft = 3;
            if (SendCommand("curl https://bootstrap.pypa.io/get-pip.py -o get-pip.py", false, 60))
                Log.Verbose(0, $"OK", ConsoleColor.Green);
            else
                Log.Verbose(0, $"TO", ConsoleColor.Red);

            Log.Verbose($"{General.PadString("[ .. ] Installing PIP.", 30)}", ConsoleColor.White, false);
            Console.CursorLeft = 3;
            if (SendCommand("Successfully installed", "python3 get-pip.py", false, 60))
                Log.Verbose(0, $"OK", ConsoleColor.Green);
            else
                Log.Verbose(0, $"TO", ConsoleColor.Red);

            Log.Verbose($"{General.PadString("[ .. ] Installing pexpect for python.", 30)}", ConsoleColor.White, false);
            Console.CursorLeft = 3;
            if (SendCommand("pip install pexpect", false, 30))
                Log.Verbose(0, $"OK", ConsoleColor.Green);
            else
                Log.Verbose(0, $"TO", ConsoleColor.Red);

            Log.Verbose($"{General.PadString("[ .. ] PIP Installing Ansible.", 30)}", ConsoleColor.White, false);
            Console.CursorLeft = 3;
            if (SendCommand("(Successfully built ansible|satisfied: pycparser)", "pip install ansible", false, 90))
                Log.Verbose(0, $"OK", ConsoleColor.Green);
            else
                Log.Verbose(0, $"TO", ConsoleColor.Red);

            Log.Verbose($"{General.PadString("[ .. ] Deleting get-pip.py.", 30)}", ConsoleColor.White, false);
            Console.CursorLeft = 3;
            if (SendCommand("rm -f get-pip.py", false))
                Log.Verbose(0, $"OK", ConsoleColor.Green);
            else
                Log.Verbose(0, $"TO", ConsoleColor.Red);

            Log.Verbose("Finished..\n", ConsoleColor.White);
            Log.Verbose("Press any key to continue...", ConsoleColor.White);
            Console.ReadKey();

            General.ApplicationHeader(true);
            SendCommand("pip --version", true);
            SendCommand("python3 --version", true);
            SendCommand("pip freeze", true);
        }

        /// <summary>
        /// Translate user commands into Linux commands.
        /// </summary>
        /// <param name="usedCommand"></param>
        /// <param name="actualCommand"></param>
        /// <param name="defaultOptions"></param>
        private void TranslateUserCommand(string usedCommand, string actualCommand, string defaultOptions = "")
        {
            string options = "";
            string otherParams = "";
            string command = _lastCommand.Substring(usedCommand.Length, _lastCommand.Length - usedCommand.Length).Trim();
            string[] sp = command.Split(' ');

            foreach (string param in sp)
            {
                if (param.StartsWith("-"))
                {
                    if (options.Length > 0)
                        options += " ";
                    options += param;
                }
                else
                {
                    if (otherParams.Length > 0)
                        otherParams += " ";
                    otherParams += param;
                }
            }

            if (options.Length == 0 && defaultOptions.Length > 0)
                options = defaultOptions;

            _lastCommand = $"{actualCommand} {options} {otherParams}";
        }

        /// <summary>
        /// Thread used to send command.   
        /// SSH.NET has a Expect() timeout for their command call and it didn't seem to ever timeout.  
        /// So we threaded it, so we can hold our own timer.
        /// UPDATE: Timeout seems to be working again, but I'm going to leave it as a thread, just in case.
        /// </summary>
        /// <param name="autoPromptAnswers"></param>
        private static void ThreadProc(string expect, TimeSpan timeOut)
        {

            var promptRegex = new Regex(expect);

            try
            {
                //clear data that might still be in the stream from last time.
                while (_shellStream.DataAvailable)
                    _shellStream.Read();
                _shellStream.Flush();

                //straight away 100% works.
                _shellStream.WriteLine(_lastCommand);
                var output = _shellStream.Expect(promptRegex, timeOut);

                while (_shellStream.DataAvailable)
                {
                    string data = _shellStream.Read();

                    //if output is null or empty, and data has info, use it instead.
                    if (string.IsNullOrWhiteSpace(output) && !string.IsNullOrWhiteSpace(data))
                        output = data;

                    //lets look for something specific
                    var matches = Regex.Matches(output, expect);
                    Log.IsPrompt = false;

                    foreach (Match match in matches)
                    {
                        if (match.ToString().Length > 1)
                        {
                            Defs.UserServer = match.ToString();
                            output = output.Replace(Defs.UserServer, "");
                        }
                        else if (match.ToString().Contains("?"))
                            Log.IsPrompt = true;
                    }

                    //looking for a bunch of ******* and trim it down to a max of 75 bytes.
                    //this is something ansible ymls return with the use of ansible-playbook.
                    int maxSize = 75;
                    string regPattern = @"(.)\1{" + maxSize.ToString() + ",}";

                    var repeatedChar = Regex.Matches(output, regPattern);
                    foreach (Match match in repeatedChar)
                    {
                        string captured = match.ToString();
                        char[] bytes = captured.ToCharArray();
                        output = output.Replace(captured, new string(bytes[0], maxSize));
                    }

                    //I've seen both line feed \n and a carrage return line feed \r\n
                    //I've also seen when the output has a space in front of the command. ???????
                    //we don't want to regex this as we don't want all exiting words that is a 
                    //command and could be something else replaced.
                    if (output.StartsWith($"{_lastCommand}\n"))
                        output = output.Substring($"{_lastCommand}\n".Length);
                    if (output.StartsWith($"{_lastCommand}\r\n"))
                        output = output.Substring($"{_lastCommand}\r\n".Length);
                    if (output.StartsWith($" {_lastCommand}\n"))
                        output = output.Substring($" {_lastCommand}\n".Length);
                    if (output.StartsWith($" {_lastCommand}\r\n"))
                        output = output.Substring($" {_lastCommand}\r\n".Length);

                    if (_displayResults)
                        Log.Verbose(output);
                    else
                        Defs.DataContent.Append(output);

                    output = "";
                }

                //clears all buffers, we should be done.
                _shellStream.Flush();
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("Thread was being aborted.", StringComparison.OrdinalIgnoreCase) == -1)
                    Log.Error(ex.Message);
            }

            _displayResults = false;
            TransComplete.Set();
        }
    }
}
