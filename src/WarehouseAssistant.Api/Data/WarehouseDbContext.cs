using System;
using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Domain;

namespace WarehouseAssistant.Api.Data;

public sealed class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : DbContext(options)
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Nomenclature> Nomenclatures => Set<Nomenclature>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<MovementItem> MovementItems => Set<MovementItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();

            entity.HasData(
                new Warehouse { Id = 1, Name = "Основной склад" },
                new Warehouse { Id = 2, Name = "Розничный склад" },
                new Warehouse { Id = 3, Name = "Склад брака" });
        });

        modelBuilder.Entity<Nomenclature>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();

            entity.HasData(
                new Nomenclature { Id = 1, Name = "Ноутбук" },
                new Nomenclature { Id = 2, Name = "Монитор" },
                new Nomenclature { Id = 3, Name = "Клавиатура" },
                new Nomenclature { Id = 4, Name = "Мышь" },
                new Nomenclature { Id = 5, Name = "Сетевой кабель" },
                new Nomenclature { Id = 6, Name = "Принтер" },
                new Nomenclature { Id = 7, Name = "Тонер" });
        });

        modelBuilder.Entity<Movement>(entity =>
        {
            entity.Property(x => x.OccurredAt).IsRequired();

            entity.HasOne(x => x.FromWarehouse)
                .WithMany()
                .HasForeignKey(x => x.FromWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToWarehouse)
                .WithMany()
                .HasForeignKey(x => x.ToWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MovementItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3).IsRequired();

            entity.HasIndex(x => new { x.MovementId, x.NomenclatureId }).IsUnique();
        });
    }
}