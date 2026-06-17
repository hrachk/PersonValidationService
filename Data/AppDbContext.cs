using Microsoft.EntityFrameworkCore;
using PersonValidationService.Models;

namespace PersonValidationService.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Passport> Passports => Set<Passport>();

    public DbSet<Person> Persons => Set<Person>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Passport>(entity =>
        {
            entity.ToTable("Passports");

            entity.HasKey(x => x.PassportId);

            entity.Property(x => x.PassportId)
                .HasColumnName("PassportID");

            entity.Property(x => x.PersonId)
                .HasColumnName("PersonID");

            entity.Property(x => x.PassportNum)
                .HasColumnName("PassportNum");
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("Persons");

            entity.HasKey(x => x.PersonId);

            entity.Property(x => x.PersonId)
                .HasColumnName("PersonID");

            entity.Property(x => x.SocialCard)
                .HasColumnName("SocialCard");
        });
    }
}