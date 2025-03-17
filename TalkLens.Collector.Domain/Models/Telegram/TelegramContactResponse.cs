using System.Text.Json.Serialization;

namespace TalkLens.Collector.Domain.Models.Telegram;

public class TelegramContactResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;
    
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
} 