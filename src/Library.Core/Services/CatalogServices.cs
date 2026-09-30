using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

public class BookService(
    IRepository<Book> books,
    IRepository<Author> authors,
    IRepository<Category> categories,
    IRepository<BookCopy> copies) : IBookService
{
    public Book Create(Book book)
    {
        Validate(book);
        if (GetByIsbn(book.Isbn) is not null)
            throw new LibraryException($"Ya existe un libro con ISBN '{book.Isbn}'.");
        return books.Add(book);
    }

    public Book Update(Book book)
    {
        Validate(book);
        var existing = GetByIsbn(book.Isbn);
        if (existing is not null && existing.Id != book.Id)
            throw new LibraryException($"Ya existe un libro con ISBN '{book.Isbn}'.");
        return books.Update(book);
    }

    public void Delete(Guid id)
    {
        if (copies.Find(c => c.BookId == id).Count > 0)
            throw new LibraryException("No se puede eliminar un libro con ejemplares registrados.");
        if (!books.Remove(id))
            throw new NotFoundException(nameof(Book), id);
    }

    public Book? GetById(Guid id) => books.GetById(id);

    public Book? GetByIsbn(string isbn) =>
        books.Find(b => string.Equals(b.Isbn, isbn, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

    public IReadOnlyList<Book> Search(string? title = null, Guid? authorId = null, Guid? categoryId = null) =>
        books.Find(b =>
                (string.IsNullOrWhiteSpace(title) || b.Title.Contains(title, StringComparison.OrdinalIgnoreCase)) &&
                (authorId is null || b.AuthorId == authorId) &&
                (categoryId is null || b.CategoryId == categoryId))
            .OrderBy(b => b.Title)
            .ToList();

    private void Validate(Book book)
    {
        if (string.IsNullOrWhiteSpace(book.Isbn)) throw new LibraryException("El ISBN es obligatorio.");
        if (string.IsNullOrWhiteSpace(book.Title)) throw new LibraryException("El título es obligatorio.");
        if (book.Price < 0) throw new LibraryException("El precio no puede ser negativo.");
        if (authors.GetById(book.AuthorId) is null) throw new NotFoundException(nameof(Author), book.AuthorId);
        if (categories.GetById(book.CategoryId) is null) throw new NotFoundException(nameof(Category), book.CategoryId);
    }
}

public class AuthorService(IRepository<Author> authors, IRepository<Book> books) : IAuthorService
{
    public Author Create(Author author)
    {
        Validate(author);
        return authors.Add(author);
    }

    public Author Update(Author author)
    {
        Validate(author);
        return authors.Update(author);
    }

    public void Delete(Guid id)
    {
        if (GetBooks(id).Count > 0)
            throw new LibraryException("No se puede eliminar un autor con libros asociados.");
        if (!authors.Remove(id))
            throw new NotFoundException(nameof(Author), id);
    }

    public Author? GetById(Guid id) => authors.GetById(id);

    public IReadOnlyList<Author> GetAll() => authors.GetAll().OrderBy(a => a.Name).ToList();

    public IReadOnlyList<Book> GetBooks(Guid authorId) => books.Find(b => b.AuthorId == authorId);

    private static void Validate(Author author)
    {
        if (string.IsNullOrWhiteSpace(author.Name)) throw new LibraryException("El nombre del autor es obligatorio.");
    }
}

public class CategoryService(IRepository<Category> categories, IRepository<Book> books) : ICategoryService
{
    public Category Create(Category category)
    {
        Validate(category);
        return categories.Add(category);
    }

    public Category Update(Category category)
    {
        Validate(category);
        return categories.Update(category);
    }

    public void Delete(Guid id)
    {
        if (books.Find(b => b.CategoryId == id).Count > 0)
            throw new LibraryException("No se puede eliminar una categoría con libros asociados.");
        if (!categories.Remove(id))
            throw new NotFoundException(nameof(Category), id);
    }

    public Category? GetById(Guid id) => categories.GetById(id);

    public IReadOnlyList<Category> GetAll() => categories.GetAll().OrderBy(c => c.Name).ToList();

    private void Validate(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            throw new LibraryException("El nombre de la categoría es obligatorio.");
        if (categories.Find(c => c.Id != category.Id &&
                                 string.Equals(c.Name, category.Name, StringComparison.OrdinalIgnoreCase)).Count > 0)
            throw new LibraryException($"Ya existe la categoría '{category.Name}'.");
    }
}

public class InventoryService(IRepository<BookCopy> copies, IRepository<Book> books) : IInventoryService
{
    public BookCopy AddCopy(Guid bookId, string barcode)
    {
        if (books.GetById(bookId) is null) throw new NotFoundException(nameof(Book), bookId);
        if (string.IsNullOrWhiteSpace(barcode)) throw new LibraryException("El código de barras es obligatorio.");
        if (copies.Find(c => c.Barcode == barcode).Count > 0)
            throw new LibraryException($"El código de barras '{barcode}' ya está registrado.");
        return copies.Add(new BookCopy { BookId = bookId, Barcode = barcode });
    }

    public void RemoveCopy(Guid copyId)
    {
        var copy = copies.GetById(copyId) ?? throw new NotFoundException(nameof(BookCopy), copyId);
        if (copy.Status == CopyStatus.Loaned)
            throw new LibraryException("No se puede dar de baja un ejemplar prestado.");
        copies.Remove(copyId);
    }

    public void SetStatus(Guid copyId, CopyStatus status)
    {
        var copy = copies.GetById(copyId) ?? throw new NotFoundException(nameof(BookCopy), copyId);
        copy.Status = status;
        copies.Update(copy);
    }

    public IReadOnlyList<BookCopy> GetCopies(Guid bookId) => copies.Find(c => c.BookId == bookId);

    public int CountAvailable(Guid bookId) =>
        copies.Find(c => c.BookId == bookId && c.Status == CopyStatus.Available).Count;

    public BookCopy? FindAvailableCopy(Guid bookId) =>
        copies.Find(c => c.BookId == bookId && c.Status == CopyStatus.Available).FirstOrDefault();
}

public class MemberService(IRepository<Member> members, IRepository<Loan> loans) : IMemberService
{
    public Member Register(Member member)
    {
        Validate(member);
        return members.Add(member);
    }

    public Member Update(Member member)
    {
        Validate(member);
        return members.Update(member);
    }

    public void Deactivate(Guid id)
    {
        var member = members.GetById(id) ?? throw new NotFoundException(nameof(Member), id);
        if (loans.Find(l => l.MemberId == id && l.IsActive).Count > 0)
            throw new LibraryException("El socio tiene préstamos activos.");
        member.IsActive = false;
        members.Update(member);
    }

    public Member? GetById(Guid id) => members.GetById(id);

    public Member? GetByEmail(string email) =>
        members.Find(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

    public IReadOnlyList<Member> GetAll(bool onlyActive = true) =>
        members.Find(m => !onlyActive || m.IsActive).OrderBy(m => m.FullName).ToList();

    private void Validate(Member member)
    {
        if (string.IsNullOrWhiteSpace(member.FullName)) throw new LibraryException("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(member.Email) || !member.Email.Contains('@'))
            throw new LibraryException("El email no es válido.");
        var existing = GetByEmail(member.Email);
        if (existing is not null && existing.Id != member.Id)
            throw new LibraryException($"El email '{member.Email}' ya está registrado.");
    }
}
