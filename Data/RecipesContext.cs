using Microsoft.EntityFrameworkCore;
using Recipes.Models;

namespace Recipes.Data;

public class RecipesContext(DbContextOptions<RecipesContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockEntry> Stock => Set<StockEntry>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuRecipe> MenuRecipes => Set<MenuRecipe>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<SaleVoucher> SaleVouchers => Set<SaleVoucher>();

    // SQL Server was case-insensitive; keep that for equality and uniqueness checks
    protected override void ConfigureConventions(ModelConfigurationBuilder configuration) =>
        configuration.Properties<string>().UseCollation("NOCASE");

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.UnitOfMeasure).HasMaxLength(10);
            e.Property(x => x.Category).HasMaxLength(20);
            e.Property(x => x.Code).HasMaxLength(10);
        });

        b.Entity<StockEntry>(e =>
        {
            e.ToTable("Stock");
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Recipe>(e =>
        {
            e.ToTable("Recipes");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.Quantity).HasMaxLength(50);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<RecipeIngredient>(e =>
        {
            e.ToTable("RecipeIngredients");
            e.HasOne(x => x.Recipe).WithMany(x => x.Products).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Menu>(e =>
        {
            e.ToTable("Menus");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Code).HasMaxLength(10);
        });

        b.Entity<MenuRecipe>(e =>
        {
            e.ToTable("MenuRecipes");
            e.HasOne(x => x.Menu).WithMany(x => x.Recipes).HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Recipe).WithMany(x => x.MenuRecipes).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<GoodsReceipt>(e =>
        {
            e.ToTable("GoodsReceipts");
            e.Property(x => x.SupplierName).HasMaxLength(100);
            e.Property(x => x.SupplierTaxId).HasMaxLength(50);
            e.Property(x => x.SupplierTradeRegisterNo).HasMaxLength(50);
            e.Property(x => x.SupplierAddress).HasMaxLength(250);
            e.Property(x => x.SupplierCounty).HasMaxLength(50);
            e.Property(x => x.SupplierAccount).HasMaxLength(50);
            e.Property(x => x.SupplierBank).HasMaxLength(50);
            e.Property(x => x.DeliveryDocumentType).HasMaxLength(20);
            e.Property(x => x.DeliveryDocumentNumber).HasMaxLength(30);
        });

        b.Entity<GoodsReceiptLine>(e =>
        {
            e.ToTable("GoodsReceiptLines");
            e.HasOne(x => x.GoodsReceipt).WithMany(x => x.Products).HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Sale>(e =>
        {
            e.ToTable("Sales");
            e.Property(x => x.CustomerName).HasMaxLength(100);
            e.Property(x => x.CustomerTaxId).HasMaxLength(50);
            e.Property(x => x.CustomerTradeRegisterNo).HasMaxLength(50);
            e.Property(x => x.CustomerAddress).HasMaxLength(250);
            e.Property(x => x.CustomerCounty).HasMaxLength(50);
            e.Property(x => x.CustomerAccount).HasMaxLength(50);
            e.Property(x => x.CustomerBank).HasMaxLength(50);
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.InvoiceSeries).HasMaxLength(10);
        });

        b.Entity<SaleLine>(e =>
        {
            e.ToTable("SaleLines");
            e.HasOne(x => x.Sale).WithMany(x => x.SaleLines).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Menu).WithMany(x => x.SaleLines).HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Recipe).WithMany(x => x.SaleLines).HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SaleVoucher>(e =>
        {
            e.ToTable("SaleVouchers");
            e.HasOne(x => x.Sale).WithMany(x => x.Vouchers).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
