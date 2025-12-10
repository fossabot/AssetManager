// CONTEXTO DO PROJETO:
// Essa porcaria pega informações do PC (CPU, RAM, Disco, Rede)
// e envia para um System Tray via Named Pipe (asset-monitor-pipe).
// O tray faz (vai fazer) POST para o backend.
// TODO: Melhorar coleta, incluir CPU model, RAM total, tráfego de rede.
// TODO: Implementar reconexão do pipe e manejo de falhas de envio.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System.Runtime.InteropServices;
using System.IO.Pipes;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ICpuMonitor _cpu;
    private readonly IMemoryMonitor _mem;
    private readonly IDiskMonitor _disk;
    private readonly INetworkMonitor _net;

    private const string PIPE_NAME = "asset-monitor-pipe";
    private const int PIPE_CONNECT_TIMEOUT_MS = 5_000;

    private const int TIMER = 2_000;
    private readonly int timerSeconds = TIMER / 1000;

    public Worker(
        ILogger<Worker> logger,
        ICpuMonitor cpu,
        IMemoryMonitor mem,
        IDiskMonitor disk,
        INetworkMonitor net)
    {
        _logger = logger;
        _cpu = cpu;
        _mem = mem;
        _disk = disk;
        _net = net;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Monitor iniciado. Intervalo: {timerSeconds} segundos");

        while (!stoppingToken.IsCancellationRequested)
        {
            await SendMetricsAsync(stoppingToken); // TODO: Melhorar
            await Task.Delay(TIMER, stoppingToken);
        }
    }

    private async Task SendMetricsAsync(CancellationToken stoppingToken)
    {
        var (diskUsedGb, diskTotalGb) = _disk.GetDiskUsage();
        var (ip, mac) = _net.GetNetworkInfo();

        // TODO: mudar para ID real
        var assetId = Environment.MachineName;
        var hostname = Environment.MachineName;

        var diskUsagePercent = diskTotalGb > 0
            ? Math.Round((diskUsedGb / diskTotalGb) * 100, 2)
            : 0;

        var payload = new
        {
            asset_id = assetId, // TODO: mudar para ID real
            hostname = hostname,
            cpu_usage = _cpu.GetCpuUsage(),
            memory_usage = _mem.GetMemoryUsage(),
            disk_usage = diskUsagePercent,
            network_in = 0,  // TODO: implementar
            network_out = 0,
            uptime = (long)Environment.TickCount64 / 1000,

            cpu_info = new
            {
                model = "unknown", // TODO: mostrar modelo
                cores = Environment.ProcessorCount
            },

            ram_info = new
            {
                usage_percent = _mem.GetMemoryUsage() // TODO: mostrar total em GB
            },

            storage_info = new
            {
                disks = new[]
                {
                    new { name = "C:", used_gb = diskUsedGb, total_gb = diskTotalGb }
                }
            },

            ip_address = ip,
            mac_address = mac,
            os_name = RuntimeInformation.OSDescription,
            os_version = Environment.OSVersion.Version.ToString()
        };

        var json = JsonSerializer.Serialize(payload);

        try
        {
            using var pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(PIPE_CONNECT_TIMEOUT_MS);

            await pipeClient.ConnectAsync(cts.Token);

            if (!pipeClient.IsConnected)
            {
                _logger.LogWarning("Nao foi possivel conectar ao named pipe {PipeName}.", PIPE_NAME);
                return;
            }

            using var writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(json);

            _logger.LogInformation("Metricas enviadas via named pipe.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout ao conectar ao named pipe {PipeName}.", PIPE_NAME);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Erro de E/S ao enviar metricas via named pipe.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar metricas via named pipe.");
        }
    }
}
