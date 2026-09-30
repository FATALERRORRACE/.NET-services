namespace Library.Core.Services;

/// <summary>Abstracción del reloj para poder probar reglas basadas en fechas.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
