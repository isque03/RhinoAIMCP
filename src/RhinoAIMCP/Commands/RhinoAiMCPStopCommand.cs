using Rhino;
using Rhino.Commands;
using RhinoAIMCP.Host;

namespace RhinoAIMCP.Commands;

[CommandStyle(Style.ScriptRunner)]
public class RhinoAiMCPStopCommand : Command
{
    public RhinoAiMCPStopCommand()
    {
        Instance = this;
    }

    public static RhinoAiMCPStopCommand Instance { get; private set; } = null!;

    public override string EnglishName => "RhinoAiMCPStop";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!McpHost.IsRunning)
        {
            RhinoApp.WriteLine("MCP server is not running.");
            return Result.Nothing;
        }

        McpHost.Stop();
        RhinoApp.WriteLine("MCP server stopped.");
        return Result.Success;
    }
}
