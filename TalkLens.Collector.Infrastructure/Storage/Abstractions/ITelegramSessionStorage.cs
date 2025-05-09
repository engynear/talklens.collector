namespace TalkLens.Collector.Infrastructure.Storage.Abstractions
{
    /// <summary>
    /// Интерфейс для хранилища сессий Telegram
    /// </summary>
    public interface ITelegramSessionStorage
    {
        /// <summary>
        /// Проверяет существование сессии по идентификатору телефона
        /// </summary>
        Task<bool> ExistsAsync(string phoneNumber);

        /// <summary>
        /// Получает сессию Telegram по идентификатору телефона и сохраняет её во временный файл
        /// </summary>
        Task<string> GetSessionFilePathAsync(string phoneNumber);

        /// <summary>
        /// Сохраняет сессию Telegram из файла в хранилище
        /// </summary>
        Task SaveSessionAsync(string phoneNumber, string filePath);

        /// <summary>
        /// Удаляет сессию Telegram по идентификатору телефона
        /// </summary>
        Task DeleteSessionAsync(string phoneNumber);
    }
} 