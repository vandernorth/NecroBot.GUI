using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PoGo.NecroBot.GUI
{
    static class Program
    {
        [STAThread]
        static void Main(string[] argv)
        {
            bool isDebug = false;
            if (argv.Length > 0 && argv[0] == "--debug") {
                AllocConsole();
                Console.Title = "VanDerBot - Debug";
                isDebug = true;
            }
            

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PokeGUI(isDebug));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
    }

}
