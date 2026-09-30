using Library.Core.Models;

namespace Library.Core.Services;

/// <summary>1. Catálogo de libros: alta, edición, baja y búsqueda.</summary>
public interface IBookService
{
    Book Create(Book book);
    Book Update(Book book);
    void Delete(Guid id);
    Book? GetById(Guid id);
    Book? GetByIsbn(string isbn);
    IReadOnlyList<Book> Search(string? title = null, Guid? authorId = null, Guid? categoryId = null);
}

/// <summary>2. Gestión de autores.</summary>
public interface IAuthorService
{
    Author Create(Author author);
    Author Update(Author author);
    void Delete(Guid id);
    Author? GetById(Guid id);
    IReadOnlyList<Author> GetAll();
    IReadOnlyList<Book> GetBooks(Guid authorId);
}

/// <summary>3. Gestión de categorías / géneros.</summary>
public interface ICategoryService
{
    Category Create(Category category);
    Category Update(Category category);
    void Delete(Guid id);
    Category? GetById(Guid id);
    IReadOnlyList<Category> GetAll();
}

/// <summary>4. Inventario de ejemplares físicos.</summary>
public interface IInventoryService
{
    BookCopy AddCopy(Guid bookId, string barcode);
    void RemoveCopy(Guid copyId);
    void SetStatus(Guid copyId, CopyStatus status);
    IReadOnlyList<BookCopy> GetCopies(Guid bookId);
    int CountAvailable(Guid bookId);
    BookCopy? FindAvailableCopy(Guid bookId);
}

/// <summary>5. Socios / usuarios de la librería.</summary>
public interface IMemberService
{
    Member Register(Member member);
    Member Update(Member member);
    void Deactivate(Guid id);
    Member? GetById(Guid id);
    Member? GetByEmail(string email);
    IReadOnlyList<Member> GetAll(bool onlyActive = true);
}

/// <summary>6. Préstamos, devoluciones y renovaciones.</summary>
public interface ILoanService
{
    Loan Lend(Guid bookId, Guid memberId);
    Loan Return(Guid loanId);
    Loan Renew(Guid loanId);
    IReadOnlyList<Loan> GetActiveLoans(Guid memberId);
    IReadOnlyList<Loan> GetOverdueLoans();
    IReadOnlyList<Loan> GetHistory(Guid memberId);
}

/// <summary>7. Reservas de libros sin ejemplares disponibles.</summary>
public interface IReservationService
{
    Reservation Reserve(Guid bookId, Guid memberId);
    void Cancel(Guid reservationId);
    /// <summary>Marca como "lista" la siguiente reserva pendiente del libro (cola FIFO).</summary>
    Reservation? ProcessNext(Guid bookId);
    Reservation? GetReadyReservation(Guid bookId, Guid memberId);
    void Fulfill(Guid reservationId);
    IReadOnlyList<Reservation> GetQueue(Guid bookId);
    IReadOnlyList<Reservation> GetByMember(Guid memberId);
}

/// <summary>8. Multas por retraso.</summary>
public interface IFineService
{
    decimal Calculate(Loan loan);
    Fine? IssueForLoan(Loan loan);
    void Pay(Guid fineId);
    IReadOnlyList<Fine> GetUnpaid(Guid memberId);
    decimal GetBalance(Guid memberId);
}

/// <summary>9. Notificaciones a socios (vencimientos, reservas, multas).</summary>
public interface INotificationService
{
    Notification Send(Guid memberId, string subject, string message);
    IReadOnlyList<Notification> GetByMember(Guid memberId);
    int SendDueDateReminders(int daysBefore = 2);
}

/// <summary>10. Informes y estadísticas.</summary>
public interface IReportService
{
    IReadOnlyList<(Book Book, int Loans)> MostBorrowedBooks(int top = 10);
    IReadOnlyList<(Member Member, int Loans)> MostActiveMembers(int top = 10);
    InventorySummary GetInventorySummary();
    decimal TotalFinesCollected(DateTime from, DateTime to);
}

public record InventorySummary(int Titles, int Copies, int Available, int Loaned, int Reserved, int Damaged, int Lost);
