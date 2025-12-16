// CONTEXTO DO PROJETO:
// Essa porcaria pega informações do PC (CPU, RAM, Disco, Rede)
// e envia para um System Tray via Named Pipe (asset-monitor-pipe).
// O tray faz (vai fazer) POST para o backend.
// TODO: Melhorar coleta, incluir CPU model, RAM total, tráfego de rede.
// TODO: Implementar reconexão do pipe e manejo de falhas de envio.
// TODO: Mudar a lógica de cálculo e outras estatiticas para o tray (ex: disk usage / cpu model).

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ICpuMonitor _cpu;
    private readonly IMemoryMonitor _mem;
    private readonly IDiskMonitor _disk;
    private readonly INetworkMonitor _net;

    private const string PIPE_NAME = "asset-monitor-pipe";
    private const int COLLECT_INTERVAL_SECONDS = 5;

    // Snapshot atual (thread-safe)
    private readonly object _lock = new();
    private string _latestJson = "{}";

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
        _logger.LogInformation("AssetManager Worker iniciado.");

        // Loop de coleta
        _ = Task.Run(() => CollectLoop(stoppingToken), stoppingToken);

        // Loop do servidor de pipe
        await PipeServerLoop(stoppingToken);
    }
    /// Coleta e mantém o último snapshot
    private async Task CollectLoop(CancellationToken token)
    {
        var (diskUsedGb, diskTotalGb) = _disk.GetDiskUsage();
        var diskUsagePercent = diskTotalGb > 0
            ? Math.Round((diskUsedGb / diskTotalGb) * 100, 2)
            : 0;
        var (ip, mac) = _net.GetNetworkInfo();

        // TODO: mudar para ID real
        var assetId = Environment.MachineName;
        var hostname = Environment.MachineName;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var payload = new
                {
                    asset_id = assetId, // TODO: mudar para ID real
                    hostname = Environment.MachineName,
                    // TODO: mostrar model CPU
                    cpu_usage = _cpu.GetCpuUsage(),
                    // TODO: mostrar RAM total
                    memory_usage = _mem.GetMemoryUsage(),
                    disk = diskUsagePercent,
                    ip_addr = ip,
                    mac_addr = mac,
                    os = Environment.OSVersion.ToString(),
                    timestamp = DateTime.UtcNow,
                    // TODO: adicionar versionamento do payload
                    version = "v1"
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                lock (_lock)
                {
                    _latestJson = json;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao coletar métricas.");
            }

            await Task.Delay(TimeSpan.FromSeconds(COLLECT_INTERVAL_SECONDS), token);
        }
    }
    private async Task PipeServerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream(
                    PIPE_NAME,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _logger.LogDebug("Aguardando conexão no named pipe...");

                await pipeServer.WaitForConnectionAsync(token);

                string snapshot;
                lock (_lock)
                {
                    snapshot = _latestJson;
                }

                using var writer = new StreamWriter(pipeServer, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                // Envia uma única linha (snapshot atual)
                await writer.WriteLineAsync(snapshot);

                _logger.LogDebug("Snapshot enviado para cliente do pipe.");
            }
            catch (OperationCanceledException)
            {
                // shutdown normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no servidor do named pipe.");
                await Task.Delay(1000, token); // backoff leve
            }
        }
    }
}