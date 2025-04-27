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

    [JsonPropertyName("username")]
    public string? Username { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("has_photo")]
    public bool HasPhoto { get; set; }
    
    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; set; }
} 