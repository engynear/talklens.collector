namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки для JWT аутентификации
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Имя секции в конфигурации приложения
    /// </summary>
    public const string SectionName = "Jwt";
    
    /// <summary>
    /// Издатель токена
    /// </summary>
    public string Issuer { get; set; }
    
    /// <summary>
    /// Аудитория токена
    /// </summary>
    public string Audience { get; set; }
    
    /// <summary>
    /// Секретный ключ для подписи токена
    /// </summary>
    public string SecretKey { get; set; }
} 