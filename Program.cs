using System;
using System.Windows.Forms;

namespace PoGo.NecroBot.GUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PokeGUI());
        }
    }
}
