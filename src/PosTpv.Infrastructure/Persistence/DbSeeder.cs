using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;

namespace PosTpv.Infrastructure.Persistence;

/// <summary>Applies pending migrations and seeds a demo catalogue on first run.</summary>
public class DbSeeder : IDbSeeder
{
    private readonly PosDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<DbSeeder> _log;

    public DbSeeder(PosDbContext db, IPasswordHasher hasher, ILogger<DbSeeder> log)
    {
        _db = db;
        _hasher = hasher;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Sqlite dev fallback: apply the live model directly instead of the SQL-Server-authored
        // migrations, since their column type strings (nvarchar(max), datetime2, ...) aren't Sqlite-native.
        if (_db.Database.IsSqlite())
            await _db.Database.EnsureCreatedAsync(ct);
        else
            await _db.Database.MigrateAsync(ct);

        if (!await _db.Users.AnyAsync(ct))
        {
            _log.LogInformation("Seeding demo users.");
            _db.Users.AddRange(
                NewUser("admin", "Administrator", UserRole.Admin),
                NewUser("waiter", "Alex Waiter", UserRole.Waiter),
                NewUser("kitchen", "Kitchen Station", UserRole.Kitchen),
                NewUser("cashier", "Casey Cashier", UserRole.Cashier));
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.AppSettings.AnyAsync(ct))
        {
            _log.LogInformation("Seeding default app settings.");
            _db.AppSettings.Add(new AppSetting());
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.Categories.AnyAsync(ct))
        {
            _log.LogInformation("Seeding demo catalogue.");
            var drinks = new Category { Name = "Drinks", Icon = "🥤", Color = "#0ea5e9", DisplayOrder = 1, Kind = CategoryKind.Drink };
            var starters = new Category { Name = "Starters", Icon = "🥗", Color = "#22c55e", DisplayOrder = 2, Course = CourseType.Starter };
            var salads = new Category { Name = "Salads", Icon = "🥙", Color = "#65a30d", DisplayOrder = 3, Course = CourseType.Starter };
            var pizzas = new Category { Name = "Pizzas", Icon = "🍕", Color = "#ef4444", DisplayOrder = 4, Course = CourseType.Main };
            var pasta = new Category { Name = "Pasta", Icon = "🍝", Color = "#f59e0b", DisplayOrder = 5, Course = CourseType.Main };
            var burgers = new Category { Name = "Burgers", Icon = "🍔", Color = "#a16207", DisplayOrder = 6, Course = CourseType.Main };
            var desserts = new Category { Name = "Desserts", Icon = "🍰", Color = "#ec4899", DisplayOrder = 7, Course = CourseType.Dessert };

            starters.Comments.Add(Cmt("No onion", 0));
            salads.Comments.Add(Cmt("No dressing", 0));
            salads.Comments.Add(Cmt("Dressing on the side", 1));
            pizzas.Comments.Add(Cmt("Well done", 0));
            pizzas.Comments.Add(Cmt("No cheese", 1));
            pizzas.Comments.Add(Cmt("Extra sauce", 2));
            pizzas.Comments.Add(Cmt("Gluten free", 3));
            pasta.Comments.Add(Cmt("Spicy", 0));
            pasta.Comments.Add(Cmt("No cheese", 1));
            pasta.Comments.Add(Cmt("Gluten free", 2));
            burgers.Comments.Add(Cmt("No onion", 0));
            burgers.Comments.Add(Cmt("Well done", 1));
            burgers.Comments.Add(Cmt("No pickles", 2));

            _db.Categories.AddRange(drinks, starters, salads, pizzas, pasta, burgers, desserts);

            _db.Products.AddRange(
                P("Water", 1.50m, drinks, "#38bdf8", 10),
                P("Sparkling Water", 1.80m, drinks, "#7dd3fc", 10),
                P("Soft Drink", 2.20m, drinks, "#0ea5e9", 10),
                P("Coca-Cola", 2.20m, drinks, "#dc2626", 10),
                P("Iced Tea", 2.30m, drinks, "#ca8a04", 10),
                P("House Wine", 3.50m, drinks, "#7c3aed", 10),
                P("Beer", 2.80m, drinks, "#f59e0b", 10),
                P("Espresso", 1.60m, drinks, "#78350f", 10),
                P("Cappuccino", 2.40m, drinks, "#92400e", 10),
                P("Bruschetta", 5.90m, starters, "#22c55e", 21, prep: 6),
                P("Garlic Bread", 4.50m, starters, "#16a34a", 21, prep: 5),
                P("Caesar Salad", 7.90m, starters, "#84cc16", 21, prep: 8),
                P("Mozzarella Sticks", 6.50m, starters, "#65a30d", 21, prep: 7),
                P("Onion Rings", 5.20m, starters, "#4d7c0f", 21, prep: 6),
                P("Hummus & Pita", 6.00m, starters, "#3f6212", 21, prep: 5),
                P("Greek Salad", 8.20m, salads, "#65a30d", 21, prep: 6),
                P("Caprese Salad", 8.50m, salads, "#84cc16", 21, prep: 5),
                P("Mixed Green Salad", 6.90m, salads, "#4d7c0f", 21, prep: 5),
                P("Margherita", 8.50m, pizzas, "#ef4444", 21, prep: 12),
                P("Pepperoni", 10.90m, pizzas, "#dc2626", 21, prep: 12),
                P("Four Cheese", 11.50m, pizzas, "#f97316", 21, prep: 13),
                P("Diavola", 11.90m, pizzas, "#b91c1c", 21, prep: 13),
                P("Vegetariana", 10.50m, pizzas, "#f87171", 21, prep: 12),
                P("Quattro Stagioni", 12.20m, pizzas, "#991b1b", 21, prep: 14),
                P("Hawaiana", 10.90m, pizzas, "#fb923c", 21, prep: 12),
                P("Carbonara", 9.90m, pasta, "#f59e0b", 21, prep: 11),
                P("Bolognese", 9.50m, pasta, "#d97706", 21, prep: 11),
                P("Lasagna", 10.90m, pasta, "#b45309", 21, prep: 15),
                P("Ravioli", 10.50m, pasta, "#ea580c", 21, prep: 12),
                P("Pesto Penne", 9.20m, pasta, "#ca8a04", 21, prep: 10),
                P("Classic Burger", 9.90m, burgers, "#a16207", 21, prep: 12),
                P("Cheeseburger", 10.50m, burgers, "#854d0e", 21, prep: 12),
                P("Bacon Burger", 11.90m, burgers, "#713f12", 21, prep: 13),
                P("Veggie Burger", 9.50m, burgers, "#ca8a04", 21, prep: 12),
                P("Tiramisu", 5.50m, desserts, "#ec4899", 10, prep: 3),
                P("Panna Cotta", 5.00m, desserts, "#db2777", 10, prep: 3),
                P("Cheesecake", 5.80m, desserts, "#be185d", 10, prep: 3),
                P("Chocolate Brownie", 5.20m, desserts, "#9d174d", 10, prep: 4),
                P("Ice Cream", 4.50m, desserts, "#f472b6", 10, prep: 2));

            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.Tables.AnyAsync(ct))
        {
            _log.LogInformation("Seeding demo floor plan.");
            for (var i = 1; i <= 8; i++)
            {
                var col = (i - 1) % 4;
                var row = (i - 1) / 4;
                _db.Tables.Add(new RestaurantTable
                {
                    Name = $"T{i}",
                    Seats = i % 3 == 0 ? 6 : 4,
                    Shape = i % 4 == 0 ? TableShape.Round : TableShape.Square,
                    PositionX = 40 + col * 170,
                    PositionY = 40 + row * 170,
                    Zone = i >= 7 ? "Bar" : "Main hall",
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.Extras.AnyAsync(ct))
        {
            _log.LogInformation("Seeding demo extras.");
            var extras = new[]
            {
                new Extra { Name = "Extra cheese", Price = 1.50m },
                new Extra { Name = "Bacon", Price = 1.80m },
                new Extra { Name = "Mushrooms", Price = 1.20m },
                new Extra { Name = "Extra sauce", Price = 0.80m },
                new Extra { Name = "Gluten-free base", Price = 2.00m },
            };
            _db.Extras.AddRange(extras);
            await _db.SaveChangesAsync(ct);

            // Offer the add-ons on pizzas, pasta and burgers.
            var eligible = await _db.Products
                .Include(p => p.Extras)
                .Where(p => p.Category.Name == "Pizzas" || p.Category.Name == "Pasta" || p.Category.Name == "Burgers")
                .ToListAsync(ct);
            foreach (var product in eligible)
                foreach (var extra in extras)
                    product.Extras.Add(extra);
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.Invoices.AnyAsync(ct))
        {
            _log.LogInformation("Seeding demo sales history.");
            var waiter = await _db.Users.FirstAsync(u => u.Role == UserRole.Waiter, ct);
            var tables = await _db.Tables.OrderBy(t => t.Id).ToListAsync(ct);
            var products = await _db.Products.OrderBy(p => p.Id).ToListAsync(ct);

            _db.SkipAuditStamp = true; // preserve the back-dated timestamps below
            var seq = 1;
            for (var day = 9; day >= 0; day--)
            {
                var ordersToday = 2 + (day % 3); // 2..4 paid bills per day
                for (var k = 0; k < ordersToday; k++)
                {
                    var when = DateTime.UtcNow.Date.AddDays(-day).AddHours(13 + k * 2);
                    var table = tables[(day + k) % tables.Count];

                    var order = new Order
                    {
                        Number = $"O-{seq:D5}",
                        TableId = table.Id,
                        WaiterId = waiter.Id,
                        Status = OrderStatus.Paid,
                        CreatedAt = when,
                        ClosedAt = when.AddMinutes(50),
                    };

                    var lineCount = 2 + ((day + k) % 3); // 2..4 lines
                    for (var j = 0; j < lineCount; j++)
                    {
                        var product = products[(day * 3 + k * 2 + j) % products.Count];
                        order.Items.Add(new OrderItem
                        {
                            ProductId = product.Id,
                            Quantity = 1 + (j % 2),
                            UnitPrice = product.Price,
                            VatRate = product.VatRate,
                            Status = OrderItemStatus.Delivered,
                            CreatedAt = when,
                        });
                    }

                    var invoice = new Invoice
                    {
                        Number = $"INV-{seq:D6}",
                        Subtotal = order.Subtotal,
                        VatTotal = order.VatTotal,
                        Total = order.Total,
                        CreatedAt = when,
                    };

                    if (seq % 5 == 0)
                    {
                        // Occasional split payment → recorded as "Other" on the invoice.
                        var half = Math.Round(order.Total / 2, 2);
                        invoice.PaymentMethod = PaymentMethod.Other;
                        invoice.Payments.Add(new Payment { Amount = half, Method = PaymentMethod.Cash, CreatedAt = when });
                        invoice.Payments.Add(new Payment { Amount = order.Total - half, Method = PaymentMethod.Card, CreatedAt = when });
                    }
                    else
                    {
                        var method = seq % 2 == 0 ? PaymentMethod.Card : PaymentMethod.Cash;
                        invoice.PaymentMethod = method;
                        invoice.Payments.Add(new Payment { Amount = order.Total, Method = method, CreatedAt = when });
                    }

                    order.Invoice = invoice;
                    _db.Orders.Add(order);
                    seq++;
                }
            }

            await _db.SaveChangesAsync(ct);
            _db.SkipAuditStamp = false;
        }
    }

    private User NewUser(string username, string fullName, UserRole role) => new()
    {
        Username = username,
        FullName = fullName,
        Role = role,
        PasswordHash = _hasher.Hash("1234") // demo PIN; change in production
    };

    private static CategoryComment Cmt(string text, int order) => new() { Text = text, DisplayOrder = order };

    private static Product P(string name, decimal price, Category cat, string color, decimal vat, int prep = 0) => new()
    {
        Name = name,
        Price = price,
        Category = cat,
        Color = color,
        VatRate = vat,
        PreparationMinutes = prep
    };
}
