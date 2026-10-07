using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using RhinoAIMCP.Host;

namespace RhinoAIMCP.Tools;

[McpServerToolType]
public sealed class RhinoTools
{
    [McpServerTool(Name = "get_document_info"), Description("Get units, layers, object count, and selected object IDs from the active Rhino document.")]
    public string GetDocumentInfo()
    {
        return UiThread.Invoke(() =>
        {
            var doc = RhinoDoc.ActiveDoc
                ?? throw new InvalidOperationException("No active Rhino document.");

            var layers = doc.Layers.Select(l => l.FullPath).ToArray();
            var selected = doc.Objects.GetSelectedObjects(false, false)
                .Select(o => o.Id.ToString())
                .ToArray();

            var payload = new
            {
                name = doc.Name,
                path = doc.Path,
                units = doc.ModelUnitSystem.ToString(),
                object_count = doc.Objects.Count,
                layer_count = doc.Layers.Count,
                layers,
                selected_ids = selected,
            };
            return JsonSerializer.Serialize(payload);
        });
    }

    [McpServerTool(Name = "get_objects"), Description("List objects in the document. Optional filters: layer, name, type (e.g. brep, mesh, curve, point).")]
    public string GetObjects(
        [Description("Layer full path filter (substring match, case-insensitive).")] string? layer = null,
        [Description("Object name filter (substring match, case-insensitive).")] string? name = null,
        [Description("Geometry type filter: brep, mesh, curve, point, surface, extrude, unknown.")] string? type = null)
    {
        return UiThread.Invoke(() =>
        {
            var doc = RhinoDoc.ActiveDoc
                ?? throw new InvalidOperationException("No active Rhino document.");

            var items = new List<object>();
            foreach (var obj in doc.Objects)
            {
                if (obj is null || obj.IsDeleted)
                    continue;

                var geomType = Classify(obj.Geometry);
                if (!string.IsNullOrWhiteSpace(type) &&
                    !string.Equals(geomType, type, StringComparison.OrdinalIgnoreCase))
                    continue;

                var layerName = doc.Layers[obj.Attributes.LayerIndex]?.FullPath ?? "";
                if (!string.IsNullOrWhiteSpace(layer) &&
                    layerName.IndexOf(layer, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var objName = obj.Name ?? "";
                if (!string.IsNullOrWhiteSpace(name) &&
                    objName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var bbox = obj.Geometry?.GetBoundingBox(true) ?? BoundingBox.Empty;
                items.Add(new
                {
                    id = obj.Id.ToString(),
                    name = objName,
                    type = geomType,
                    layer = layerName,
                    bbox = bbox.IsValid
                        ? new
                        {
                            min = new[] { bbox.Min.X, bbox.Min.Y, bbox.Min.Z },
                            max = new[] { bbox.Max.X, bbox.Max.Y, bbox.Max.Z },
                        }
                        : null,
                });
            }

            return JsonSerializer.Serialize(new { count = items.Count, objects = items });
        });
    }

    [McpServerTool(Name = "create_box"), Description("Create an axis-aligned box solid in the active document.")]
    public string CreateBox(
        [Description("Minimum X")] double minX = 0,
        [Description("Minimum Y")] double minY = 0,
        [Description("Minimum Z")] double minZ = 0,
        [Description("Maximum X")] double maxX = 1,
        [Description("Maximum Y")] double maxY = 1,
        [Description("Maximum Z")] double maxZ = 1,
        [Description("Optional object name")] string? name = null,
        [Description("Optional layer full path (created if missing)")] string? layer = null)
    {
        return UiThread.Invoke(() =>
        {
            var doc = RequireDoc();
            var box = new Box(new BoundingBox(
                new Point3d(minX, minY, minZ),
                new Point3d(maxX, maxY, maxZ)));
            if (!box.IsValid)
                throw new ArgumentException("Invalid box dimensions.");

            var attrs = BuildAttributes(doc, name, layer);
            var id = doc.Objects.AddBox(box, attrs);
            doc.Views.Redraw();
            return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
        });
    }

    [McpServerTool(Name = "create_sphere"), Description("Create a sphere solid in the active document.")]
    public string CreateSphere(
        [Description("Center X")] double centerX = 0,
        [Description("Center Y")] double centerY = 0,
        [Description("Center Z")] double centerZ = 0,
        [Description("Radius")] double radius = 1,
        [Description("Optional object name")] string? name = null,
        [Description("Optional layer full path (created if missing)")] string? layer = null)
    {
        return UiThread.Invoke(() =>
        {
            var doc = RequireDoc();
            if (radius <= 0)
                throw new ArgumentException("Radius must be positive.");

            var sphere = new Sphere(new Point3d(centerX, centerY, centerZ), radius);
            var attrs = BuildAttributes(doc, name, layer);
            var id = doc.Objects.AddSphere(sphere, attrs);
            doc.Views.Redraw();
            return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
        });
    }

    [McpServerTool(Name = "create_cylinder"), Description("Create a cylinder solid along Z (or a custom axis) in the active document.")]
    public string CreateCylinder(
        [Description("Base center X")] double baseX = 0,
        [Description("Base center Y")] double baseY = 0,
        [Description("Base center Z")] double baseZ = 0,
        [Description("Radius")] double radius = 1,
        [Description("Height along +Z")] double height = 1,
        [Description("Optional object name")] string? name = null,
        [Description("Optional layer full path (created if missing)")] string? layer = null)
    {
        return UiThread.Invoke(() =>
        {
            var doc = RequireDoc();
            if (radius <= 0 || Math.Abs(height) < 1e-12)
                throw new ArgumentException("Radius must be positive and height non-zero.");

            var plane = new Plane(new Point3d(baseX, baseY, baseZ), Vector3d.ZAxis);
            var circle = new Circle(plane, radius);
            var cylinder = new Cylinder(circle, height);
            var brep = cylinder.ToBrep(true, true)
                ?? throw new InvalidOperationException("Failed to create cylinder brep.");

            var attrs = BuildAttributes(doc, name, layer);
            var id = doc.Objects.AddBrep(brep, attrs);
            doc.Views.Redraw();
            return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
        });
    }

    [McpServerTool(Name = "delete_objects"), Description("Delete objects by GUID string list.")]
    public string DeleteObjects(
        [Description("Comma-separated or JSON array of object GUID strings.")] string ids)
    {
        return UiThread.Invoke(() =>
        {
            var doc = RequireDoc();
            var parsed = ParseIds(ids);
            var deleted = new List<string>();
            var missing = new List<string>();

            foreach (var id in parsed)
            {
                if (doc.Objects.Delete(id, true))
                    deleted.Add(id.ToString());
                else
                    missing.Add(id.ToString());
            }

            doc.Views.Redraw();
            return JsonSerializer.Serialize(new { deleted, missing });
        });
    }

    [McpServerTool(Name = "execute_csharp"), Description("Compile and run a short C# snippet against RhinoCommon on the UI thread. The snippet may use 'Doc' (RhinoDoc) and should return a value or null.")]
    public async Task<string> ExecuteCsharp(
        [Description("C# script body. Globals: Doc (RhinoDoc). Example: Doc.Objects.AddPoint(new Point3d(0,0,0)); return \"ok\";")] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("code is required.");

        return await Task.Run(() =>
        {
            return UiThread.Invoke(() =>
            {
                var doc = RequireDoc();
                var options = ScriptOptions.Default
                    .AddReferences(typeof(RhinoDoc).Assembly)
                    .AddReferences(typeof(Point3d).Assembly)
                    .AddImports("System", "System.Linq", "Rhino", "Rhino.Geometry", "Rhino.DocObjects");

                try
                {
                    var result = CSharpScript.EvaluateAsync(
                        code,
                        options,
                        globals: new ScriptGlobals { Doc = doc },
                        globalsType: typeof(ScriptGlobals)).GetAwaiter().GetResult();

                    doc.Views.Redraw();
                    return JsonSerializer.Serialize(new
                    {
                        ok = true,
                        result = result?.ToString(),
                    });
                }
                catch (CompilationErrorException ex)
                {
                    var errors = string.Join("\n", ex.Diagnostics.Select(d => d.ToString()));
                    return JsonSerializer.Serialize(new { ok = false, error = errors });
                }
                catch (Exception ex)
                {
                    return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
                }
            });
        });
    }

    public sealed class ScriptGlobals
    {
        public RhinoDoc Doc { get; set; } = null!;
    }

    private static RhinoDoc RequireDoc() =>
        RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("No active Rhino document.");

    private static ObjectAttributes BuildAttributes(RhinoDoc doc, string? name, string? layer)
    {
        var attrs = new ObjectAttributes();
        if (!string.IsNullOrWhiteSpace(name))
            attrs.Name = name;

        if (!string.IsNullOrWhiteSpace(layer))
        {
            var idx = doc.Layers.FindByFullPath(layer, -1);
            if (idx < 0)
            {
                var layerObj = new Layer { Name = layer };
                idx = doc.Layers.Add(layerObj);
            }
            if (idx >= 0)
                attrs.LayerIndex = idx;
        }

        return attrs;
    }

    private static List<Guid> ParseIds(string ids)
    {
        var trimmed = ids.Trim();
        IEnumerable<string> parts;
        if (trimmed.StartsWith('['))
        {
            var arr = JsonSerializer.Deserialize<string[]>(trimmed) ?? [];
            parts = arr;
        }
        else
        {
            parts = trimmed.Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var result = new List<Guid>();
        foreach (var p in parts)
        {
            if (Guid.TryParse(p, out var g))
                result.Add(g);
            else
                throw new ArgumentException($"Invalid GUID: {p}");
        }
        return result;
    }

    private static string Classify(GeometryBase? geometry) => geometry switch
    {
        null => "unknown",
        Brep => "brep",
        Extrusion => "extrude",
        Mesh => "mesh",
        Curve => "curve",
        Point => "point",
        Surface => "surface",
        _ => geometry.GetType().Name.ToLowerInvariant(),
    };
}
