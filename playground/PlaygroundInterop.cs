using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Scenetake.Playground;

/// <summary>
/// The API that worker.js calls. Results are JSON strings; the cast and the SVG of the last render are taken
/// separately as UTF-8 bytes (<see cref="TakeCast"/>, <see cref="TakeSvg"/>), so a large SVG is never escaped
/// into JSON or copied into a JavaScript string.
/// </summary>
/// <remarks>
/// Every export catches every exception: one that crosses the interop boundary stops the runtime, and the page
/// would need a reload.
/// </remarks>
public static partial class PlaygroundInterop
{
    private static StepOutputs s_outputs = new() { Scenetake = "", Os = "", Shell = "", Width = 0, Height = 0, Steps = [] };
    private static byte[] s_cast = [];
    private static byte[] s_svg = [];
    private static readonly Dictionary<string, byte[]> s_images = new(StringComparer.Ordinal);

    [JSExport]
    public static string GetProductVersion()
    {
        try
        {
            return typeof(PlaygroundInterop).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>Makes <paramref name="json"/> the outputs file that later renders replay. Returns its header, or an error.</summary>
    [JSExport]
    public static string LoadOutputs(string? json)
    {
        try
        {
            var outputs = StepOutputs.Parse(json ?? "");
            s_outputs = outputs;
            return WriteJson(writer =>
            {
                writer.WriteString("scenetake", outputs.Scenetake);
                writer.WriteString("os", outputs.Os);
                writer.WriteString("shell", outputs.Shell);
                writer.WriteNumber("width", outputs.Width);
                writer.WriteNumber("height", outputs.Height);
                writer.WriteNumber("steps", outputs.Steps.Count);
            });
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    /// <summary>
    /// Keeps <paramref name="data"/> as the image <paramref name="name"/>, which a background in the YAML or the
    /// options names. A name set again replaces its image. Returns an empty object, or an error. The name is a file
    /// name without a folder: a background finds an image by the file name of its path.
    /// </summary>
    [JSExport]
    public static string SetImage(string? name, byte[]? data)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.IndexOfAny(['/', '\\']) >= 0)
                return ErrorJson($"invalid image name: '{name}'");
            if (!RenderSettingsResolver.IsBackgroundImageName(name))
                return ErrorJson($"a background image must be a .png, .jpg, .gif, or .webp file: {name}");
            if (data is null || data.Length == 0)
                return ErrorJson($"the image is empty: {name}");
            if (data.LongLength > RenderSettingsResolver.MaxBackgroundImageBytes)
                return ErrorJson($"the image is larger than {RenderSettingsResolver.MaxBackgroundImageBytes / (1024 * 1024)} MB, and every SVG embeds it: {name}");

            s_images[name] = data;
            return "{}";
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    /// <summary>Composes the cast and SVG of <paramref name="yaml"/> from the loaded outputs file.</summary>
    [JSExport]
    public static string RenderScenario(string? yaml, string? optionsJson)
    {
        try
        {
            var result = PlaygroundRender.RenderScenario(yaml ?? "", s_outputs, ParseOptions(optionsJson));
            s_cast = result.Cast ?? [];
            s_svg = result.Svg ?? [];
            return ResultJson(result);
        }
        catch (Exception ex)
        {
            s_cast = [];
            s_svg = [];
            return ErrorJson($"render failed: {ex.Message}");
        }
    }

    /// <summary>Renders a cast file to SVG, as the svg command does.</summary>
    [JSExport]
    public static string RenderCast(string? castText, string? optionsJson)
    {
        try
        {
            var result = PlaygroundRender.RenderCast(castText ?? "", ParseOptions(optionsJson));
            s_cast = [];
            s_svg = result.Svg ?? [];
            return ResultJson(result);
        }
        catch (Exception ex)
        {
            s_svg = [];
            return ErrorJson($"render failed: {ex.Message}");
        }
    }

    [JSExport]
    public static byte[] TakeCast()
    {
        var cast = s_cast;
        s_cast = [];
        return cast;
    }

    [JSExport]
    public static byte[] TakeSvg()
    {
        var svg = s_svg;
        s_svg = [];
        return svg;
    }

    private static PlaygroundOptions ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PlaygroundOptions();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new PlaygroundOptions(
            Theme: String(root, "theme"),
            Window: String(root, "window"),
            FontSize: Int(root, "fontSize"),
            FontFamily: String(root, "fontFamily"),
            MaxFps: Int(root, "maxFps"),
            WindowTitle: String(root, "windowTitle"),
            Background: String(root, "background"),
            Margin: Int(root, "margin"),
            Padding: Int(root, "padding"),
            Images: s_images,
            Opacity: Double(root, "opacity"),
            Blur: Int(root, "blur"));

        static string? String(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

        static int? Int(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

        static double? Double(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
    }

    private static string ResultJson(PlaygroundResult result) => WriteJson(writer =>
    {
        if (result.Error is not null)
            writer.WriteString("error", result.Error);
        writer.WriteNumber("castBytes", result.Cast?.Length ?? 0);
        writer.WriteNumber("svgBytes", result.Svg?.Length ?? 0);
        WriteStrings(writer, "warnings", result.Warnings);
        WriteStrings(writer, "notApplied", result.NotApplied);
        writer.WriteStartArray("steps");
        foreach (var step in result.Steps)
        {
            writer.WriteStartObject();
            writer.WriteNumber("index", step.Index);
            writer.WriteString("run", step.Run);
            writer.WriteString("kind", step.Kind);
            writer.WriteBoolean("recorded", step.Recorded);
            WriteStrings(writer, "notices", step.Notices);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    private static string ErrorJson(string message) => WriteJson(writer => writer.WriteString("error", message));

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static string WriteJson(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}
