using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

public class LoanService(
    IRepository<Loan> loans,
    IRepository<Book> books,
    IRepository<BookCopy> copies,
    IMemberService members,
    IInventoryService inventory,
    IReservationService reservations,
    IFineService fines,
    INotificationService notifications,
    IClock clock,
    LibraryOptions options) : ILoanService
{
    public Loan Lend(Guid bookId, Guid memberId)
    {
        var book = books.GetById(bookId) ?? throw new NotFoundException(nameof(Book), bookId);
        var member = members.GetById(memberId) ?? throw new NotFoundException(nameof(Member), memberId);

        if (!member.IsActive) throw new LibraryException("El socio no está activo.");
        if (GetActiveLoans(memberId).Count >= member.MaxActiveLoans)
            throw new LibraryException($"El socio ha alcanzado el máximo de {member.MaxActiveLoans} préstamos.");
        if (fines.GetBalance(memberId) >= options.MaxUnpaidFines)
            throw new LibraryException("El socio tiene multas pendientes que superan el límite permitido.");

        // Si el socio tiene una reserva lista, se le entrega el ejemplar apartado.
        var reservation = reservations.GetReadyReservation(bookId, memberId);
        Guid copyId;
        if (reservation?.CopyId is { } reservedCopyId)
        {
            copyId = reservedCopyId;
            reservations.Fulfill(reservation.Id);
        }
        else
        {
            copyId = inventory.FindAvailableCopy(bookId)?.Id
                     ?? throw new LibraryException($"No hay ejemplares disponibles de '{book.Title}'. Puede reservarlo.");
        }

        inventory.SetStatus(copyId, CopyStatus.Loaned);

        var now = clock.UtcNow;
        var loan = loans.Add(new Loan
        {
            CopyId = copyId,
            MemberId = memberId,
            LoanDate = now,
            DueDate = now.AddDays(options.LoanDays)
        });

        notifications.Send(memberId, "Préstamo registrado",
            $"Has retirado '{book.Title}'. Fecha de devolución: {loan.DueDate:dd/MM/yyyy}.");
        return loan;
    }

    public Loan Return(Guid loanId)
    {
        var loan = loans.GetById(loanId) ?? throw new NotFoundException(nameof(Loan), loanId);
        if (!loan.IsActive) throw new LibraryException("El préstamo ya fue devuelto.");

        loan.ReturnDate = clock.UtcNow;
        loans.Update(loan);

        var fine = fines.IssueForLoan(loan);
        if (fine is not null)
            notifications.Send(loan.MemberId, "Multa por retraso",
                $"Se ha generado una multa de {fine.Amount:0.00} por devolución tardía.");

        var copy = copies.GetById(loan.CopyId) ?? throw new NotFoundException(nameof(BookCopy), loan.CopyId);
        inventory.SetStatus(copy.Id, CopyStatus.Available);
        reservations.ProcessNext(copy.BookId);

        return loan;
    }

    public Loan Renew(Guid loanId)
    {
        var loan = loans.GetById(loanId) ?? throw new NotFoundException(nameof(Loan), loanId);
        if (!loan.IsActive) throw new LibraryException("El préstamo ya fue devuelto.");
        if (loan.IsOverdue(clock.UtcNow)) throw new LibraryException("No se puede renovar un préstamo vencido.");
        if (loan.Renewals >= options.MaxRenewals)
            throw new LibraryException($"Se ha alcanzado el máximo de {options.MaxRenewals} renovaciones.");

        var copy = copies.GetById(loan.CopyId) ?? throw new NotFoundException(nameof(BookCopy), loan.CopyId);
        if (reservations.GetQueue(copy.BookId).Count > 0)
            throw new LibraryException("El libro tiene reservas pendientes y no se puede renovar.");

        loan.DueDate = loan.DueDate.AddDays(options.LoanDays);
        loan.Renewals++;
        return loans.Update(loan);
    }

    public IReadOnlyList<Loan> GetActiveLoans(Guid memberId) =>
        loans.Find(l => l.MemberId == memberId && l.IsActive);

    public IReadOnlyList<Loan> GetOverdueLoans()
    {
        var now = clock.UtcNow;
        return loans.Find(l => l.IsOverdue(now)).OrderBy(l => l.DueDate).ToList();
    }

    public IReadOnlyList<Loan> GetHistory(Guid memberId) =>
        loans.Find(l => l.MemberId == memberId).OrderByDescending(l => l.LoanDate).ToList();
}

public class ReservationService(
    IRepository<Reservation> reservations,
    IRepository<Book> books,
    IMemberService members,
    IInventoryService inventory,
    INotificationService notifications,
    IClock clock) : IReservationService
{
    public Reservation Reserve(Guid bookId, Guid memberId)
    {
        var book = books.GetById(bookId) ?? throw new NotFoundException(nameof(Book), bookId);
        var member = members.GetById(memberId) ?? throw new NotFoundException(nameof(Member), memberId);

        if (!member.IsActive) throw new LibraryException("El socio no está activo.");
        if (inventory.CountAvailable(bookId) > 0)
            throw new LibraryException($"Hay ejemplares disponibles de '{book.Title}'; no es necesario reservar.");
        if (reservations.Find(r => r.BookId == bookId && r.MemberId == memberId &&
                                   r.Status is ReservationStatus.Pending or ReservationStatus.Ready).Count > 0)
            throw new LibraryException("El socio ya tiene una reserva activa para este libro.");

        return reservations.Add(new Reservation { BookId = bookId, MemberId = memberId, CreatedAt = clock.UtcNow });
    }

    public void Cancel(Guid reservationId)
    {
        var reservation = reservations.GetById(reservationId)
                          ?? throw new NotFoundException(nameof(Reservation), reservationId);
        if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Ready))
            throw new LibraryException("La reserva ya no está activa.");

        var wasReady = reservation.Status == ReservationStatus.Ready;
        reservation.Status = ReservationStatus.Cancelled;
        reservations.Update(reservation);

        // Se libera el ejemplar apartado y pasa al siguiente de la cola.
        if (wasReady && reservation.CopyId is { } copyId)
        {
            inventory.SetStatus(copyId, CopyStatus.Available);
            ProcessNext(reservation.BookId);
        }
    }

    public Reservation? ProcessNext(Guid bookId)
    {
        var next = GetQueue(bookId).FirstOrDefault();
        if (next is null) return null;

        var copy = inventory.FindAvailableCopy(bookId);
        if (copy is null) return null;

        inventory.SetStatus(copy.Id, CopyStatus.Reserved);
        next.CopyId = copy.Id;
        next.Status = ReservationStatus.Ready;
        reservations.Update(next);

        var title = books.GetById(bookId)?.Title ?? "el libro";
        notifications.Send(next.MemberId, "Reserva disponible", $"Tu reserva de '{title}' ya puede recogerse.");
        return next;
    }

    public Reservation? GetReadyReservation(Guid bookId, Guid memberId) =>
        reservations.Find(r => r.BookId == bookId && r.MemberId == memberId && r.Status == ReservationStatus.Ready)
            .FirstOrDefault();

    public void Fulfill(Guid reservationId)
    {
        var reservation = reservations.GetById(reservationId)
                          ?? throw new NotFoundException(nameof(Reservation), reservationId);
        reservation.Status = ReservationStatus.Fulfilled;
        reservations.Update(reservation);
    }

    public IReadOnlyList<Reservation> GetQueue(Guid bookId) =>
        reservations.Find(r => r.BookId == bookId && r.Status == ReservationStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToList();

    public IReadOnlyList<Reservation> GetByMember(Guid memberId) =>
        reservations.Find(r => r.MemberId == memberId).OrderByDescending(r => r.CreatedAt).ToList();
}

public class FineService(IRepository<Fine> fines, IClock clock, LibraryOptions options) : IFineService
{
    public decimal Calculate(Loan loan)
    {
        var end = loan.ReturnDate ?? clock.UtcNow;
        var daysLate = (int)Math.Ceiling((end - loan.DueDate).TotalDays);
        return daysLate > 0 ? daysLate * options.DailyFine : 0m;
    }

    public Fine? IssueForLoan(Loan loan)
    {
        var amount = Calculate(loan);
        if (amount <= 0) return null;
        if (fines.Find(f => f.LoanId == loan.Id).Count > 0)
            throw new LibraryException("Ya existe una multa para este préstamo.");

        return fines.Add(new Fine
        {
            LoanId = loan.Id,
            MemberId = loan.MemberId,
            Amount = amount,
            IssuedAt = clock.UtcNow
        });
    }

    public void Pay(Guid fineId)
    {
        var fine = fines.GetById(fineId) ?? throw new NotFoundException(nameof(Fine), fineId);
        if (fine.IsPaid) throw new LibraryException("La multa ya está pagada.");
        fine.PaidAt = clock.UtcNow;
        fines.Update(fine);
    }

    public IReadOnlyList<Fine> GetUnpaid(Guid memberId) =>
        fines.Find(f => f.MemberId == memberId && !f.IsPaid);

    public decimal GetBalance(Guid memberId) => GetUnpaid(memberId).Sum(f => f.Amount);
}

/// <summary>
/// Registra las notificaciones. Para envíos reales (email/SMS) basta con otra implementación
/// de <see cref="INotificationService"/>.
/// </summary>
public class NotificationService(
    IRepository<Notification> notifications,
    IRepository<Loan> loans,
    IClock clock) : INotificationService
{
    public Notification Send(Guid memberId, string subject, string message) =>
        notifications.Add(new Notification
        {
            MemberId = memberId,
            Subject = subject,
            Message = message,
            SentAt = clock.UtcNow
        });

    public IReadOnlyList<Notification> GetByMember(Guid memberId) =>
        notifications.Find(n => n.MemberId == memberId).OrderByDescending(n => n.SentAt).ToList();

    public int SendDueDateReminders(int daysBefore = 2)
    {
        var now = clock.UtcNow;
        var limit = now.AddDays(daysBefore);
        var dueSoon = loans.Find(l => l.IsActive && l.DueDate >= now && l.DueDate <= limit);

        foreach (var loan in dueSoon)
            Send(loan.MemberId, "Recordatorio de devolución",
                $"Tu préstamo vence el {loan.DueDate:dd/MM/yyyy}.");

        return dueSoon.Count;
    }
}

public class ReportService(
    IRepository<Book> books,
    IRepository<BookCopy> copies,
    IRepository<Member> members,
    IRepository<Loan> loans,
    IRepository<Fine> fines) : IReportService
{
    public IReadOnlyList<(Book Book, int Loans)> MostBorrowedBooks(int top = 10)
    {
        var copyToBook = copies.GetAll().ToDictionary(c => c.Id, c => c.BookId);
        return loans.GetAll()
            .Where(l => copyToBook.ContainsKey(l.CopyId))
            .GroupBy(l => copyToBook[l.CopyId])
            .Select(g => (Book: books.GetById(g.Key), Loans: g.Count()))
            .Where(x => x.Book is not null)
            .OrderByDescending(x => x.Loans)
            .Take(top)
            .Select(x => (x.Book!, x.Loans))
            .ToList();
    }

    public IReadOnlyList<(Member Member, int Loans)> MostActiveMembers(int top = 10) =>
        loans.GetAll()
            .GroupBy(l => l.MemberId)
            .Select(g => (Member: members.GetById(g.Key), Loans: g.Count()))
            .Where(x => x.Member is not null)
            .OrderByDescending(x => x.Loans)
            .Take(top)
            .Select(x => (x.Member!, x.Loans))
            .ToList();

    public InventorySummary GetInventorySummary()
    {
        var all = copies.GetAll();
        int Count(CopyStatus s) => all.Count(c => c.Status == s);
        return new InventorySummary(
            books.GetAll().Count, all.Count,
            Count(CopyStatus.Available), Count(CopyStatus.Loaned), Count(CopyStatus.Reserved),
            Count(CopyStatus.Damaged), Count(CopyStatus.Lost));
    }

    public decimal TotalFinesCollected(DateTime from, DateTime to) =>
        fines.Find(f => f.PaidAt is { } paid && paid >= from && paid <= to).Sum(f => f.Amount);
}
