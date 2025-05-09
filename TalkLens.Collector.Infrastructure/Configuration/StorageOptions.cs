namespace TalkLens.Collector.Infrastructure.Configuration;

/// <summary>
/// Настройки для хранилища
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// Имя секции в конфигурации приложения
    /// </summary>
    public const string SectionName = "Storage";
    
    /// <summary>
    /// Провайдер хранилища
    /// </summary>
    public string Provider { get; set; } = "Minio";
    
    /// <summary>
    /// Использовать удаленное хранилище
    /// </summary>
    public bool UseRemoteStorage { get; set; } = false;
    
    /// <summary>
    /// Пропускать сохранение в Minio
    /// </summary>
    public bool SkipMinioSave { get; set; } = false;
    
    /// <summary>
    /// Настройки Minio
    /// </summary>
    public MinioOptions Minio { get; set; } = new();
} 