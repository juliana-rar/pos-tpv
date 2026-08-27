using Microsoft.EntityFrameworkCore;
using PosTpv.Domain.Common;
using PosTpv.Domain.Entities;

namespace PosTpv.Infrastructure.Persistence;

/// <summary>EF Core context for the whole POS. Configuration lives in OnModelCreating.</summary>
public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    /// <summary>When true, SaveChanges does not overwrite CreatedAt (used to seed back-dated demo data).</summary>
    public bool SkipAuditStamp { get; set; }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CategoryComment> CategoryComments => Set<CategoryComment>();
    public DbSet<Extra> Extras => Set<Extra>();
    public DbSet<Allergen> Allergens => Set<Allergen>();
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<FloorDecor> FloorDecors => Set<FloorDecor>();
    public DbSet<FloorZone> FloorZones => Set<FloorZone>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemExtra> OrderItemExtras => Set<OrderItemExtra>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9);
            e.Property(x => x.Icon).HasMaxLength(40);
            e.HasIndex(x => x.DisplayOrder);
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.HasOne(x => x.Category).WithMany(c => c.Products)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Extras).WithMany(x => x.Products);
            e.HasMany(x => x.Allergens).WithMany(x => x.Products);
            e.HasIndex(x => x.CategoryId);
        });

        b.Entity<Extra>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Price).HasPrecision(10, 2);
        });

        b.Entity<Allergen>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
        });

        b.Entity<CategoryComment>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(80).IsRequired();
            e.HasOne(x => x.Category).WithMany(c => c.Comments)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CategoryId);
        });

        b.Entity<RestaurantTable>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(40).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.GroupId);
        });

        b.Entity<FloorZone>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9);
        });

        b.Entity<Reservation>(e =>
        {
            e.Property(x => x.CustomerName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Color).HasMaxLength(9);
            e.HasMany(x => x.Tables).WithMany(t => t.Reservations)
                .UsingEntity(j => j.ToTable("ReservationTables"));
            e.HasOne(x => x.Customer).WithMany(c => c.Reservations)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Date);
        });

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(160);
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne(x => x.Table).WithMany(t => t.Orders)
                .HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Waiter).WithMany(u => u.Orders)
                .HasForeignKey(x => x.WaiterId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Subtotal);
            e.Ignore(x => x.VatTotal);
            e.Ignore(x => x.Total);
            // Both are filtered on every open-orders/kitchen/dashboard load.
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(10, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 2);
            e.Property(x => x.Comment).HasMaxLength(250);
            e.HasOne(x => x.Order).WithMany(o => o.Items)
                .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.ExtrasUnit);
            e.Ignore(x => x.LineGross);
            e.Ignore(x => x.LineNet);
            e.Ignore(x => x.LineVat);
        });

        b.Entity<OrderItemExtra>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasOne(x => x.OrderItem).WithMany(i => i.Extras)
                .HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Subtotal).HasPrecision(10, 2);
            e.Property(x => x.VatTotal).HasPrecision(10, 2);
            e.Property(x => x.Total).HasPrecision(10, 2);
            e.HasOne(x => x.Order).WithOne(o => o.Invoice)
                .HasForeignKey<Invoice>(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            // Filtered by date range in the billing report and by the dashboard's daily/monthly totals.
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AppSetting>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(80).IsRequired();
            e.Property(x => x.FloorTexture).HasMaxLength(20).IsRequired();
            e.Property(x => x.ReservationPolicy).HasMaxLength(20).IsRequired();
            e.Property(x => x.ReceiptLegalName).HasMaxLength(120);
            e.Property(x => x.ReceiptTaxId).HasMaxLength(40);
            e.Property(x => x.ReceiptAddress).HasMaxLength(250);
            e.Property(x => x.ReceiptFooter).HasMaxLength(300);
            e.Property(x => x.ReceiptPaperWidth).HasMaxLength(4).IsRequired();
        });

        b.Entity<Product>(e => e.Property(x => x.StockQuantity).HasPrecision(10, 2));

        b.Entity<Supplier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ContactName).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(160);
            e.Property(x => x.TaxId).HasMaxLength(40);
            e.Property(x => x.Address).HasMaxLength(250);
            e.Property(x => x.Notes).HasMaxLength(500);
        });

        b.Entity<SupplierDocument>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(200).IsRequired();
            e.Property(x => x.FileUrl).HasMaxLength(300).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasOne(x => x.Supplier).WithMany(s => s.Documents)
                .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SupplierId);
        });

        b.Entity<Purchase>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(60);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Supplier).WithMany(s => s.Purchases)
                .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.Total);
            e.HasIndex(x => x.Date);
        });

        b.Entity<PurchaseLine>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(10, 2);
            e.Property(x => x.UnitCost).HasPrecision(10, 2);
            e.HasOne(x => x.Purchase).WithMany(p => p.Lines)
                .HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.LineTotal);
        });

        b.Entity<StockMovement>(e =>
        {
            e.Property(x => x.QuantityChange).HasPrecision(10, 2);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Product).WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ProductId);
        });
    }

    /// <summary>Stamps audit timestamps on save.</summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (!SkipAuditStamp)
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
                if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
