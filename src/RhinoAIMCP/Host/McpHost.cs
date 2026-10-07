using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RhinoAIMCP.Tools;

namespace RhinoAIMCP.Host;

/// <summary>
/// Hosts the MCP Streamable HTTP server inside Rhino.
/// Default port is 4001 (original RhinoAiMCP used 3001).
/// </summary>
public static class McpHost
{
    public const int DefaultPort = 4001;

    private static WebApplication? _app;
    private static Task? _runTask;
    private static CancellationTokenSource? _cts;

    public static bool IsRunning => _app is not null;

    public static int Port { get; private set; } = DefaultPort;

    public static string Endpoint => $"http://localhost:{Port}/mcp";

    public static bool Start(int? port = null)
    {
        if (IsRunning)
            return false;

        Port = port is > 0 and < 65536 ? port.Value : DefaultPort;
        _cts = new CancellationTokenSource();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(McpHost).Assembly.FullName,
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{Port}");
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "RhinoAIMCP",
                    Version = "0.1.0",
                };
            })
            .WithHttpTransport()
            .WithTools<RhinoTools>();

        _app = builder.Build();
        _app.MapMcp("/mcp");

        _runTask = Task.Run(async () =>
        {
            try
            {
                await _app.StartAsync(_cts.Token);
                await _app.WaitForShutdownAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"RhinoAIMCP server error: {ex.Message}");
            }
        }, _cts.Token);

        return true;
    }

    public static void Stop()
    {
        if (_app is null)
            return;

        try
        {
            _cts?.Cancel();
            _app.StopAsync().GetAwaiter().GetResult();
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // best-effort shutdown
        }
        finally
        {
            _app = null;
            _runTask = null;
            _cts?.Dispose();
            _cts = null;
        }
    }
}
