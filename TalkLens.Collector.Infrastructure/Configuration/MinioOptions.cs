namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки для Minio хранилища
/// </summary>
public class MinioOptions
{
    /// <summary>
    /// Эндпоинт для подключения к Minio
    /// </summary>
    public string Endpoint { get; set; } = "localhost:9000";
    
    /// <summary>
    /// Ключ доступа
    /// </summary>
    public string AccessKey { get; set; } = "minioadmin";
    
    /// <summary>
    /// Секретный ключ
    /// </summary>
    public string SecretKey { get; set; } = "minioadmin";
    
    /// <summary>
    /// Использовать SSL
    /// </summary>
    public bool WithSSL { get; set; } = false;
    
    /// <summary>
    /// Имя бакета
    /// </summary>
    public string BucketName { get; set; } = "talklens";
    
    /// <summary>
    /// Временная директория
    /// </summary>
    public string TempDirectory { get; set; } = "TelegramSessionCache";
} 