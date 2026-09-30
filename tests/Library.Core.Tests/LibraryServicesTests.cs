using Library.Core;
using Library.Core.Models;
using Library.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Core.Tests;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
}

public class LibraryServicesTests
{
    private readonly FakeClock _clock = new();
    private readonly IServiceProvider _sp;

    public LibraryServicesTests()
    {
        var services = new ServiceCollection().AddLibraryServices();
        services.AddSingleton<IClock>(_clock);
        _sp = services.BuildServiceProvider();
    }

    private T Get<T>() where T : notnull => _sp.GetRequiredService<T>();

    private Book CreateBook(int copies = 1)
    {
        var author = Get<IAuthorService>().Create(new Author { Name = "Gabriel García Márquez" });
        var category = Get<ICategoryService>().Create(new Category { Name = $"Novela {Guid.NewGuid()}" });
        var book = Get<IBookService>().Create(new Book
        {
            Isbn = Guid.NewGuid().ToString("N"),
            Title = "Cien años de soledad",
            AuthorId = author.Id,
            CategoryId = category.Id
        });
        for (var i = 0; i < copies; i++)
            Get<IInventoryService>().AddCopy(book.Id, $"{book.Isbn}-{i}");
        return book;
    }

    private Member CreateMember(string email) =>
        Get<IMemberService>().Register(new Member { FullName = email, Email = email });

    [Fact]
    public void Book_with_duplicate_isbn_is_rejected()
    {
        var book = CreateBook();
        var dup = new Book { Isbn = book.Isbn, Title = "Otro", AuthorId = book.AuthorId, CategoryId = book.CategoryId };

        Assert.Throws<LibraryException>(() => Get<IBookService>().Create(dup));
    }

    [Fact]
    public void Search_filters_by_title()
    {
        CreateBook();

        Assert.Single(Get<IBookService>().Search(title: "soledad"));
        Assert.Empty(Get<IBookService>().Search(title: "quijote"));
    }

    [Fact]
    public void Lend_and_return_updates_copy_status()
    {
        var book = CreateBook();
        var member = CreateMember("ana@example.com");
        var loans = Get<ILoanService>();
        var inventory = Get<IInventoryService>();

        var loan = loans.Lend(book.Id, member.Id);
        Assert.Equal(0, inventory.CountAvailable(book.Id));
        Assert.Equal(_clock.UtcNow.AddDays(14), loan.DueDate);

        loans.Return(loan.Id);
        Assert.Equal(1, inventory.CountAvailable(book.Id));
        Assert.Empty(loans.GetActiveLoans(member.Id));
    }

    [Fact]
    public void Lend_fails_when_no_copies_available()
    {
        var book = CreateBook();
        Get<ILoanService>().Lend(book.Id, CreateMember("a@example.com").Id);

        Assert.Throws<LibraryException>(() => Get<ILoanService>().Lend(book.Id, CreateMember("b@example.com").Id));
    }

    [Fact]
    public void Lend_fails_when_member_reaches_loan_limit()
    {
        var member = Get<IMemberService>().Register(new Member { FullName = "Ana", Email = "ana@example.com", MaxActiveLoans = 1 });
        Get<ILoanService>().Lend(CreateBook().Id, member.Id);

        Assert.Throws<LibraryException>(() => Get<ILoanService>().Lend(CreateBook().Id, member.Id));
    }

    [Fact]
    public void Late_return_generates_fine_and_blocks_new_loans_over_limit()
    {
        var book = CreateBook();
        var member = CreateMember("ana@example.com");
        var loan = Get<ILoanService>().Lend(book.Id, member.Id);

        _clock.UtcNow = loan.DueDate.AddDays(20); // 20 días * 0,50 = 10
        Get<ILoanService>().Return(loan.Id);

        Assert.Equal(10m, Get<IFineService>().GetBalance(member.Id));
        Assert.Throws<LibraryException>(() => Get<ILoanService>().Lend(book.Id, member.Id));

        var fine = Get<IFineService>().GetUnpaid(member.Id).Single();
        Get<IFineService>().Pay(fine.Id);
        Assert.Equal(0m, Get<IFineService>().GetBalance(member.Id));
        Assert.Equal(10m, Get<IReportService>().TotalFinesCollected(DateTime.MinValue, DateTime.MaxValue));
    }

    [Fact]
    public void Renew_extends_due_date_until_max_renewals()
    {
        var loans = Get<ILoanService>();
        var loan = loans.Lend(CreateBook().Id, CreateMember("ana@example.com").Id);
        var originalDue = loan.DueDate;

        loans.Renew(loan.Id);
        loans.Renew(loan.Id);

        Assert.Equal(originalDue.AddDays(28), loan.DueDate);
        Assert.Throws<LibraryException>(() => loans.Renew(loan.Id));
    }

    [Fact]
    public void Returned_copy_is_held_for_next_reservation()
    {
        var book = CreateBook();
        var ana = CreateMember("ana@example.com");
        var luis = CreateMember("luis@example.com");
        var pedro = CreateMember("pedro@example.com");
        var loans = Get<ILoanService>();
        var reservations = Get<IReservationService>();

        var loan = loans.Lend(book.Id, ana.Id);
        var reservation = reservations.Reserve(book.Id, luis.Id);
        Assert.Throws<LibraryException>(() => loans.Renew(loan.Id));

        loans.Return(loan.Id);

        Assert.Equal(ReservationStatus.Ready, reservation.Status);
        Assert.Throws<LibraryException>(() => loans.Lend(book.Id, pedro.Id));

        loans.Lend(book.Id, luis.Id);
        Assert.Equal(ReservationStatus.Fulfilled, reservation.Status);
        Assert.Contains(Get<INotificationService>().GetByMember(luis.Id), n => n.Subject == "Reserva disponible");
    }

    [Fact]
    public void Reserve_fails_when_copies_are_available()
    {
        var book = CreateBook();

        Assert.Throws<LibraryException>(() => Get<IReservationService>().Reserve(book.Id, CreateMember("a@example.com").Id));
    }

    [Fact]
    public void Due_date_reminders_are_sent_for_loans_due_soon()
    {
        var member = CreateMember("ana@example.com");
        var loan = Get<ILoanService>().Lend(CreateBook().Id, member.Id);

        _clock.UtcNow = loan.DueDate.AddDays(-1);

        Assert.Equal(1, Get<INotificationService>().SendDueDateReminders(2));
    }

    [Fact]
    public void Reports_summarize_inventory_and_activity()
    {
        var book = CreateBook(copies: 3);
        var member = CreateMember("ana@example.com");
        Get<ILoanService>().Lend(book.Id, member.Id);

        var summary = Get<IReportService>().GetInventorySummary();
        Assert.Equal(3, summary.Copies);
        Assert.Equal(2, summary.Available);
        Assert.Equal(1, summary.Loaned);

        Assert.Equal(book.Id, Get<IReportService>().MostBorrowedBooks().Single().Book.Id);
        Assert.Equal(member.Id, Get<IReportService>().MostActiveMembers().Single().Member.Id);
    }
}
