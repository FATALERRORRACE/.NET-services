using System.Text.Json.Serialization;
using Library.Core;
using Library.Core.Models;
using Library.Core.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLibraryServices(o => builder.Configuration.GetSection("Library").Bind(o));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Las violaciones de reglas de negocio se devuelven como 400 / 404.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (NotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (LibraryException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

// Autores
var authors = app.MapGroup("/api/authors");
authors.MapGet("/", (IAuthorService s) => s.GetAll());
authors.MapGet("/{id:guid}", (Guid id, IAuthorService s) => s.GetById(id) is { } a ? Results.Ok(a) : Results.NotFound());
authors.MapGet("/{id:guid}/books", (Guid id, IAuthorService s) => s.GetBooks(id));
authors.MapPost("/", (Author a, IAuthorService s) => Results.Created($"/api/authors/{a.Id}", s.Create(a)));
authors.MapPut("/{id:guid}", (Guid id, Author a, IAuthorService s) => { a.Id = id; return s.Update(a); });
authors.MapDelete("/{id:guid}", (Guid id, IAuthorService s) => { s.Delete(id); return Results.NoContent(); });

// Categorías
var categories = app.MapGroup("/api/categories");
categories.MapGet("/", (ICategoryService s) => s.GetAll());
categories.MapPost("/", (Category c, ICategoryService s) => Results.Created($"/api/categories/{c.Id}", s.Create(c)));
categories.MapPut("/{id:guid}", (Guid id, Category c, ICategoryService s) => { c.Id = id; return s.Update(c); });
categories.MapDelete("/{id:guid}", (Guid id, ICategoryService s) => { s.Delete(id); return Results.NoContent(); });

// Libros
var books = app.MapGroup("/api/books");
books.MapGet("/", (string? title, Guid? authorId, Guid? categoryId, IBookService s) => s.Search(title, authorId, categoryId));
books.MapGet("/{id:guid}", (Guid id, IBookService s) => s.GetById(id) is { } b ? Results.Ok(b) : Results.NotFound());
books.MapGet("/isbn/{isbn}", (string isbn, IBookService s) => s.GetByIsbn(isbn) is { } b ? Results.Ok(b) : Results.NotFound());
books.MapPost("/", (Book b, IBookService s) => Results.Created($"/api/books/{b.Id}", s.Create(b)));
books.MapPut("/{id:guid}", (Guid id, Book b, IBookService s) => { b.Id = id; return s.Update(b); });
books.MapDelete("/{id:guid}", (Guid id, IBookService s) => { s.Delete(id); return Results.NoContent(); });

// Inventario (ejemplares)
books.MapGet("/{id:guid}/copies", (Guid id, IInventoryService s) => s.GetCopies(id));
books.MapPost("/{id:guid}/copies", (Guid id, AddCopyRequest r, IInventoryService s) => s.AddCopy(id, r.Barcode));
app.MapPut("/api/copies/{id:guid}/status", (Guid id, CopyStatus status, IInventoryService s) => { s.SetStatus(id, status); return Results.NoContent(); });
app.MapDelete("/api/copies/{id:guid}", (Guid id, IInventoryService s) => { s.RemoveCopy(id); return Results.NoContent(); });

// Socios
var members = app.MapGroup("/api/members");
members.MapGet("/", (bool? onlyActive, IMemberService s) => s.GetAll(onlyActive ?? true));
members.MapGet("/{id:guid}", (Guid id, IMemberService s) => s.GetById(id) is { } m ? Results.Ok(m) : Results.NotFound());
members.MapPost("/", (Member m, IMemberService s) => Results.Created($"/api/members/{m.Id}", s.Register(m)));
members.MapPut("/{id:guid}", (Guid id, Member m, IMemberService s) => { m.Id = id; return s.Update(m); });
members.MapPost("/{id:guid}/deactivate", (Guid id, IMemberService s) => { s.Deactivate(id); return Results.NoContent(); });
members.MapGet("/{id:guid}/loans", (Guid id, ILoanService s) => s.GetHistory(id));
members.MapGet("/{id:guid}/reservations", (Guid id, IReservationService s) => s.GetByMember(id));
members.MapGet("/{id:guid}/fines", (Guid id, IFineService s) => s.GetUnpaid(id));
members.MapGet("/{id:guid}/notifications", (Guid id, INotificationService s) => s.GetByMember(id));

// Préstamos
var loans = app.MapGroup("/api/loans");
loans.MapPost("/", (LoanRequest r, ILoanService s) => s.Lend(r.BookId, r.MemberId));
loans.MapPost("/{id:guid}/return", (Guid id, ILoanService s) => s.Return(id));
loans.MapPost("/{id:guid}/renew", (Guid id, ILoanService s) => s.Renew(id));
loans.MapGet("/overdue", (ILoanService s) => s.GetOverdueLoans());

// Reservas
var reservations = app.MapGroup("/api/reservations");
reservations.MapPost("/", (LoanRequest r, IReservationService s) => s.Reserve(r.BookId, r.MemberId));
reservations.MapDelete("/{id:guid}", (Guid id, IReservationService s) => { s.Cancel(id); return Results.NoContent(); });
reservations.MapGet("/book/{bookId:guid}", (Guid bookId, IReservationService s) => s.GetQueue(bookId));

// Multas
app.MapPost("/api/fines/{id:guid}/pay", (Guid id, IFineService s) => { s.Pay(id); return Results.NoContent(); });

// Notificaciones
app.MapPost("/api/notifications/reminders", (int? daysBefore, INotificationService s) =>
    new { sent = s.SendDueDateReminders(daysBefore ?? 2) });

// Informes
var reports = app.MapGroup("/api/reports");
reports.MapGet("/top-books", (int? top, IReportService s) =>
    s.MostBorrowedBooks(top ?? 10).Select(x => new { x.Book, x.Loans }));
reports.MapGet("/top-members", (int? top, IReportService s) =>
    s.MostActiveMembers(top ?? 10).Select(x => new { x.Member, x.Loans }));
reports.MapGet("/inventory", (IReportService s) => s.GetInventorySummary());
reports.MapGet("/fines", (DateTime from, DateTime to, IReportService s) => new { total = s.TotalFinesCollected(from, to) });

app.Run();

public record AddCopyRequest(string Barcode);
public record LoanRequest(Guid BookId, Guid MemberId);

public partial class Program;
