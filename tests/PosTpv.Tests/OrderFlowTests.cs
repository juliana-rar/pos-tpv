using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosTpv.Application;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Application.Services;
using PosTpv.Domain.Entities;
using PosTpv.Domain.Enums;
using PosTpv.Infrastructure;
using PosTpv.Infrastructure.Persistence;
using Xunit;

namespace PosTpv.Tests;

/// <summary>
/// End-to-end check of the core POS lifecycle against a real SQL Server (Podman) test database:
/// open a table → add lines → send to kitchen → mark ready → checkout.
/// </summary>
public class OrderFlowTests
{
    // Falls back to the well-known local dev password (see .env.example) so `dotnet test` keeps
    // working out of the box; override with POSTPV_TEST_CONNECTION for CI/other environments.
    private static readonly string TestConnection =
        Environment.GetEnvironmentVariable("POSTPV_TEST_CONNECTION")
        ?? "Server=127.0.0.1,14333;Database=PosTpv_Test;User Id=sa;Password=PosTpv!Dev2026;TrustServerCertificate=True;Encrypt=False";

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PosDbContext>(o => o.UseSqlServer(TestConnection));

        // Register infrastructure pieces manually (real DbContext above, no IConfiguration needed).
        services.AddScoped<IUnitOfWork, PosTpv.Infrastructure.Persistence.Repositories.UnitOfWork>();
        services.AddSingleton<IPasswordHasher, PosTpv.Infrastructure.Identity.Pbkdf2PasswordHasher>();
        services.AddScoped<IDbSeeder, DbSeeder>();
        services.AddApplication();
        services.AddSingleton<IKitchenNotifier, NullNotifier>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Full_order_lifecycle_produces_invoice_and_frees_table()
    {
        await using var provider = BuildProvider();

        // Fresh database each run for a deterministic assertion.
        using (var reset = provider.CreateScope())
        {
            var db = reset.ServiceProvider.GetRequiredService<PosDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        using (var seedScope = provider.CreateScope())
        {
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();
        }

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var orders = sp.GetRequiredService<IOrderService>();
        var products = sp.GetRequiredService<IProductService>();
        var tables = sp.GetRequiredService<ITableService>();
        var db2 = sp.GetRequiredService<PosDbContext>();

        var waiter = await db2.Users.FirstAsync(u => u.Role == UserRole.Waiter);
        var table = await db2.Tables.FirstAsync();
        var pizza = await db2.Products.FirstAsync(p => p.Name == "Margherita");
        var drink = await db2.Products.FirstAsync(p => p.Name == "Beer");

        // Open a table.
        var order = await orders.OpenOrderAsync(table.Id, waiter.Id);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.StartsWith("O-", order.Number);

        // Add lines (two pizzas, one beer) and verify totals.
        order = await orders.AddItemAsync(new(order.Id, pizza.Id, 2));
        order = await orders.AddItemAsync(new(order.Id, drink.Id));
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(pizza.Price * 2 + drink.Price, order.Total);

        // Table shows as occupied with the running total.
        var occupied = (await tables.GetAllAsync()).First(t => t.Id == table.Id);
        Assert.Equal(TableStatus.Occupied, occupied.Status);
        Assert.Equal(order.Total, occupied.ActiveOrderTotal);

        // Send to kitchen → lines move to Preparing.
        await orders.SendToKitchenAsync(order.Id);
        var kitchen = await orders.GetKitchenOrdersAsync();
        Assert.Contains(kitchen, o => o.Id == order.Id);

        // Kitchen marks every line ready.
        var reloaded = await orders.GetByIdAsync(order.Id);
        foreach (var line in reloaded!.Items)
            await orders.SetItemStatusAsync(line.Id, OrderItemStatus.Ready);

        reloaded = await orders.GetByIdAsync(order.Id);
        Assert.Equal(OrderStatus.Ready, reloaded!.Status);

        // Checkout → invoice created, order paid, table freed.
        var invoiceId = await orders.CheckoutAsync(order.Id, PaymentMethod.Card);
        Assert.True(invoiceId > 0);

        var paid = await db2.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Paid, paid.Status);

        var invoice = await db2.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(reloaded.Total, invoice.Total);

        var freedTable = await db2.Tables.AsNoTracking().FirstAsync(t => t.Id == table.Id);
        Assert.Equal(TableStatus.Available, freedTable.Status);
    }

    [Fact]
    public async Task Save_layout_persists_table_geometry()
    {
        await using var provider = BuildProvider();

        using (var reset = provider.CreateScope())
        {
            var db = reset.ServiceProvider.GetRequiredService<PosDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
        using (var seedScope = provider.CreateScope())
        {
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();
        }

        using var scope = provider.CreateScope();
        var tables = scope.ServiceProvider.GetRequiredService<ITableService>();
        var db2 = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var table = await db2.Tables.AsNoTracking().FirstAsync();

        // Simulates the payload produced by the JS floor-plan editor's getLayout().
        var patch = new PosTpv.Application.DTOs.TableLayoutDto(
            table.Id, PositionX: 123, PositionY: 456, Width: 140, Height: 100, Rotation: 45, IsLocked: true);

        await tables.SaveLayoutAsync(new[] { patch });

        var saved = await db2.Tables.AsNoTracking().FirstAsync(t => t.Id == table.Id);
        Assert.Equal(123, saved.PositionX);
        Assert.Equal(456, saved.PositionY);
        Assert.Equal(140, saved.Width);
        Assert.Equal(100, saved.Height);
        Assert.Equal(45, saved.Rotation);
        Assert.True(saved.IsLocked);
    }

    [Fact]
    public async Task Joined_tables_share_one_order_and_free_together()
    {
        await using var provider = BuildProvider();

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var tables = sp.GetRequiredService<ITableService>();
        var orders = sp.GetRequiredService<IOrderService>();
        var db = sp.GetRequiredService<PosDbContext>();

        var t1 = await db.Tables.OrderBy(t => t.Id).FirstAsync();
        var t2 = await db.Tables.OrderBy(t => t.Id).Skip(1).FirstAsync();
        var waiter = await db.Users.FirstAsync(u => u.Role == UserRole.Waiter);
        var pizza = await db.Products.FirstAsync(p => p.Name == "Margherita");

        // Join the two tables.
        await tables.JoinTablesAsync(new[] { t1.Id, t2.Id });
        var joined = await tables.GetAllAsync();
        var group = joined.First(x => x.Id == t1.Id).GroupId;
        Assert.NotNull(group);
        Assert.Equal(group, joined.First(x => x.Id == t2.Id).GroupId);

        // Opening an order on the second table uses the primary (lowest id) and occupies both.
        var order = await orders.OpenOrderAsync(t2.Id, waiter.Id);
        Assert.Equal(t1.Id, order.TableId);

        var occupied = await tables.GetAllAsync();
        Assert.Equal(TableStatus.Occupied, occupied.First(x => x.Id == t1.Id).Status);
        Assert.Equal(TableStatus.Occupied, occupied.First(x => x.Id == t2.Id).Status);
        Assert.Equal(order.Id, occupied.First(x => x.Id == t2.Id).ActiveOrderId); // surfaced on member too

        // Opening on the other member returns the same shared order.
        var same = await orders.OpenOrderAsync(t1.Id, waiter.Id);
        Assert.Equal(order.Id, same.Id);

        // Cannot separate while the group has an open order.
        await Assert.ThrowsAsync<InvalidOperationException>(() => tables.SeparateGroupAsync(t1.Id));

        // Charge → both tables free together.
        await orders.AddItemAsync(new(order.Id, pizza.Id));
        await orders.CheckoutAsync(order.Id, PaymentMethod.Cash);
        var freed = await tables.GetAllAsync();
        Assert.Equal(TableStatus.Available, freed.First(x => x.Id == t1.Id).Status);
        Assert.Equal(TableStatus.Available, freed.First(x => x.Id == t2.Id).Status);

        // Now the group can be separated.
        await tables.SeparateGroupAsync(t1.Id);
        var separated = await tables.GetAllAsync();
        Assert.Null(separated.First(x => x.Id == t1.Id).GroupId);
        Assert.Null(separated.First(x => x.Id == t2.Id).GroupId);
    }

    [Fact]
    public async Task Lines_with_extras_capture_price_and_stay_separate()
    {
        await using var provider = BuildProvider();

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var orders = sp.GetRequiredService<IOrderService>();
        var products = sp.GetRequiredService<IProductService>();
        var db = sp.GetRequiredService<PosDbContext>();

        var waiter = await db.Users.FirstAsync(u => u.Role == UserRole.Waiter);
        var table = await db.Tables.FirstAsync();
        var pizza = await db.Products.FirstAsync(p => p.Name == "Margherita");

        var extras = await products.GetExtrasAsync(pizza.Id);
        Assert.NotEmpty(extras); // pizzas were seeded with add-ons
        var cheese = extras.First(e => e.Name == "Extra cheese");
        var bacon = extras.First(e => e.Name == "Bacon");

        var order = await orders.OpenOrderAsync(table.Id, waiter.Id);

        // A line with two extras captures their prices in the line total.
        order = await orders.AddItemAsync(new(order.Id, pizza.Id, 1, null, new[] { cheese.Id, bacon.Id }));
        var withExtras = order.Items.Single();
        Assert.Equal(2, withExtras.Extras.Count);
        Assert.Equal(pizza.Price + cheese.Price + bacon.Price, withExtras.LineTotal);

        // A plain add of the same product does NOT merge into the extras line.
        order = await orders.AddItemAsync(new(order.Id, pizza.Id));
        Assert.Equal(2, order.Items.Count);

        // A second plain add merges with the plain line (qty 2), still two lines total.
        order = await orders.AddItemAsync(new(order.Id, pizza.Id));
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(2, order.Items.First(i => i.Extras.Count == 0).Quantity);
    }

    [Fact]
    public async Task Split_payment_records_multiple_tenders_and_closes_the_bill()
    {
        await using var provider = BuildProvider();

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var orders = sp.GetRequiredService<IOrderService>();
        var db = sp.GetRequiredService<PosDbContext>();

        var waiter = await db.Users.FirstAsync(u => u.Role == UserRole.Waiter);
        var table = await db.Tables.FirstAsync();
        var pizza = await db.Products.FirstAsync(p => p.Name == "Margherita");

        var order = await orders.OpenOrderAsync(table.Id, waiter.Id);
        order = await orders.AddItemAsync(new(order.Id, pizza.Id));
        order = await orders.AddItemAsync(new(order.Id, pizza.Id));
        var total = order.Total; // 2 × Margherita

        // Under-payment is rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orders.CheckoutAsync(order.Id, new[] { new PaymentInput(5m, PaymentMethod.Cash) }));

        // Split: half cash, half card.
        var half = Math.Round(total / 2, 2);
        var invoiceId = await orders.CheckoutAsync(order.Id, new[]
        {
            new PaymentInput(half, PaymentMethod.Cash),
            new PaymentInput(total - half, PaymentMethod.Card),
        });

        var invoice = await db.Invoices.Include(i => i.Payments).AsNoTracking().FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(2, invoice.Payments.Count);
        Assert.Equal(total, invoice.Payments.Sum(p => p.Amount));
        Assert.Equal(total, invoice.Total);
        Assert.Equal(PaymentMethod.Other, invoice.PaymentMethod); // mixed methods → Other

        var paid = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Paid, paid.Status);
    }

    [Fact]
    public async Task Serve_drinks_delivers_whole_block_and_lines_carry_their_course()
    {
        await using var provider = BuildProvider();

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var orders = sp.GetRequiredService<IOrderService>();
        var db = sp.GetRequiredService<PosDbContext>();

        var waiter = await db.Users.FirstAsync(u => u.Role == UserRole.Waiter);
        var table = await db.Tables.FirstAsync();
        var water = await db.Products.FirstAsync(p => p.Name == "Water");        // Drinks
        var beer = await db.Products.FirstAsync(p => p.Name == "Beer");          // Drinks
        var salad = await db.Products.FirstAsync(p => p.Name == "Caesar Salad"); // Starters  -> First course
        var pizza = await db.Products.FirstAsync(p => p.Name == "Margherita");   // Pizzas    -> Second course
        var tiramisu = await db.Products.FirstAsync(p => p.Name == "Tiramisu");  // Desserts

        var order = await orders.OpenOrderAsync(table.Id, waiter.Id);
        await orders.AddItemAsync(new(order.Id, water.Id, 2));
        await orders.AddItemAsync(new(order.Id, beer.Id));
        await orders.AddItemAsync(new(order.Id, salad.Id));
        await orders.AddItemAsync(new(order.Id, pizza.Id));
        await orders.AddItemAsync(new(order.Id, tiramisu.Id));

        // Each line carries the drink flag / course that drives its block on /orders.
        var open = (await orders.GetOpenOrdersAsync()).First(o => o.Id == order.Id);
        Assert.True(open.Items.Single(i => i.ProductName == "Water").IsDrink);
        Assert.Equal(CourseType.Starter, open.Items.Single(i => i.ProductName == "Caesar Salad").Course);
        Assert.Equal(CourseType.Main, open.Items.Single(i => i.ProductName == "Margherita").Course);
        Assert.Equal(CourseType.Dessert, open.Items.Single(i => i.ProductName == "Tiramisu").Course);

        // One call serves every drink; the food block is left exactly as it was.
        await orders.ServeDrinksAsync(order.Id);
        var served = (await orders.GetOpenOrdersAsync()).First(o => o.Id == order.Id);
        Assert.All(served.Items.Where(i => i.IsDrink), i => Assert.Equal(OrderItemStatus.Delivered, i.Status));
        Assert.All(served.Items.Where(i => !i.IsDrink), i => Assert.NotEqual(OrderItemStatus.Delivered, i.Status));

        // Firing firsts / seconds flags the order (independently); acknowledging clears just that one.
        await orders.FireFirstCoursesAsync(order.Id);
        await orders.FireSecondCoursesAsync(order.Id);
        var fired = (await orders.GetOpenOrdersAsync()).First(o => o.Id == order.Id);
        Assert.NotNull(fired.FirstsFiredAt);
        Assert.NotNull(fired.SecondsFiredAt);

        await orders.AcknowledgeFirstCoursesAsync(order.Id);
        var acked = (await orders.GetOpenOrdersAsync()).First(o => o.Id == order.Id);
        Assert.Null(acked.FirstsFiredAt);
        Assert.NotNull(acked.SecondsFiredAt);
    }

    private sealed class NullNotifier : IKitchenNotifier
    {
        public Task OrderSentToKitchenAsync(int orderId) => Task.CompletedTask;
        public Task OrderItemStatusChangedAsync(int orderId, int orderItemId) => Task.CompletedTask;
        public Task OrderReadyAsync(int orderId) => Task.CompletedTask;
        public Task FirstCoursesFiredAsync(int orderId) => Task.CompletedTask;
        public Task SecondCoursesFiredAsync(int orderId) => Task.CompletedTask;
        public Task DessertCoursesFiredAsync(int orderId) => Task.CompletedTask;
        public Task SecondCoursesServedAsync(int orderId) => Task.CompletedTask;
        public Task DessertCoursesServedAsync(int orderId) => Task.CompletedTask;
        public Task DrinksServedAsync(int orderId) => Task.CompletedTask;
        public Task FirstCoursesServedAsync(int orderId) => Task.CompletedTask;
    }
}
