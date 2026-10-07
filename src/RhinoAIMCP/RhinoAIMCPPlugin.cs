using System.Reflection;
using Rhino.PlugIns;

namespace RhinoAIMCP;

/// <summary>
/// RhinoAIMCP — clean-room recreation of RhinoPort RhinoAiMCP behavior:
/// embed a local MCP HTTP server inside Rhino for AI agent control.
/// </summary>
public class RhinoAIMCPPlugin : PlugIn
{
    public RhinoAIMCPPlugin()
    {
        Instance = this;
    }

    public static RhinoAIMCPPlugin Instance { get; private set; } = null!;

    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
        Rhino.RhinoApp.WriteLine($"RhinoAIMCP {version} loaded. Run RhinoAiMCP to start the MCP server.");
        return LoadReturnCode.Success;
    }

    protected override void OnShutdown()
    {
        Host.McpHost.Stop();
        base.OnShutdown();
    }
}
