# RhinoAIMCP

Rhino 8 plugin that embeds a local **MCP** (Model Context Protocol) HTTP server so AI agents (Claude Desktop, Cursor, Copilot, etc.) can inspect and drive the active Rhino document.

This is a **clean-room MIT** recreation inspired by the public behavior of [RhinoAiMCP (RhinoPort) on Food4Rhino](https://www.food4rhino.com/en/app/rhinoaimcp). No original binaries or source were copied. Related open-source projects ([jingcheng-chen/rhinomcp](https://github.com/jingcheng-chen/rhinomcp), [mcneel/RhinoAI](https://github.com/mcneel/RhinoAI)) informed architecture only.

## Features (v0.1)

- Rhino commands: `RhinoAiMCP` (start), `RhinoAiMCPStop` (stop)
- MCP Streamable HTTP at `http://localhost:4001/mcp` (override with env `RHINOAIMCP_PORT`)
- Tools: `get_document_info`, `get_objects`, `create_box`, `create_sphere`, `create_cylinder`, `delete_objects`, `execute_csharp`

> Note: the Food4Rhino original used port **3001**. This build defaults to **4001** to avoid common local conflicts.

## Requirements

- [Rhino 8](https://www.rhino3d.com/) (macOS or Windows)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
- [Node.js](https://nodejs.org/) (`npx`) for `mcp-remote` clients

## Build

```bash
dotnet build src/RhinoAIMCP/RhinoAIMCP.csproj -c Release
```

Output `.rhp`:

```
src/RhinoAIMCP/bin/Release/net8.0/RhinoAIMCP.rhp
```

### Optional Yak package

```bash
mkdir -p dist/net8.0
cp src/RhinoAIMCP/bin/Release/net8.0/RhinoAIMCP.rhp dist/net8.0/
cp src/RhinoAIMCP/bin/Release/net8.0/*.dll dist/net8.0/ 2>/dev/null || true
cp manifest.yml dist/
# macOS:
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" build --platform any
# run yak from the dist/ directory
```

## Install in Rhino

1. Build the project (above).
2. In Rhino: **Tools → Options → Plug-ins → Install** (Windows) or drag the `.rhp` onto the Rhino window / use Plug-ins preferences (macOS), and select `RhinoAIMCP.rhp`.
3. Restart Rhino if prompted.
4. In the command line, run: `RhinoAiMCP`
5. You should see: `MCP server started: http://localhost:4001`

## Connect Claude Desktop

Merge [`examples/claude_desktop_config.json`](examples/claude_desktop_config.json) into your Claude Desktop MCP config:

```json
{
  "mcpServers": {
    "rhinoceros3d": {
      "command": "npx",
      "args": ["mcp-remote", "http://localhost:4001/mcp"]
    }
  }
}
```

Restart Claude Desktop. With Rhino open and `RhinoAiMCP` running, ask: *“Create a 10×10×10 box at the origin in Rhino.”*

## Cursor / other MCP clients

Point any MCP client that supports remote Streamable HTTP (via `mcp-remote`) at:

```
http://localhost:4001/mcp
```

## MCP tools

| Tool | Description |
|------|-------------|
| `get_document_info` | Units, layers, counts, selection |
| `get_objects` | List/filter objects |
| `create_box` / `create_sphere` / `create_cylinder` | Add solids |
| `delete_objects` | Delete by GUID list |
| `execute_csharp` | Run a short RhinoCommon C# script (`Doc` global) |

## Verify without a live agent

With the server running in Rhino, you can confirm the HTTP listener:

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:4001/mcp
```

A non-connection-refused response means the host is up (MCP clients speak JSON-RPC over Streamable HTTP).

## Not in v0.1

Grasshopper tools, Rhino 7, remote auth/tunnels, full geometry catalog.

## License

MIT — see [LICENSE](LICENSE).

## Citations

- Public product docs: [RhinoAiMCP — Food4Rhino](https://www.food4rhino.com/en/app/rhinoaimcp)
- Announcement context: Reddit `/r/rhino` post introducing RhinoAiMCP (RhinoPort)
- Architecture peers (not forked): [jingcheng-chen/rhinomcp](https://github.com/jingcheng-chen/rhinomcp), [mcneel/RhinoAI](https://github.com/mcneel/RhinoAI)
