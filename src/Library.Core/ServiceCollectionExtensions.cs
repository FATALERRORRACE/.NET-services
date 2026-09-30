using Library.Core.Data;
using Library.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra todos los servicios de la librería en el contenedor de DI.</summary>
    public static IServiceCollection AddLibraryServices(
        this IServiceCollection services, Action<LibraryOptions>? configure = null)
    {
        var options = new LibraryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));

        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IAuthorService, AuthorService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IFineService, FineService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
