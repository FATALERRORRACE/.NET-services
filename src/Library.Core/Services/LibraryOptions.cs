namespace Library.Core.Services;

/// <summary>Parámetros configurables de las reglas de negocio.</summary>
public class LibraryOptions
{
    public int LoanDays { get; set; } = 14;
    public int MaxRenewals { get; set; } = 2;
    public decimal DailyFine { get; set; } = 0.50m;
    /// <summary>Importe de multas pendientes a partir del cual se bloquean nuevos préstamos.</summary>
    public decimal MaxUnpaidFines { get; set; } = 10m;
}
