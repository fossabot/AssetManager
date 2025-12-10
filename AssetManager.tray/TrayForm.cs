using System;
using System.Windows.Forms;
using System.ServiceProcess;

namespace AssetManager.Tray
{
    public class TrayForm : Form
    {
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip trayMenu;

        private void EnsureServiceRunning()
        {
            const string serviceName = "AssetManager";

            try
            {
                using var sc = new ServiceController(serviceName);

                if (sc.Status == ServiceControllerStatus.Stopped ||
                    sc.Status == ServiceControllerStatus.StopPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao iniciar serviço: {ex.Message}");
            }
        }

        public TrayForm()
        {
            // Ocultar a janela
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;

            // Menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Enviar agora", null, OnSendNow);
            trayMenu.Items.Add("Sair", null, OnExit);

            // Icon
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,   // TODO: trocar icone
                Visible = true,
                Text = "Asset Manager Agent",
                ContextMenuStrip = trayMenu
            };
            EnsureServiceRunning();
        }

        private void OnSendNow(object? sender, EventArgs e)
        {
            MessageBox.Show("Envio manual ainda não implementado.");
        }

        private void OnExit(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}
