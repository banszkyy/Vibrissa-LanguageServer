using System.Text.Json.Serialization;

namespace LanguageServer;

sealed class CompilerStatusNotificationArgs
{
    [JsonPropertyName("status")] public required string Status { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
}
