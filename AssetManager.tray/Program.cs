using System;
using System.Windows.Forms;

namespace AssetManager.Tray
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayForm());
        }
    }
    
}