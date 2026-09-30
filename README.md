# Servicios .NET para la gestión de una librería

Solución .NET 8 con el catálogo de servicios de negocio para gestionar una librería/biblioteca
(catálogo, inventario, socios, préstamos, reservas, multas, notificaciones e informes),
expuestos mediante una API REST (Minimal APIs).

## Estructura

```
LibraryManagement.sln
├── src/Library.Core          # Modelos, interfaces y lógica de negocio
│   ├── Models/               # Book, Author, Category, BookCopy, Member, Loan, Reservation, Fine, Notification
│   ├── Data/                 # IRepository<T> + InMemoryRepository<T>
│   └── Services/             # Interfaces e implementaciones de los servicios
├── src/Library.Api           # API REST que expone los servicios
└── tests/Library.Core.Tests  # Tests xUnit de las reglas de negocio
```

## Listado de servicios

| # | Servicio | Responsabilidad | Operaciones principales |
|---|----------|-----------------|-------------------------|
| 1 | `IBookService` | Catálogo de libros | `Create`, `Update`, `Delete`, `GetById`, `GetByIsbn`, `Search(título, autor, categoría)` |
| 2 | `IAuthorService` | Autores | `Create`, `Update`, `Delete`, `GetById`, `GetAll`, `GetBooks` |
| 3 | `ICategoryService` | Categorías / géneros | `Create`, `Update`, `Delete`, `GetById`, `GetAll` |
| 4 | `IInventoryService` | Ejemplares físicos y su estado | `AddCopy`, `RemoveCopy`, `SetStatus`, `GetCopies`, `CountAvailable`, `FindAvailableCopy` |
| 5 | `IMemberService` | Socios | `Register`, `Update`, `Deactivate`, `GetById`, `GetByEmail`, `GetAll` |
| 6 | `ILoanService` | Préstamos, devoluciones y renovaciones | `Lend`, `Return`, `Renew`, `GetActiveLoans`, `GetOverdueLoans`, `GetHistory` |
| 7 | `IReservationService` | Reservas con cola FIFO | `Reserve`, `Cancel`, `ProcessNext`, `GetReadyReservation`, `Fulfill`, `GetQueue`, `GetByMember` |
| 8 | `IFineService` | Multas por retraso | `Calculate`, `IssueForLoan`, `Pay`, `GetUnpaid`, `GetBalance` |
| 9 | `INotificationService` | Avisos a socios | `Send`, `GetByMember`, `SendDueDateReminders` |
| 10 | `IReportService` | Informes y estadísticas | `MostBorrowedBooks`, `MostActiveMembers`, `GetInventorySummary`, `TotalFinesCollected` |

Servicios de soporte: `IRepository<T>` (persistencia, en memoria por defecto) e `IClock` (fecha actual, para tests).

## Reglas de negocio

Configurables en `appsettings.json` (sección `Library`) o vía `LibraryOptions`:

- Préstamo de **14 días**, máximo **2 renovaciones**; no se renueva si está vencido o hay reservas pendientes.
- Cada socio tiene un límite de préstamos activos (`MaxActiveLoans`, por defecto 3).
- Multa de **0,50 por día** de retraso; con **10** o más en multas pendientes no se permiten nuevos préstamos.
- Solo se puede reservar un libro sin ejemplares disponibles. Al devolverse un ejemplar se aparta
  para el primer socio de la cola y se le notifica.
- No se pueden borrar autores/categorías con libros, libros con ejemplares, ni desactivar socios con préstamos activos.

## Uso

```bash
dotnet build
dotnet test
dotnet run --project src/Library.Api
```

Registro en cualquier aplicación .NET:

```csharp
builder.Services.AddLibraryServices(o => o.LoanDays = 21);
```

## Endpoints

| Método | Ruta | Servicio |
|--------|------|----------|
| GET/POST/PUT/DELETE | `/api/authors`, `/api/authors/{id}` | Autores |
| GET/POST/PUT/DELETE | `/api/categories`, `/api/categories/{id}` | Categorías |
| GET/POST/PUT/DELETE | `/api/books?title=&authorId=&categoryId=`, `/api/books/{id}`, `/api/books/isbn/{isbn}` | Libros |
| GET/POST | `/api/books/{id}/copies` | Inventario |
| PUT/DELETE | `/api/copies/{id}/status?status=Damaged`, `/api/copies/{id}` | Inventario |
| GET/POST/PUT | `/api/members`, `/api/members/{id}`, `POST /api/members/{id}/deactivate` | Socios |
| GET | `/api/members/{id}/loans` \| `/reservations` \| `/fines` \| `/notifications` | Historial del socio |
| POST | `/api/loans`, `/api/loans/{id}/return`, `/api/loans/{id}/renew` | Préstamos |
| GET | `/api/loans/overdue` | Préstamos vencidos |
| POST/DELETE/GET | `/api/reservations`, `/api/reservations/{id}`, `/api/reservations/book/{bookId}` | Reservas |
| POST | `/api/fines/{id}/pay` | Multas |
| POST | `/api/notifications/reminders?daysBefore=2` | Recordatorios |
| GET | `/api/reports/top-books`, `/top-members`, `/inventory`, `/fines?from=&to=` | Informes |

## Próximos pasos

- Sustituir `InMemoryRepository<T>` por una implementación con Entity Framework Core.
- Implementar `INotificationService` con envío real de email/SMS.
- Añadir autenticación y roles (bibliotecario / socio).
