using Microsoft.Extensions.Configuration;
using System;
using System.Windows.Forms;

namespace AssetManager.Tray
{
    internal static class Program
    {
        public static IConfiguration Configuration { get; private set; } = null!;

        [STAThread]
        static void Main()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayForm());
        }
    }
}
