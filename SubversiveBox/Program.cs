namespace SubversiveBox
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    internal class Program
    {
        private static FileSystemWatcher fsw;
        private static string log = "";
        private static string subversionApp = "svn.exe";
        private static List<string> svnCommands;
        private static string svnDirectory = "";
        private static string svnPassword = "anonymous";
        private static string svnURL = "";
        private static string svnUserName = "anonymous";
        private static string totallog = "";

        private static string callRealSVN(string arguments)
        {
            Process process = new Process
            {
                EnableRaisingEvents = false
            };
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = subversionApp;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            Console.WriteLine("  called: '" + subversionApp + " " + arguments + "'");
            process.Start();
            string str = process.StandardOutput.ReadToEnd();
            string str2 = process.StandardError.ReadToEnd();
            Console.WriteLine("  stdout: " + str);
            Console.WriteLine("  stderr: " + str2);
            return str;
        }

        private static string callSVN(string command, string path, bool directCall)
        {
            string str2 = " " + command + " \"" + path + "\" ";
            string arguments = str2 + " --trust-server-cert --username " + svnUserName + " --password " + svnPassword + " --non-interactive";
            if (directCall)
            {
                return callRealSVN(arguments);
            }
            lock (svnCommands)
            {
                svnCommands.Add(arguments);
            }
            return "";
        }

        private static void f_Changed(object sender, FileSystemEventArgs e)
        {
            if (makesSense(e))
            {
                Console.WriteLine(e.ChangeType + ": " + e.FullPath);
                callSVN("commit -m \"\" ", e.FullPath, false);
            }
        }

        private static void f_Created(object sender, FileSystemEventArgs e)
        {
            if (makesSense(e))
            {
                Console.WriteLine(e.ChangeType + ": " + e.FullPath);
                if (!isFolder(e.FullPath) || !e.FullPath.EndsWith("New Folder"))
                {
                    callSVN("add", e.FullPath, false);
                    callSVN("commit -m \"\" ", e.FullPath, false);
                }
            }
        }

        private static void f_Deleted(object sender, FileSystemEventArgs e)
        {
            if (makesSense(e))
            {
                Console.WriteLine(e.ChangeType + ": " + e.FullPath);
                callSVN("delete", e.FullPath, false);
                callSVN("commit -m \"\" ", e.FullPath, false);
            }
        }

        private static void f_Renamed(object sender, RenamedEventArgs e)
        {
            if (makesSense(e))
            {
                Console.WriteLine(e.ChangeType + ": " + e.FullPath);
                if (isFolder(e.FullPath))
                {
                    string str = e.OldFullPath.Remove(e.OldFullPath.IndexOf(svnDirectory), svnDirectory.Length);
                    str = sanitizeURL(svnURL + "/" + str);
                    string str2 = e.FullPath.Remove(e.FullPath.IndexOf(svnDirectory), svnDirectory.Length);
                    str2 = sanitizeURL(svnURL + "/" + str2);
                    callSVN("add", e.FullPath, false);
                    callSVN("commit -m \"\" ", svnDirectory, false);
                }
                else
                {
                    callSVN("add", e.FullPath, false);
                    callSVN("delete", e.OldFullPath, false);
                    callSVN("commit -m \"\" ", svnDirectory, false);
                }
            }
        }

        private static string getRepositoryURL()
        {
            try
            {
                string str = callSVN("info ", svnDirectory, true);
                Console.WriteLine();
                Console.WriteLine(str);
                Console.WriteLine();
                string[] strArray = str.Split(new char[] { '\n' });
                Console.WriteLine();
                Console.WriteLine(strArray[0]);
                Console.WriteLine();
                string str2 = strArray[1];
                Console.WriteLine();
                Console.WriteLine(str2);
                Console.WriteLine();
                string[] separator = new string[] { ": " };
                str2 = str2.Split(separator, StringSplitOptions.None)[1];
                str2 = str2.Trim();
                logg("base url is: " + str2, true);
                return str2;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
                return "";
            }
        }

        private static bool isFolder(string fullpath)
        {
            return !File.Exists(fullpath);
        }

        private static void logg(string s, bool print)
        {
            string str = string.Concat(new object[] { DateTime.Now, ": ", s, '\n' });
            totallog = totallog + str;
            if (print)
            {
                log = log + str;
            }
        }

        private static void Main(string[] args)
        {
            svnCommands = new List<string>();
            bool flag = true;
            if ((args.Length < 2) || (args.Length > 3))
            {
                Console.WriteLine("Usage: SubversiveBox <FolderToWatch> <SVN-Username> [<SVN-Password>]");
                flag = false;
            }
            if (flag)
            {
                svnDirectory = args[0];
                svnUserName = args[1];
                if (args.Length == 2)
                {
                    Console.Write("Please enter your SVN password: ");
                    svnPassword = Console.ReadLine();
                }
                else
                {
                    svnPassword = args[2];
                }
                svnURL = getRepositoryURL();
                if (svnURL.Equals(""))
                {
                    Console.Write("Please enter the repository's base URL: ");
                    svnURL = Console.ReadLine();
                }
                callSVN("update ", svnDirectory, true);
                fsw = new FileSystemWatcher(svnDirectory);
                fsw.IncludeSubdirectories = true;
                fsw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
                fsw.Changed += new FileSystemEventHandler(Program.f_Changed);
                fsw.Deleted += new FileSystemEventHandler(Program.f_Deleted);
                fsw.Renamed += new RenamedEventHandler(Program.f_Renamed);
                fsw.Created += new FileSystemEventHandler(Program.f_Created);
                fsw.EnableRaisingEvents = true;
                Console.Clear();
                while (true)
                {
                    Console.Write(log);
                    log = "";
                    lock (svnCommands)
                    {
                        foreach (string str in svnCommands)
                        {
                            callRealSVN(str);
                        }
                        svnCommands.Clear();
                    }
                    Thread.Sleep(0x3e8);
                }
            }
        }

        private static bool makesSense(FileSystemEventArgs e)
        {
            return (!e.FullPath.Contains('\\' + ".svn") && !e.FullPath.Equals(svnDirectory));
        }

        private static string sanitizeURL(string url)
        {
            url = url.Replace('\\', '/');
            url = url.Replace("//", "/");
            url = url.Replace("https:/", "https://");
            url = url.Replace("http:/", "http://");
            return url;
        }
    }
}

