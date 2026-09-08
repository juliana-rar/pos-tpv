using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// Covers the albarán-scan pipeline against a real SQL Server (Podman) test database, same
/// convention as <see cref="OrderFlowTests"/>. The vision-model call itself is stubbed out
/// (<see cref="FakeOllamaVisionClient"/>) so these don't depend on a running Ollama instance —
/// only the JSON-extraction/persistence/purchase-creation logic is under test here.
/// </summary>
public class AlbaranScanFlowTests
{
    private static readonly string TestConnection =
        Environment.GetEnvironmentVariable("POSTPV_TEST_CONNECTION")
        ?? "Server=127.0.0.1,14333;Database=PosTpv_Test;User Id=sa;Password=PosTpv!Dev2026;TrustServerCertificate=True;Encrypt=False";

    private static ServiceProvider BuildProvider(IOllamaVisionClient ollamaClient)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PosDbContext>(o => o.UseSqlServer(TestConnection));

        services.AddScoped<IUnitOfWork, PosTpv.Infrastructure.Persistence.Repositories.UnitOfWork>();
        services.AddSingleton<IPasswordHasher, PosTpv.Infrastructure.Identity.Pbkdf2PasswordHasher>();
        services.AddScoped<IDbSeeder, DbSeeder>();
        services.AddApplication();
        services.AddSingleton<IKitchenNotifier, NullKitchenNotifier>();
        services.AddSingleton<ISupplierNotifier, NullSupplierNotifier>();
        services.AddSingleton(ollamaClient);
        return services.BuildServiceProvider();
    }

    private const string CleanJson = """
        Aquí tienes el resultado:
        {"proveedor":"Acme Foods","numero_albaran":"A-123","fecha":"2026-01-15",
        "lineas":[{"descripcion":"Harina 25kg","cantidad":4,"precio_unitario":12.5},
        {"descripcion":"Tomate triturado","cantidad":10,"precio_unitario":1.2}],"total":62.0}
        Fin del análisis.
        """;

    private static string WriteTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF]);
        return path;
    }

    [Fact]
    public async Task ProcessAsync_extracts_lines_and_matches_supplier_by_name()
    {
        await using var provider = BuildProvider(new FakeOllamaVisionClient(CleanJson));

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var scans = sp.GetRequiredService<IAlbaranScanService>();
        var db = sp.GetRequiredService<PosDbContext>();

        db.Suppliers.Add(new Supplier { Name = "Acme Foods" });
        await db.SaveChangesAsync();

        var scanId = await scans.CreateAsync("/uploads/albaranes/test.jpg", null);
        await scans.ProcessAsync(scanId, WriteTempImage());

        var detail = await scans.GetByIdAsync(scanId);
        Assert.NotNull(detail);
        Assert.Equal(AlbaranScanStatus.NeedsReview, detail!.Status);
        Assert.Equal("Acme Foods", detail.SupplierName);
        Assert.Equal("A-123", detail.AlbaranNumber);
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal(62.0m, detail.TotalAmount);
    }

    [Fact]
    public async Task ProcessAsync_marks_Failed_when_Ollama_is_unavailable()
    {
        await using var provider = BuildProvider(new FakeOllamaVisionClient(new OllamaUnavailableException("no está corriendo")));

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var scans = scope.ServiceProvider.GetRequiredService<IAlbaranScanService>();

        var scanId = await scans.CreateAsync("/uploads/albaranes/test.jpg", null);
        await scans.ProcessAsync(scanId, WriteTempImage());

        var detail = await scans.GetByIdAsync(scanId);
        Assert.Equal(AlbaranScanStatus.Failed, detail!.Status);
        Assert.Equal("no está corriendo", detail.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAndCreatePurchaseAsync_creates_purchase_and_restocks_products()
    {
        await using var provider = BuildProvider(new FakeOllamaVisionClient(CleanJson));

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var scans = sp.GetRequiredService<IAlbaranScanService>();
        var db = sp.GetRequiredService<PosDbContext>();

        var supplier = new Supplier { Name = "Acme Foods" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        var product = await db.Products.FirstAsync(p => p.Name == "Margherita");
        var stockBefore = product.StockQuantity;

        var scanId = await scans.CreateAsync("/uploads/albaranes/test.jpg", null);
        await scans.ProcessAsync(scanId, WriteTempImage());
        var detail = await scans.GetByIdAsync(scanId);

        var editDto = new AlbaranScanEditDto
        {
            SupplierId = supplier.Id,
            AlbaranNumber = detail!.AlbaranNumber,
            AlbaranDate = detail.AlbaranDate,
            Lines = detail.Lines.Select(l => new AlbaranScanLineEditDto
            {
                Id = l.Id,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                ProductId = product.Id
            }).ToList()
        };

        var purchaseId = await scans.ValidateAndCreatePurchaseAsync(scanId, editDto);
        Assert.True(purchaseId > 0);

        var updatedDetail = await scans.GetByIdAsync(scanId);
        Assert.Equal(AlbaranScanStatus.Validated, updatedDetail!.Status);

        var reloadedProduct = await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id);
        Assert.Equal(stockBefore + editDto.Lines.Sum(l => l.Quantity), reloadedProduct.StockQuantity);

        var purchaseExists = await db.Purchases.AnyAsync(p => p.Id == purchaseId && p.SupplierId == supplier.Id);
        Assert.True(purchaseExists);
    }

    [Fact]
    public async Task ValidateAndCreatePurchaseAsync_throws_when_a_line_has_no_product()
    {
        await using var provider = BuildProvider(new FakeOllamaVisionClient(CleanJson));

        using (var reset = provider.CreateScope())
            await reset.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        using (var seedScope = provider.CreateScope())
            await seedScope.ServiceProvider.GetRequiredService<IDbSeeder>().SeedAsync();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var scans = sp.GetRequiredService<IAlbaranScanService>();
        var db = sp.GetRequiredService<PosDbContext>();

        var supplier = new Supplier { Name = "Acme Foods" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var scanId = await scans.CreateAsync("/uploads/albaranes/test.jpg", null);
        await scans.ProcessAsync(scanId, WriteTempImage());
        var detail = await scans.GetByIdAsync(scanId);

        var editDto = new AlbaranScanEditDto
        {
            SupplierId = supplier.Id,
            Lines = detail!.Lines.Select(l => new AlbaranScanLineEditDto
            {
                Id = l.Id,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                ProductId = 0
            }).ToList()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => scans.ValidateAndCreatePurchaseAsync(scanId, editDto));
    }

    private sealed class FakeOllamaVisionClient : IOllamaVisionClient
    {
        private readonly string? _response;
        private readonly Exception? _toThrow;

        public FakeOllamaVisionClient(string response) => _response = response;
        public FakeOllamaVisionClient(Exception toThrow) => _toThrow = toThrow;

        public Task<string> ExtractAlbaranJsonAsync(byte[] imageBytes, CancellationToken ct = default) =>
            _toThrow is not null ? throw _toThrow : Task.FromResult(_response!);
    }

    private sealed class NullSupplierNotifier : ISupplierNotifier
    {
        public Task AlbaranScanUpdatedAsync(int scanId, string status) => Task.CompletedTask;
    }

    private sealed class NullKitchenNotifier : IKitchenNotifier
    {
        public Task OrderSentToKitchenAsync(int orderId) => Task.CompletedTask;
        public Task OrderItemStatusChangedAsync(int orderId, int orderItemId) => Task.CompletedTask;
        public Task OrderReadyAsync(int orderId) => Task.CompletedTask;
        public Task DrinksServedAsync(int orderId) => Task.CompletedTask;
        public Task FirstCoursesServedAsync(int orderId) => Task.CompletedTask;
        public Task SecondCoursesFiredAsync(int orderId) => Task.CompletedTask;
        public Task SecondCoursesServedAsync(int orderId) => Task.CompletedTask;
        public Task DessertCoursesServedAsync(int orderId) => Task.CompletedTask;
    }
}
