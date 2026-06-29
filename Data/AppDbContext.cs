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

    public DbSet<DicFirstName> DicFirstNames => Set<DicFirstName>();

    public DbSet<DicLastName> DicLastNames => Set<DicLastName>();

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

            // FirstName/LastName columns on Persons are int FKs into the
            // dictionary tables below, NOT the name text itself.
            entity.Property(x => x.FirstNameId)
                .HasColumnName("FirstName");

            entity.Property(x => x.LastNameId)
                .HasColumnName("LastName");

            entity.Property(x => x.BirthDate)
                .HasColumnName("BirthDate");
        });

        modelBuilder.Entity<DicFirstName>(entity =>
        {
            entity.ToTable("DicFirstNames");

            entity.HasKey(x => x.FirstNameId);

            entity.Property(x => x.FirstNameId)
                .HasColumnName("FirstNameID");

            entity.Property(x => x.FirstName)
                .HasColumnName("FirstName");
        });

        modelBuilder.Entity<DicLastName>(entity =>
        {
            entity.ToTable("DicLastNames");

            entity.HasKey(x => x.LastNameId);

            entity.Property(x => x.LastNameId)
                .HasColumnName("LastNameID");

            entity.Property(x => x.LastName)
                .HasColumnName("LastName");
        });
    }
}