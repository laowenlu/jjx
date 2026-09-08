namespace Contracts.Domain.Database;

/// <summary>
/// Abstract base class for all database persistence entities in the application.
/// Provides a unique Guid identifier used as the primary key for document storage.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity (used as a document key in MartenDB).
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
