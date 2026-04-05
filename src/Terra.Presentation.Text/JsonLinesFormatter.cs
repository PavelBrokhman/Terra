using System.Text.Json;
using System.Text.Json.Serialization;
using Terra.Engine.Events;

namespace Terra.Presentation.Text;

/// <summary>
/// JSON-Lines formatter: one JSON object per line, with a <c>"type"</c>
/// discriminator property naming the event subclass. This is the public,
/// versioned wire format for the event stream — any downstream client
/// (text viewer, 2D/3D renderer, replay tool, analytics pipeline) can
/// consume it identically.
/// </summary>
public sealed class JsonLinesFormatter : IEventFormatter
{
    private readonly JsonSerializerOptions _options;

    public JsonLinesFormatter()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() },
        };
    }

    public string? Format(SimulationEvent evt) =>
        JsonSerializer.Serialize<SimulationEvent>(evt, _options);
}
