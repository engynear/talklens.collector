using System;
using System.Collections.Generic;

namespace TalkLens.Collector.Domain.Models;

/// <summary>
/// Базовая модель данных для всех типов сессий
/// </summary>
public class SessionData
{
    /// <summary>
    /// Уникальный идентификатор сессии
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Идентификатор пользователя, владеющего сессией
    /// </summary>
    public string UserId { get; set; }
    
    /// <summary>
    /// Идентификатор сессии (клиентский ID)
    /// </summary>
    public string SessionId { get; set; }
    
    /// <summary>
    /// Дата создания сессии
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Дата последней активности сессии
    /// </summary>
    public DateTime LastActivityAt { get; set; }
    
    /// <summary>
    /// Флаг активности сессии
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Тип сессии (например, "Telegram", "WhatsApp" и т.д.)
    /// </summary>
    public string SessionType { get; set; }
}