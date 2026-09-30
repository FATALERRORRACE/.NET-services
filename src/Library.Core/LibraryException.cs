namespace Library.Core;

/// <summary>Error de regla de negocio de la librería.</summary>
public class LibraryException(string message) : Exception(message);

public class NotFoundException(string entity, Guid id)
    : LibraryException($"{entity} con id '{id}' no encontrado.");
