namespace TalkLens.Collector.Domain.Enums.Telegram;

/// <summary>
/// Тип привязанности в общении
/// </summary>
public enum AttachmentType
{
    /// <summary>
    /// Надежная привязанность
    /// </summary>
    Secure,
    
    /// <summary>
    /// Тревожная привязанность
    /// </summary>
    Anxious,
    
    /// <summary>
    /// Избегающая привязанность
    /// </summary>
    Avoidant
} 