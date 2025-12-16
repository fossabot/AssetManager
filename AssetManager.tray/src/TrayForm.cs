using System;
using System.Windows.Forms;
using System.ServiceProcess;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AssetManager.Tray
{
    public class TrayForm : Form
    {
        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly HttpDebugService _httpDebugService = new();
        private const string ServiceName = "AssetManager";
        private static readonly HttpClient _http = new();
        private readonly string _apiUrl;
        private readonly string _apiToken;
        private readonly bool _apiEnabled;
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
            var config = Program.Configuration;

            _apiUrl = config["Api:Url"];
            _apiToken = config["Api:Token"];

            _apiEnabled = !string.IsNullOrWhiteSpace(_apiUrl);

            if (_apiEnabled && !string.IsNullOrWhiteSpace(_apiToken))
            {
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);
            }

            _ = Task.Run(_httpDebugService.StartAsync);
            _ = Task.Run(PipeReadLoop);
            EnsureServiceRunning();
        }
        private void EnsureServiceRunning()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);

                if (sc.Status == ServiceControllerStatus.Stopped ||
                    sc.Status == ServiceControllerStatus.StopPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao iniciar o serviço {ServiceName}.\n\n{ex.Message}",
                    "AssetManager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        private void StopService()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);

                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao parar o serviço {ServiceName}.\n\n{ex.Message}",
                    "AssetManager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
        private void OnSendNow(object? sender, EventArgs e)
        {
            MessageBox.Show("Envio manual ainda não implementado.");
        }

        private void OnExit(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            StopService();
            Application.Exit();
        }
        private async Task PipeReadLoop()
        {
            while (true)
            {
                try
                {
                    using var pipe = new NamedPipeClientStream(
                        ".",
                        "asset-monitor-pipe",
                        PipeDirection.In
                    );

                    await pipe.ConnectAsync(3000);

                    using var reader = new StreamReader(pipe);
                    var json = await reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        HttpDebugService.UpdateSnapshot(json);
                        _ = SendToApiAsync(json);
                    }
                }
                catch
                {
                    // Serviço pode estar indisponível ainda
                    // ignorar
                }
                await Task.Delay(2000);
            }
        }
        private async Task SendToApiAsync(string json)
        {
            if (!_apiEnabled)
                return;

            try
            {
                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                await _http.PostAsync(_apiUrl!, content);
            }
            catch
            {
                // falha silenciosa
                // tray nunca deve quebrar por rede
            }
        }
    }
}
