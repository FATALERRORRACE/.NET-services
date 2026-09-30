namespace Library.Core.Models;

public interface IEntity
{
    Guid Id { get; }
}

public class Author : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Nationality { get; set; }
    public DateOnly? BirthDate { get; set; }
}

public class Category : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Book : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Isbn { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public Guid CategoryId { get; set; }
    public string? Publisher { get; set; }
    public int? Year { get; set; }
    public decimal Price { get; set; }
}

public enum CopyStatus
{
    Available,
    Loaned,
    Reserved,
    Damaged,
    Lost
}

/// <summary>Ejemplar físico de un libro.</summary>
public class BookCopy : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public CopyStatus Status { get; set; } = CopyStatus.Available;
}

public class Member : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public int MaxActiveLoans { get; set; } = 3;
}

public class Loan : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CopyId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime LoanDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int Renewals { get; set; }

    public bool IsActive => ReturnDate is null;
    public bool IsOverdue(DateTime now) => IsActive && now > DueDate;
}

public enum ReservationStatus
{
    Pending,
    Ready,
    Fulfilled,
    Cancelled,
    Expired
}

public class Reservation : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public Guid MemberId { get; set; }
    /// <summary>Ejemplar apartado cuando la reserva pasa a estado Ready.</summary>
    public Guid? CopyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
}

public class Fine : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Guid LoanId { get; set; }
    public decimal Amount { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public bool IsPaid => PaidAt is not null;
}

public class Notification : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
