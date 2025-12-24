using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace AssetManager.Tray
{
    public class HttpDebugService
    {
        // Snapshot atual (por enquanto fixo / mock)
        // Depois isso vai ser atualizado pelo pipe
        private static string _latestJson = "{ \"status\": \"aguardando dados\" }";
        public static void UpdateSnapshot(string json)
        {
            _latestJson = json;
        }
        public async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://localhost:8765");

            var app = builder.Build();

            app.MapGet("/metrics", () =>
            {
                return Results.Text(_latestJson, "application/json");
            });

            await app.RunAsync();
        }
    }
}