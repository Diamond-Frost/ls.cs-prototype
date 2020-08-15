//Uses: .NET Core 3.1; System.IO.FileSecurity.AccessControl pkg

using System;
using System.IO;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ls.cs
{
    using NativeExtension;

    static class Globals
    {
        public static int longestSize = 3;
        public static int longestAmount = 1;
        public static int longestOwn = 0;
        public static int longestGrp = 0;
        public static int longestName = 0;
        //better for debugging
        public static string[] testArgs = new string[] { "--dateformat", "dd \\o\\f MM yyyy hh:mm:ss" };

    }
    static class Color
    { //wanted JSON config, couldnt get to it
        public static uint mode = 251;
        public static uint amount = 229;
        public static uint owner = 216;
        public static uint group = 215;
        public static uint date = 106;
        public static uint time = 107;
        public static uint size = 186;
        public static uint file = 15;
        public static uint path = 15;
        public static uint perm = 164;
        public static bool allowColors = true;
    }

    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, int mode);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(IntPtr handle, out int mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(int handle);
        static void Main(string[] args)
        {
            var handle = GetStdHandle(-11);
            int mode;
            GetConsoleMode(handle, out mode);
            SetConsoleMode(handle, mode | 0x4);


            //using home dir for debugging 
            string wd = "C:\\Users\\Username";
            string cwd = Directory.GetCurrentDirectory();
            string[] files = Directory.GetFileSystemEntries(wd);

            Options opt = Options.parse(Globals.testArgs);
            List<lsFileInfo> infos = new List<lsFileInfo>();

            foreach (string file in files)
            { 

                FileInfo cFI = new FileInfo(file);
                lsFileInfo f = new lsFileInfo();

                //no need to get info on a hidden file if you dont wanna have all
                if (cFI.Attributes.HasFlag(FileAttributes.Hidden) && !opt.all)
                    continue;


                f.mode = Util.GetMode(cFI.Attributes, opt.modeformat);
                if (!cFI.Attributes.HasFlag(FileAttributes.Directory))
                    f.size = cFI.Length;

                f.udate = cFI.LastWriteTime.ToString(opt.dateFormat);
                f.utime = cFI.LastWriteTime.ToString(opt.timeFormat);
                f.path = cFI.DirectoryName;
                f.name = cFI.Name;

                f.isDir = cFI.Attributes.HasFlag(FileAttributes.Directory);
                f.isLink = cFI.Attributes.HasFlag(FileAttributes.ReparsePoint);
                f.hidden = cFI.Attributes.HasFlag(FileAttributes.Hidden);

                f.owner = cFI.GetAccessControl().GetOwner(typeof(NTAccount)).Value; //does #define exist in C#?
                if (f.owner.StartsWith(Environment.MachineName) && !opt.showMachine)
                    f.owner = f.owner.Remove(0, Environment.MachineName.Length + 1);

                f.group = cFI.GetAccessControl().GetGroup(typeof(NTAccount)).Value;
                if (f.group.StartsWith(Environment.MachineName) && !opt.showMachine)
                    f.group = f.group.Remove(0, Environment.MachineName.Length + 1);

                f.permissions = Util.GetPermissions(cFI.GetAccessControl().GetAccessRules(true, true, typeof(NTAccount)), opt.permformat, opt.permrwformat);

                infos.Add(f);

                if (f.isDir)
                { //could be better
                    DirectoryInfo cDI = new DirectoryInfo(file);
                    try
                    {
                        if (cDI.GetFileSystemInfos().Length.ToString().Length > Globals.longestAmount)
                            Globals.longestAmount = cDI.GetFileSystemInfos().Length.ToString().Length;
                        f.amount = cDI.GetFileSystemInfos().Length;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        f.amount = -1;
                    };
                }
                else
                {
                    if (cFI.Length.ToString().Length > Globals.longestSize) Globals.longestSize = cFI.Length.ToString().Length;
                }
                if (f.owner.Length > Globals.longestOwn) Globals.longestOwn = f.owner.Length;
                if (f.group.Length > Globals.longestGrp) Globals.longestGrp = f.group.Length;
                if (f.name.Length > Globals.longestName) Globals.longestName = f.name.Length;
            }

            if (opt.reverse) infos.Reverse();

            foreach (lsFileInfo cf in infos)
            { //could be done in the upper loop maybe?
                string line = ""; //people told me to name shit better, like "fileMeta" instead of line, even tho this is only here for outputting the line

                if (opt.filter.Count > 0)
                    if (opt.filter.IndexOf(cf.name) == -1)
                        continue;

                if (cf.hidden && !opt.all)
                    continue;

                if (opt.list)
                {
                    line = Util.formatOut(cf, opt.listformat);
                }
                else
                {
                    foreach (string listmode in opt.order)
                    { //could use dictionary
                        if (listmode == "mode") line += Util.Color(Color.mode, cf.mode + " ");
                        if (listmode == "amount") line += Util.Color(Color.amount, (cf.amount == -1 ? "?" : cf.amount.ToString()).PadRight(Globals.longestAmount) + " ");
                        if (listmode == "owner") line += Util.Color(Color.owner, cf.owner.PadRight(Globals.longestOwn) + " ");
                        if (listmode == "group") line += Util.Color(Color.group, cf.group.PadRight(Globals.longestGrp) + " ");
                        if (listmode == "date") line += Util.Color(Color.date, cf.udate + " ");
                        if (listmode == "time") line += Util.Color(Color.time, cf.utime + " ");
                        if (listmode == "size") line += Util.Color(Color.size, (cf.size == -1 ? "---" : cf.size.ToString()).PadRight(Globals.longestSize) + " ");
                        if (listmode == "perm")
                            foreach (Rule r in cf.permissions) line += Util.Color(Color.perm, r.User + " " + r.PermString);
                    }
                    if (opt.fullpath)
                        line += Util.Color(Color.file, cf.path + "\\");
                    line += Util.Color(Color.file, cf.name);

                    if (opt.indicator)
                    {
                        Util.GetIndicator(cf.mode);
                    }
                }

                Console.WriteLine(line);
            }
        }
    }
    public class Util
    {
        public static string formatOut(lsFileInfo cf, string format = "%m %A %O %G %d %t %s %f")
        {
            string output = format;
            output = output.Replace("%m", Color(cs.Color.mode, cf.mode)); //could've chained, but this looks better IMO and allows me to format and seperate

            output = output.Replace("%An", Color(cs.Color.amount, (cf.amount == -1 ? "?" : cf.amount.ToString())));
            output = output.Replace("%Al", Color(cs.Color.amount, (cf.amount == -1 ? "?" : cf.amount.ToString()).PadLeft(Globals.longestAmount)));
            output = output.Replace("%A", Color(cs.Color.amount, (cf.amount == -1 ? "?" : cf.amount.ToString()).PadRight(Globals.longestAmount)));

            output = output.Replace("%On", Color(cs.Color.owner, cf.owner));
            output = output.Replace("%Ol", Color(cs.Color.owner, cf.owner.PadLeft(Globals.longestOwn)));
            output = output.Replace("%O", Color(cs.Color.owner, cf.owner.PadRight(Globals.longestOwn)));

            output = output.Replace("%Gn", Color(cs.Color.group, cf.group));
            output = output.Replace("%Gl", Color(cs.Color.group, cf.group.PadLeft(Globals.longestGrp)));
            output = output.Replace("%G", Color(cs.Color.group, cf.group.PadRight(Globals.longestGrp)));

            output = output.Replace("%d", Color(cs.Color.date, cf.udate));
            output = output.Replace("%t", Color(cs.Color.time, cf.utime));

            output = output.Replace("%sn", Color(cs.Color.size, (cf.size == -1 ? "---" : cf.size.ToString())));
            output = output.Replace("%sl", Color(cs.Color.size, (cf.size == -1 ? "---" : cf.size.ToString()).PadLeft(Globals.longestSize)));
            output = output.Replace("%s", Color(cs.Color.size, (cf.size == -1 ? "---" : cf.size.ToString()).PadRight(Globals.longestSize)));

            output = output.Replace("%fn", Color(cs.Color.file, cf.name));
            output = output.Replace("%fl", Color(cs.Color.file, cf.name.PadLeft(Globals.longestName)));
            output = output.Replace("%f", Color(cs.Color.file, cf.name.PadRight(Globals.longestName)));

            output = output.Replace("%p", Color(cs.Color.path, cf.path));

            string perms = "";
            foreach (Rule r in cf.permissions) {
                perms += r.User + " " + r.PermString;
            }
            output = output.Replace("%P", Color(cs.Color.path, perms));
            return output;
        }

        public static string GetMode(FileAttributes attr, string order = "acdbehgnnxiorlzst")
        {
            string perms = "";
            foreach (char entry in order)
            { //could use dictionary
                if (entry == 'a') perms += attr.HasFlag(FileAttributes.Archive) ? "a" : "-";
                else if (entry == 'c') perms += attr.HasFlag(FileAttributes.Compressed) ? "c" : "-";
                else if (entry == 'd') perms += attr.HasFlag(FileAttributes.Directory) ? "d" : "-";
                else if (entry == 'b') perms += attr.HasFlag(FileAttributes.Device) ? "b" : "-";
                else if (entry == 'e') perms += attr.HasFlag(FileAttributes.Encrypted) ? "e" : "-";
                else if (entry == 'h') perms += attr.HasFlag(FileAttributes.Hidden) ? "h" : "-";
                else if (entry == 'g') perms += attr.HasFlag(FileAttributes.IntegrityStream) ? "g" : "-";
                else if (entry == 'n') perms += attr.HasFlag(FileAttributes.Normal) ? "n" : "-";
                else if (entry == 'x') perms += attr.HasFlag(FileAttributes.NoScrubData) ? "x" : "-";
                else if (entry == 'i') perms += attr.HasFlag(FileAttributes.NotContentIndexed) ? "i" : "-";
                else if (entry == 'o') perms += attr.HasFlag(FileAttributes.Offline) ? "o" : "-";
                else if (entry == 'r') perms += attr.HasFlag(FileAttributes.ReadOnly) ? "r" : "-";
                else if (entry == 'l') perms += attr.HasFlag(FileAttributes.ReparsePoint) ? "l" : "-";
                else if (entry == 'z') perms += attr.HasFlag(FileAttributes.SparseFile) ? "z" : "-";
                else if (entry == 's') perms += attr.HasFlag(FileAttributes.System) ? "s" : "-";
                else if (entry == 't') perms += attr.HasFlag(FileAttributes.Temporary) ? "t" : "-";
            }
            return perms;
        }

        public static List<Rule> GetPermissions(AuthorizationRuleCollection rules, string format = "fdDlxXtamsocrw", string rwFormat = "madxp")
        {
            List<Rule> parsedRules = new List<Rule>();
            foreach (FileSystemAccessRule rule in rules)
            {
                Rule tempRule = new Rule();
                tempRule.User = rule.IdentityReference.Value;
                string perm = "";
                foreach (char entry in format)
                {
                    if (entry == 'f') perm += rule.FileSystemRights.HasFlag(FileSystemRights.FullControl) ? 'f' : '-';

                    else if (entry == 'd') perm += rule.FileSystemRights.HasFlag(FileSystemRights.Delete) ? 'd' : '-';
                    else if (entry == 'D') perm += rule.FileSystemRights.HasFlag(FileSystemRights.DeleteSubdirectoriesAndFiles) ? 'D' : '-';

                    else if (entry == 'l') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ListDirectory) ? 'l' : '-';
                    else if (entry == 'x') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ExecuteFile) ? 'x' : '-';
                    else if (entry == 'X') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ReadAndExecute) ? 'X' : '-';
                    else if (entry == 't') perm += rule.FileSystemRights.HasFlag(FileSystemRights.Traverse) ? 't' : '-';

                    else if (entry == 'a') perm += rule.FileSystemRights.HasFlag(FileSystemRights.AppendData) ? 'a' : '-';
                    else if (entry == 'm') perm += rule.FileSystemRights.HasFlag(FileSystemRights.Modify) ? 'm' : '-';
                    else if (entry == 's') perm += rule.FileSystemRights.HasFlag(FileSystemRights.Synchronize) ? 's' : '-';
                    else if (entry == 'o') perm += rule.FileSystemRights.HasFlag(FileSystemRights.TakeOwnership) ? 'o' : '-';

                    else if (entry == 'c')
                    {
                        perm += "c[";
                        perm += rule.FileSystemRights.HasFlag(FileSystemRights.CreateDirectories) ? 'd' : '-';
                        perm += rule.FileSystemRights.HasFlag(FileSystemRights.CreateFiles) ? 'f' : '-';
                        perm += "]";
                    }

                    else if (entry == 'r')
                    {
                        if (rwFormat == "m")
                        {
                            perm += rule.FileSystemRights.HasFlag(FileSystemRights.Read) ? 'r' : '-';
                        }
                        else
                        {
                            perm += "r[";
                            foreach (char readentry in rwFormat)
                            {
                                if (readentry == 'm') perm += rule.FileSystemRights.HasFlag(FileSystemRights.Read) ? 'r' : '-';
                                else if (readentry == 'a') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ReadAttributes) ? 'a' : '-';
                                else if (readentry == 'd') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ReadData) ? 'd' : '-';
                                else if (readentry == 'x') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ReadExtendedAttributes) ? 'x' : '-';
                                else if (readentry == 'p') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ReadPermissions) ? 'p' : '-';
                            }
                            perm += "]";
                        }
                    }

                    else if (entry == 'w')
                    {
                        if (rwFormat == "m")
                        {
                            perm += rule.FileSystemRights.HasFlag(FileSystemRights.Read) ? 'r' : '-';
                        }
                        else
                        {
                            perm += "w[";
                            foreach (char readentry in rwFormat)
                            {
                                if (readentry == 'm') perm += rule.FileSystemRights.HasFlag(FileSystemRights.Write) ? 'w' : '-';
                                else if (readentry == 'a') perm += rule.FileSystemRights.HasFlag(FileSystemRights.WriteAttributes) ? 'a' : '-';
                                else if (readentry == 'd') perm += rule.FileSystemRights.HasFlag(FileSystemRights.WriteData) ? 'd' : '-';
                                else if (readentry == 'x') perm += rule.FileSystemRights.HasFlag(FileSystemRights.WriteExtendedAttributes) ? 'x' : '-';
                                else if (readentry == 'p') perm += rule.FileSystemRights.HasFlag(FileSystemRights.ChangePermissions) ? 'c' : '-';
                            }
                            perm += "]";
                        };
                    };
                    tempRule.PermString = perm;

                };
                parsedRules.Add(tempRule);
            };
            return parsedRules;
        }

        /*public static char GetIndicator(string mode)
        { //unused function... Windows aint good for indicators
            if (mode.Has("l")) return '@';
            else if (mode.Has("d")) return '/';
            else if (mode.Has("e")) return '?';
            else if (mode.Has("t")) return ':';
            else return ' ';
        }*/

        public static string Color(uint code, string str)
        { //true font color table (0 - 255)
          //if you got node: r = (i,f) = new Array(i).fill().forEach(f); r(16, (v,i) => r(16, (v,j) => process.stdout.write("\u001b[38;5;${i*16+j}m${i*16+j}")));
          //all 256 colors
            if (cs.Color.allowColors)
                return "\x1b[38;5;" + code + "m" + str + "\u001b[0m";
            else
                return str;
        }
        public static string BGColor(uint code, string str)
        {
            return "\x1b[48;5;" + code + "m" + str + "\u001b[0m";
        }
    }

    public class lsFileInfo
    {
        public List<Rule> permissions = new List<Rule>();

        public bool isDir = false;
        public bool isLink = false;
        public bool hidden = false;

        public int amount = 1;
        public long size = -1;

        public string mode = "";
        public string utime = "";
        public string udate = "";
        public string name = "";
        public string path = "";
        public string owner = "";
        public string group = "";
    }

    public class Rule
    {
        public string User;
        public string PermString;
    }

    public class Options
    {
        public List<string> order = new List<string>();
        public bool all = false;
        public bool reverse = false;
        public bool showMachine = false;

        public bool list = false;
        public bool mode = false;
        public bool amount = false;
        public bool owner = false;
        public bool group = false;
        public bool date = false;
        public bool time = false;
        public bool size = false;
        public bool perm = false;

        public bool fullpath = false;
        public bool indicator = false;

        public bool formatOut = false;

        public string dateFormat = "%DD/%MM/%YYYY";
        public string timeFormat = "%hh:%mm:%ss.%ms3";
        public string listformat = "%m %A %O %G %d %t %s %f";
        public string modeformat = "acdbehgnnxiorlzst";
        public string permformat = "fdDlxXtamsocrw";
        public string permrwformat = "madxp";
        public List<string> filter = new List<string>();

        public static Options parse(string[] args)
        {
            Options parsed = new Options();

            bool NextDateFormat = false;
            bool NextTimeFormat = false;
            bool NextModeFormat = false;
            bool NextFormat = false;

            foreach (string arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    string namedArg = arg.Remove(0, 2);
                    if (namedArg == "all") parsed.all = true;
                    else if (namedArg == "reverse") parsed.reverse = true;

                    else if (namedArg == "list") parsed.list = true;
                    else if (namedArg == "mode") { parsed.mode = true; parsed.order.Add("mode"); }
                    else if (namedArg == "amount") { parsed.amount = true; parsed.order.Add("amount"); }
                    else if (namedArg == "owner") { parsed.owner = true; parsed.order.Add("owner"); }
                    else if (namedArg == "group") { parsed.group = true; parsed.order.Add("group"); }
                    else if (namedArg == "date") { parsed.date = true; parsed.order.Add("date"); }
                    else if (namedArg == "time") { parsed.time = true; parsed.order.Add("time"); }
                    else if (namedArg == "size") { parsed.size = true; parsed.order.Add("size"); }
                    else if (namedArg == "permission") { parsed.perm = true; parsed.order.Add("perm"); }

                    else if (namedArg == "indicator") parsed.indicator = true;
                    else if (namedArg == "fullpath") parsed.fullpath = true;

                    else if (namedArg == "dateformat") { parsed.date = true; parsed.order.Add("date"); NextDateFormat = true; }
                    else if (namedArg == "timeformat") { parsed.time = true; parsed.order.Add("time"); NextTimeFormat = true; }
                    else if (namedArg == "modeformat") { parsed.mode = true; parsed.order.Add("mode"); NextModeFormat = true; }
                    else if (namedArg == "outformat") { parsed.list = true; NextFormat = true; }

                    else if (namedArg == "smallmode") { parsed.mode = true; parsed.order.Add("mode"); parsed.modeformat = "dlarh"; }
                    else if (namedArg == "readable") parsed.dateFormat = "%MF %DD %YYYY";
                    else if (namedArg == "showmachine") parsed.showMachine = true;
                }
                else if (arg.StartsWith("-"))
                {
                    foreach (char c in arg)
                    {
                        if (c == 'a') parsed.all = true;
                        else if (c == 'r') parsed.reverse = true;

                        else if (c == 'l') parsed.list = true;
                        else if (c == 'm') { parsed.mode = true; parsed.order.Add("mode"); }
                        else if (c == 'A') { parsed.amount = true; parsed.order.Add("amount"); }
                        else if (c == 'O') { parsed.owner = true; parsed.order.Add("owner"); }
                        else if (c == 'G') { parsed.group = true; parsed.order.Add("group"); }
                        else if (c == 'd') { parsed.date = true; parsed.order.Add("date"); }
                        else if (c == 't') { parsed.time = true; parsed.order.Add("time"); }
                        else if (c == 's') { parsed.size = true; parsed.order.Add("size"); }
                        else if (c == 'p') { parsed.perm = true; parsed.order.Add("perm"); }

                        else if (c == 'f') parsed.fullpath = true;
                        else if (c == 'i') parsed.indicator = true;

                    }
                }
                else
                {
                    if (NextDateFormat)
                    {
                        parsed.dateFormat = arg;
                        NextDateFormat = false;
                    }
                    else if (NextTimeFormat)
                    {
                        parsed.timeFormat = arg;
                        NextTimeFormat = false;
                    }
                    else if (NextModeFormat)
                    {
                        parsed.modeformat = arg;
                        NextModeFormat = false;
                    }
                    else if (NextFormat)
                    {
                        parsed.listformat = arg;
                        NextFormat = false;
                    }
                    else
                    {
                        parsed.filter.Add(arg);
                    }
                }
            }
            return parsed;
        }
    }
}


namespace NativeExtension
{
    public static class StringExtension
    {
        public static bool Has(this String str, string searchString)
        {
            return !(str.IndexOf(searchString) == -1);
        }
    }
}
