using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Linux_Commander.common
{
    class Processor
    {
        /// <summary>
        /// Create a directory from the ground up.
        /// </summary>
        /// <param name="dirPath"></param>
        internal static void CreateDirectory(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath))
                {
                    string fullPath = "";
                    foreach (string dir in dirPath.Split('\\'))
                    {
                        if (fullPath.Length > 0)
                            fullPath += "\\";

                        fullPath += dir;

                        if (fullPath.Length > 3 && !Directory.Exists(fullPath))
                            Directory.CreateDirectory(fullPath);
                    }
                }
            }
            catch (IOException ex)
            {
                Log.Error($"IO Exception: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"General Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate a host exists, if pingable.
        /// </summary>
        /// <param name="hostUri"></param>
        /// <param name="portNumber"></param>
        /// <returns></returns>
        internal static bool PingHost(string hostUri, int portNumber)
        {
            bool retVal = true;
            using (TcpClient tcp = new TcpClient())
            {
                IAsyncResult ar = tcp.BeginConnect(hostUri, portNumber, null, null);
                WaitHandle wh = ar.AsyncWaitHandle;
                try
                {
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3), false))
                    {
                        tcp.Close();
                        retVal = false;
                    }

                    if (retVal)
                        tcp.EndConnect(ar);
                }
                catch (SocketException ex)
                {
                    retVal = false;
                    Log.Error(ex.Message);
                }
                finally
                {
                    wh.Close();
                }
            }

            return retVal;
        }

        /// <summary>
        /// Display the contents of the local path set by [set-local]
        /// </summary>
        /// <param name="localDir"></param>
        internal static void ShowLocalDirectory(string localDir = null)
        {
            if (localDir == null)
                localDir = Defs.LocalPath;

            Log.DrawLine(50, ConsoleColor.DarkYellow);

            DriveInfo di = new DriveInfo(localDir);
            Log.Verbose($"{General.PadString("Drive:", 20)}{di.Name}", ConsoleColor.Cyan);
            Log.Verbose($"{General.PadString("Volume:", 20)}{di.VolumeLabel}", ConsoleColor.Yellow);
            Log.Verbose($"{General.PadString("Total Size:", 20)}{General.SizeSuffix(di.TotalSize)}", ConsoleColor.Cyan);
            Log.Verbose($"{General.PadString("Total Free Space:", 20)}{General.SizeSuffix(di.TotalFreeSpace)}", ConsoleColor.Yellow);

            // Get the root directory and print out some information about it.
            DirectoryInfo dirInfo = new DirectoryInfo(localDir);
            Log.Verbose($"{General.PadString("Directory:", 20)}{localDir}", ConsoleColor.Cyan);

            // Get the subdirectories directly that is under the root.
            // See "How to: Iterate Through a Directory Tree" for an example of how to
            // iterate through an entire tree.
            DirectoryInfo[] dirInfos = dirInfo.GetDirectories("*.*");
            // Get the files in the directory and print out some information about them.
            FileInfo[] fileNames = dirInfo.GetFiles("*.*");

            Log.DrawLine(50, ConsoleColor.DarkYellow);

            Log.Verbose("Count:", ConsoleColor.Yellow);
            Log.Verbose($"{General.PadString("   Directories", 20)}{dirInfos.Length}", ConsoleColor.Cyan);
            Log.Verbose($"{General.PadString("   Files:", 20)}{fileNames.Length}", ConsoleColor.Yellow);

            Log.Verbose("", ConsoleColor.Black);

            Log.Verbose($"{General.PadString("Directory", 30)}{General.PadString("Created Date", 30)}{General.PadString("Modified Date", 30)}", ConsoleColor.Green);
            Log.DrawLine(100, ConsoleColor.Green);

            foreach (DirectoryInfo d in dirInfos)
            {
                Log.Verbose($"{General.PadString(d.Name, 30)}", ConsoleColor.Cyan, false);
                Log.Verbose(0, $"{General.PadString(d.CreationTime.ToString(), 30)}{General.PadString(d.LastWriteTime.ToString(), 30)}", ConsoleColor.Gray);
            }

            Log.Verbose("\n", ConsoleColor.Black);

            Log.Verbose($"{General.PadString("Filename", 30)}{General.PadString("Created Date", 30)}{General.PadString("Modified Date", 30)}Size", ConsoleColor.Green);
            Log.DrawLine(100, ConsoleColor.Green);

            foreach (FileInfo fi in fileNames)
            {
                Log.Verbose($"{General.PadString(fi.Name, 30)}", ConsoleColor.Yellow, false);
                Log.Verbose(0, $"{General.PadString(fi.CreationTime.ToString(), 30)}{General.PadString(fi.LastWriteTime.ToString(), 30)}{General.SizeSuffix(fi.Length)}", ConsoleColor.Gray);
            }

            if (fileNames.Length > 0)
                Log.Verbose("", ConsoleColor.Black);
        }

        /// <summary>
        /// Load the local file into a clonfigured text editor
        /// </summary>
        /// <param name="fileName"></param>
        internal static void ViewFile(string fileName)
        {
            if (fileName.IndexOf(":\\") > -1 && File.Exists(fileName))
                Process.Start(Defs.TextEditor, fileName);
            else
                Log.Error($"'{fileName}' does not exist.", 1);
        }

        /// <summary>
        /// pull file from server.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static bool GetFile(string fileName)
        {
            bool retVal = true;

            try
            {
                string fromFile = $"{Defs.UserName}@{Defs.HostName}:{Defs.CurrentRemotePath}/{fileName}";
                string toPath = $"{Defs.LocalPath}\\{fileName}";

                if (File.Exists(toPath))
                    File.Delete(toPath);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "pscp",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    Arguments = $"-r -p -pw {Defs.PassWord} {fromFile} \"{toPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                General.PSCPRequired(ex.Message);
                retVal = false;
            }

            return retVal;
        }

        /// <summary>
        /// Send a file to a remote server.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="dirDiff"></param>
        /// <returns></returns>
        internal static string SendFile(string filePath, string dirDiff)
        {
            string toFile = "";

            if (filePath.Length >= 4 && filePath.Substring(1, 2) == ":\\")
            {
                if (!File.Exists(filePath))
                {
                    Log.Error($"{filePath} does not exist.");
                    return toFile;
                }
            }
            else
            {
                filePath = $"{Defs.LocalPath}\\{filePath}";
            }

            if (dirDiff.StartsWith("/"))
                toFile = $"{dirDiff}/{Path.GetFileName(filePath)}".Replace("//", "/");
            else
                toFile = $"{Defs.RemotePath}/{dirDiff}/{Path.GetFileName(filePath)}".Replace("//", "/");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "pscp",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    Arguments = $"-r -p -pw {Defs.PassWord} \"{filePath}\" {Defs.UserName}@{Defs.HostName}:{toFile}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                //pscp -r -p -pw <PASSWORD> "C:\Code\Ansible\readme.md" root@192.168.179.128:/root/Code/readme.md

                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    process.StandardInput.WriteLine("Y");
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                toFile = "";
                General.PSCPRequired(ex.Message);
            }

            Defs.DataContent.Clear();

            return toFile;
        }

        // Process all files in the directory passed in, recurse on any directories
        // that are found, and process the files they contain.
        internal static bool SendDirectory(string sourceDirectory, string targetDirectory, bool sourceIsLocal = true)
        {
            bool retVal = false;
            if ((sourceIsLocal && !Directory.Exists(sourceDirectory)) || (!sourceIsLocal && !Directory.Exists(targetDirectory)))
            {
                string dir = sourceIsLocal ? sourceDirectory : targetDirectory;
                string exMessage = (dir == Defs.LocalPath) ? "Change your local directory with 'set-local'." : "";
                Log.Error($"'{dir}' does not exist.  {exMessage}");
                return retVal;
            }

            string src;
            string dest;

            if (sourceIsLocal)
            {
                src = $"\"{sourceDirectory}\"";
                dest = $"{Defs.UserName}@{Defs.HostName}:{targetDirectory}";
            }
            else
            {
                src = $"{Defs.UserName}@{Defs.HostName}:{sourceDirectory}";
                dest = $"\"{targetDirectory}\"";
            }

            //pscp -r -p -pw <PASSWORD> "C:\Code\Ansible\readme.md" root@192.168.179.128:/root/Code/readme.md
            //pscp -r -p -pw <PASSWORD> "C:\Code\Ansible" root@192.168.179.128:/root/Code

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "pscp",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    Arguments = $"-r -p -pw {Defs.PassWord} {src} {dest}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = psi;
                    process.Start();
                    process.StandardInput.WriteLine("Y");
                    process.WaitForExit();
                }

                Defs.DataContent.Clear();

                retVal = true;
            }
            catch (Exception ex)
            {
                General.PSCPRequired(ex.Message);
            }

            return retVal;
        }

        // Process all files in the directory passed in, recurse on any directories
        // that are found, and process the files they contain.
        internal static bool EditFile(string fileName)
        {
            bool retVal = false;

            string tempFolder = $"{General.AppInfo.AppDIR}\\temp";
            string remote;
            string local;

            local = $"{tempFolder}\\{fileName}";
            remote = $"{Defs.UserName}@{Defs.HostName}:{Defs.CurrentRemotePath}/{fileName}";

            //pscp -r -p -pw <PASSWORD> "C:\Code\Ansible\readme.md" root@192.168.179.128:/root/Code/readme.md
            //pscp -r -p -pw <PASSWORD> "C:\Code\Ansible" root@192.168.179.128:/root/Code

            try
            {
                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);

                ProcessStartInfo getPSI = new ProcessStartInfo
                {
                    FileName = "pscp",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    Arguments = $"-r -p -pw {Defs.PassWord} {remote} \"{local}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                ProcessStartInfo sendPSI = new ProcessStartInfo
                {
                    FileName = "pscp",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    Arguments = $"-r -p -pw {Defs.PassWord} \"{local}\" {remote}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                ProcessStartInfo editFile = new ProcessStartInfo
                {
                    FileName = Defs.TextEditor,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    Arguments = $"\"{local}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = getPSI;
                    process.Start();
                    process.StandardInput.WriteLine("Y");
                    process.WaitForExit();
                }

                using (Process process = new Process())
                {
                    process.StartInfo = editFile;
                    process.Start();
                    process.WaitForExit();
                }

                using (Process process = new Process())
                {
                    process.StartInfo = sendPSI;
                    process.Start();
                    process.StandardInput.WriteLine("Y");
                    process.WaitForExit();
                }

                if (File.Exists(local))
                    File.Delete(local);

                retVal = true;
            }
            catch (Exception ex)
            {
                General.PSCPRequired(ex.Message);
            }

            return retVal;
        }
    }
}
