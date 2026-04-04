using System.ComponentModel.DataAnnotations.Schema;

namespace IzaleSparkle.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// [NotMapped] on DomainEvents tells EF Core to ignore it entirely —
/// BaseEvent is a domain-only concept and must never become a DB table.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    private readonly List<BaseEvent> _domainEvents = new();

    /// <summary>
    /// In-memory domain events. [NotMapped] prevents EF Core from
    /// scanning BaseEvent and trying to create a table for it.
    /// </summary>
    [NotMapped]
    public IReadOnlyList<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(BaseEvent evt)    => _domainEvents.Add(evt);
    public void RemoveDomainEvent(BaseEvent evt) => _domainEvents.Remove(evt);
    public void ClearDomainEvents()              => _domainEvents.Clear();
}

/// <summary>
/// Marker base for all domain events. Not persisted — in-memory only.
/// </summary>
public abstract class BaseEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
