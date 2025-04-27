using System;
using System.Threading.Tasks;

namespace TalkLens.Collector.Infrastructure.Cache;

/// <summary>
/// Определяет интерфейс провайдера кэша
/// </summary>
/// <typeparam name="T">Тип кэшируемых данных</typeparam>
public interface ICacheProvider<T>
{
    /// <summary>
    /// Получает объект из кэша или создает новый с помощью фабрики
    /// </summary>
    /// <param name="key">Ключ для кэширования</param>
    /// <param name="factory">Фабрика для создания значения</param>
    /// <param name="expirationTime">Время действия объекта в кэше</param>
    /// <returns>Кэшированное или созданное значение</returns>
    Task<T> GetOrCreateAsync(string key, Func<Task<T>> factory, TimeSpan expirationTime);
    
    /// <summary>
    /// Получает объект из кэша
    /// </summary>
    /// <param name="key">Ключ кэша</param>
    /// <returns>Объект или значение по умолчанию, если объект не найден</returns>
    Task<T?> GetAsync(string key);
    
    /// <summary>
    /// Сохраняет объект в кэш
    /// </summary>
    /// <param name="key">Ключ для сохранения</param>
    /// <param name="value">Значение для сохранения</param>
    /// <param name="expirationTime">Время действия объекта в кэше</param>
    Task SetAsync(string key, T value, TimeSpan expirationTime);
    
    /// <summary>
    /// Удаляет объект из кэша
    /// </summary>
    /// <param name="key">Ключ для удаления</param>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Проверяет наличие ключа в кэше
    /// </summary>
    /// <param name="key">Ключ для проверки</param>
    /// <returns>True, если ключ существует, иначе False</returns>
    Task<bool> ExistsAsync(string key);
} 