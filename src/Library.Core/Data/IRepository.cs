using System.Collections.Concurrent;
using Library.Core.Models;

namespace Library.Core.Data;

public interface IRepository<T> where T : class, IEntity
{
    T? GetById(Guid id);
    IReadOnlyList<T> GetAll();
    IReadOnlyList<T> Find(Func<T, bool> predicate);
    T Add(T entity);
    T Update(T entity);
    bool Remove(Guid id);
}

/// <summary>Repositorio en memoria. Sustituible por una implementación con EF Core.</summary>
public class InMemoryRepository<T> : IRepository<T> where T : class, IEntity
{
    private readonly ConcurrentDictionary<Guid, T> _items = new();

    public T? GetById(Guid id) => _items.GetValueOrDefault(id);

    public IReadOnlyList<T> GetAll() => _items.Values.ToList();

    public IReadOnlyList<T> Find(Func<T, bool> predicate) => _items.Values.Where(predicate).ToList();

    public T Add(T entity)
    {
        if (!_items.TryAdd(entity.Id, entity))
            throw new LibraryException($"Ya existe una entidad {typeof(T).Name} con id '{entity.Id}'.");
        return entity;
    }

    public T Update(T entity)
    {
        if (!_items.ContainsKey(entity.Id))
            throw new NotFoundException(typeof(T).Name, entity.Id);
        _items[entity.Id] = entity;
        return entity;
    }

    public bool Remove(Guid id) => _items.TryRemove(id, out _);
}
