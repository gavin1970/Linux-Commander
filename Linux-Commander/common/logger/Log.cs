using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public class Log
{
    static object _lock = new object();
    // . = end of permissions
    // + = extended permissions.    getfacl <filename>
    // @ = extended attributes.     xattr -l <filename>
    // <blank> = older versions of Linux don't have anything behind the permissions.
    static List<string> permEnd = new List<string>() { " ", ".", "+", "@" };
    internal static bool IsPrompt { get; set; } = false;
    internal static ConsoleColor Directories { get; set; } = ConsoleColor.Green;    //default
    internal static ConsoleColor Sticky { get; set; } = ConsoleColor.Cyan;    //default
    internal static ConsoleColor FullPermissionsBG { get; set; } = ConsoleColor.Red;    //default
    internal static ConsoleColor FullPermissionsFG { get; set; } = ConsoleColor.White;    //default
    internal static ConsoleColor BlockSpecialFile { get; set; } = ConsoleColor.DarkYellow;    //default
    internal static ConsoleColor CharacterSpecialFile { get; set; } = ConsoleColor.DarkMagenta;    //default
    internal static ConsoleColor SymbolicLink { get; set; } = ConsoleColor.Yellow;    //default
    internal static ConsoleColor Prompt { get; set; } = ConsoleColor.DarkGreen;    //default

    /*
        * sticky            = -rwxrwxrwt
        * dir all rights    = drwxrwxrwx 
        * file all rights   = -rwxrwxrwx
    */
    const string _allRights = "rwxrwxrw";

    /// <summary>
    /// Pass through method.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="color"></param>
    /// <param name="lineBreak"></param>
    /// <param name="center"></param>
    /// <param name="maxLineLength"></param>
    internal static void Verbose(string message, ConsoleColor color = ConsoleColor.Gray, bool lineBreak = true, bool center = false, int maxLineLength = -1)
    {
        Verbose(1, message, color, lineBreak, center, maxLineLength);
    }

    /// <summary>
    /// Display content on screen, via color and based on data being passed through.
    /// </summary>
    /// <param name="prePad"></param>
    /// <param name="message"></param>
    /// <param name="color"></param>
    /// <param name="lineBreak"></param>
    /// <param name="center"></param>
    /// <param name="maxLineLength"></param>
    internal static void Verbose(int prePad, string message, ConsoleColor color = ConsoleColor.Gray, bool lineBreak = true, bool center = false, int maxLineLength = -1)
    {
        bool orgLineBreak = lineBreak;
        if (message == null)
            return;

        lock (_lock)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = color;
            bool stickyColor = false;
            ConsoleColor bkColor = ConsoleColor.Black;

            //going to process each line, because we might change color of the line based on data being recieved.
            foreach (string msg in message.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
            {
                string startsWith = "";
                string endsWith = "";
                string[] msgSplit = msg.Split(' ');

                if (string.IsNullOrWhiteSpace(msg))
                    stickyColor = false;

                Console.Write(new string(' ', prePad));

                //let validate the first 12 bytes
                string permissions = msgSplit[0];
                if (permissions.Length == 11)
                {
                    startsWith = permissions.Substring(0, 1);
                    endsWith = permissions.Substring(permissions.Length - 1, 1);
                }

                //directory listing..
                if (msgSplit.Length >= 9 && msgSplit[0].Length == 11 && permEnd.Contains(endsWith))
                {
                    ConsoleColor setColor = color;
                    ConsoleColor dirFGColor = ConsoleColor.Gray;
                    ConsoleColor dirBGColor = ConsoleColor.Black;

                    if (startsWith == "d")
                        dirFGColor = Directories;
                    else if (startsWith == "c")
                        dirFGColor = CharacterSpecialFile;
                    else if (startsWith == "b")
                        dirFGColor = BlockSpecialFile;
                    else if (startsWith == "l")
                        dirFGColor = SymbolicLink;

                    //directory can be sticky, lets change the color to sticky color
                    if (permissions.Substring(permissions.Length - 2, 1) == "t")
                        dirFGColor = Sticky;

                    //if all rights, lets set the background color
                    if (permissions.Contains(_allRights))
                        dirBGColor = FullPermissionsBG;

                    for (int i = 0; i < msgSplit.Length; i++)
                    {
                        //beginning
                        if (i == 0)
                        {
                            Console.BackgroundColor = dirBGColor;
                            if (dirBGColor != ConsoleColor.Black)
                                Console.ForegroundColor = FullPermissionsFG;
                        }
                        //ending
                        else if (i == msgSplit.Length - 1 || (startsWith == "l" && i >= msgSplit.Length - 3))
                        {
                            Console.ForegroundColor = dirFGColor;
                        }

                        Console.Write($"{msgSplit[i].Replace("\r", "")}");

                        //reset
                        Console.ForegroundColor = setColor;
                        Console.BackgroundColor = ConsoleColor.Black;

                        Console.Write(" ");
                    }
                }
                //ansible repsonds..
                else if (msg.StartsWith("[WARNING]: ", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = true;
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.StartsWith("...ignoring", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = false;
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.StartsWith("ok: ", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = true;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.StartsWith("skipping: ", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = false;
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.StartsWith("PLAY [", StringComparison.OrdinalIgnoreCase) || msg.StartsWith("TASK [", StringComparison.OrdinalIgnoreCase) || msg.StartsWith("PLAY RECAP **", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = false;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.StartsWith("failed: ", StringComparison.OrdinalIgnoreCase) || msg.StartsWith("ERROR! ", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = true;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.StartsWith("fatal: ", StringComparison.OrdinalIgnoreCase))
                {
                    stickyColor = true;
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                //ansible repsonds..
                else if (msg.IndexOf(": ok=") > -1 && msg.IndexOf("changed=") > -1 && msg.IndexOf("failed=") > -1 && msg.IndexOf("skipped=") > -1)
                {
                    stickyColor = true;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"{msg.Replace("\r", "")}");
                }
                else
                {
                    if (center)
                    {
                        int length = (Console.WindowWidth / 2) - (msg.Length / 2);
                        Console.Write(new string(' ', length));
                    }

                    //for some reason, we are seeing a carriage return and 
                    //the end of a CFLF, so we are going to strip it.
                    if (maxLineLength != -1 && msg.Length > maxLineLength)
                    {
                        bool first = true;

                        foreach (string newMsg in Split(msg, maxLineLength))
                        {
                            if (!first)
                                Console.Write(new string(' ', prePad));
                            else
                                first = false;
                            Console.WriteLine($"{newMsg.Replace("\r", "")}");
                        }
                    }
                    else
                    {
                        if (IsPrompt && msg.Contains("?:"))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            lineBreak = false;
                            Console.Write($"{msg.Replace("\r", "\n")}");
                        }
                        else
                            Console.Write($"{msg.Replace("\r", "")}");
                    }
                }

                bkColor = ConsoleColor.Black;
                Console.BackgroundColor = bkColor;

                if (lineBreak)
                    Console.Write("\n");

                if (!stickyColor)
                    Console.ForegroundColor = color;
            }

            Console.BackgroundColor = bkColor;
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Draw a line on the screen, with color.
    /// </summary>
    /// <param name="size"></param>
    /// <param name="cc"></param>
    internal static void DrawLine(int size = 50, ConsoleColor cc = ConsoleColor.Yellow)
    {
        Verbose(new string('-', size), cc);
    }

    /// <summary>
    /// Display error box with message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="extraCRLF"></param>
    internal static void Error(string message, int extraCRLF = 0)
    {
        lock (_lock)
        {
            var seperator = new string('-', 50);
            Verbose($"\n {seperator}", ConsoleColor.Red);
            Verbose(message, ConsoleColor.Red);
            Verbose(seperator, ConsoleColor.Red);
            while (extraCRLF > 0)
            {
                Verbose("");
                extraCRLF--;
            }
        }
    }

    /// <summary>
    /// Used for creating a paragraph, max character, wrapped by words.
    /// </summary>
    /// <param name="orgString"></param>
    /// <param name="chunkSize"></param>
    /// <param name="wholeWords"></param>
    /// <returns></returns>
    private static IEnumerable<string> Split(string orgString, int chunkSize, bool wholeWords = true)
    {
        if (wholeWords)
        {
            List<string> result = new List<string>();
            StringBuilder sb = new StringBuilder();

            if (orgString.Length > chunkSize)
            {
                string[] newSplit = orgString.Split(' ');
                foreach (string str in newSplit)
                {
                    if (sb.Length != 0)
                        sb.Append(" ");

                    if (sb.Length + str.Length > chunkSize)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }

                    sb.Append(str);
                }

                result.Add(sb.ToString());
            }
            else
                result.Add(orgString);

            return result;
        }
        else
            return new List<string>(Regex.Split(orgString, @"(?<=\G.{" + chunkSize + "})", RegexOptions.Singleline));
    }
}
