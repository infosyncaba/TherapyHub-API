using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.Models;

namespace TherapuHubAPI.Configuration;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Users> Users { get; set; }
    public DbSet<UserTypes> UserTypes { get; set; }
    public DbSet<Menus> Menus { get; set; }
    public DbSet<UserTypeMenus> TipoUsuarioMenus { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configuración de UserTypes
        modelBuilder.Entity<UserTypes>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");
        });

        // Configuración de Users
        // Email, FullName, CompanyId are now on the Actors table (via ActorId FK)
        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.UserTypeId).IsRequired();
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Relación con UserTypes (sin navegación)
            entity.HasOne<UserTypes>()
                .WithMany()
                .HasForeignKey(u => u.UserTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de Menus
        modelBuilder.Entity<Menus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Route).IsUnique();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Route).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");
        });

        // Configuración de UserTypeMenus (tabla de relación)
        modelBuilder.Entity<UserTypeMenus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssignedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");
        });
    }
}
