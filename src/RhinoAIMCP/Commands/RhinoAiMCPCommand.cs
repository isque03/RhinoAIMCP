using Rhino;
using Rhino.Commands;
using RhinoAIMCP.Host;

namespace RhinoAIMCP.Commands;

[CommandStyle(Style.ScriptRunner)]
public class RhinoAiMCPCommand : Command
{
    public RhinoAiMCPCommand()
    {
        Instance = this;
    }

    public static RhinoAiMCPCommand Instance { get; private set; } = null!;

    public override string EnglishName => "RhinoAiMCP";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (McpHost.IsRunning)
        {
            RhinoApp.WriteLine($"MCP server already running: {McpHost.Endpoint}");
            return Result.Success;
        }

        var port = McpHost.DefaultPort;
        var env = Environment.GetEnvironmentVariable("RHINOAIMCP_PORT");
        if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var parsed) && parsed is > 0 and < 65536)
            port = parsed;

        if (!McpHost.Start(port))
        {
            RhinoApp.WriteLine("Failed to start MCP server.");
            return Result.Failure;
        }

        RhinoApp.WriteLine($"MCP server started: http://localhost:{McpHost.Port}");
        RhinoApp.WriteLine($"Connect clients to: {McpHost.Endpoint}");
        RhinoApp.WriteLine($"Example: npx mcp-remote {McpHost.Endpoint}");
        return Result.Success;
    }
}
