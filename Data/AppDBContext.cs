using Cuzdan360Backend.Models;
using Microsoft.EntityFrameworkCore;
using Cuzdan360Backend.Models.Finance; // 👈 EKLENDİ

namespace Cuzdan360Backend.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    // Finance tables
    public DbSet<AssetType> AssetTypes { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Source> Sources { get; set; }
    public DbSet<Transaction> Transactions { get; set; }


    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Kullanıcı tablosu için varsayılan değerler
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1, Username = "admin",
                PasswordHash = "$2a$12$vDN5rfJgTGOrCvJ0354EueBJhTkQOt3cWqCnInML7TKC9qbDv/cYK",
                Email = "admin@example.com"
            }
        );

        // === 4. YENİ EKLENEN HAZIR VERİLER ===

        // Varlık Tipleri (AssetType) için hazır veriler
        modelBuilder.Entity<AssetType>().HasData(
            new AssetType { AssetTypeId = 1, Name = "Türk Lirası", Code = "TRY" },
            new AssetType { AssetTypeId = 2, Name = "ABD Doları", Code = "USD" },
            new AssetType { AssetTypeId = 3, Name = "Euro", Code = "EUR" },
            new AssetType { AssetTypeId = 4, Name = "Gram Altın", Code = "XAUTRY" },
            new AssetType { AssetTypeId = 5, Name = "Bitcoin", Code = "BTC" }
        );

        // Kaynaklar (Source) için hazır veriler
        modelBuilder.Entity<Source>().HasData(
            new Source { SourceId = 1, SourceName = "Nakit" },
            new Source { SourceId = 2, SourceName = "Banka Hesabı" },
            new Source { SourceId = 3, SourceName = "Kredi Kartı" },
            new Source { SourceId = 4, SourceName = "Yatırım Hesabı" }
        );

        // Kategoriler (Category) için hazır veriler
        modelBuilder.Entity<Category>().HasData(
            // --- Gelir Kategorileri ---
            new Category { CategoryId = 1, Name = "Maaş" },
            new Category { CategoryId = 2, Name = "Ek Gelir (Freelance)" },
            new Category { CategoryId = 3, Name = "Kira Geliri" },
            new Category { CategoryId = 4, Name = "Satış Geliri" },
            new Category { CategoryId = 5, Name = "Diğer Gelirler" },
            new Category { CategoryId = 6, Name = "Hediye / Burs" },
            
            // --- Gider Kategorileri ---
            new Category { CategoryId = 10, Name = "Market & Gıda" },
            new Category { CategoryId = 11, Name = "Faturalar (Elektrik, Su, İnternet)" },
            new Category { CategoryId = 12, Name = "Ulaşım (Yakıt, Toplu Taşıma)" },
            new Category { CategoryId = 13, Name = "Kira / Konut Kredisi" },
            new Category { CategoryId = 14, Name = "Restoran & Dışarıda Yemek" },
            new Category { CategoryId = 15, Name = "Giyim & Alışveriş" },
            new Category { CategoryId = 16, Name = "Eğlence & Sosyal (Sinema, Konser)" },
            new Category { CategoryId = 17, Name = "Sağlık & Kişisel Bakım" },
            new Category { CategoryId = 18, Name = "Eğitim" },
            new Category { CategoryId = 19, Name = "Ev Eşyası & Dekorasyon" },
            new Category { CategoryId = 20, Name = "Tatil" },
            new Category { CategoryId = 21, Name = "Borç / Kredi Ödemesi" },
            new Category { CategoryId = 22, Name = "Diğer Giderler" }
        );
        // === HAZIR VERİ SONU ===


        // === Transaction ilişkileri ===
        // (Bu kısım sizinkiyle aynı, değişmedi)

        // Transaction → User (1 kullanıcı, birçok işlem)
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.User)
            .WithMany(u => u.Transactions) // 👈 User.cs'i güncellediğimiz için burayı da güncelledim
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Transaction → AssetType (1 varlık tipi, birçok işlem)
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.AssetType)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AssetTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Transaction → Category (1 kategori, birçok işlem)
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Transaction → Source (1 kaynak, birçok işlem)
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Source)
            .WithMany(s => s.Transactions)
            .HasForeignKey(t => t.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}