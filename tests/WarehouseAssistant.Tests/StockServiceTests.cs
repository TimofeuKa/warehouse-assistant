using Microsoft.EntityFrameworkCore;
using WarehouseAssistant.Api.Contracts;
using WarehouseAssistant.Api.Data;
using WarehouseAssistant.Api.Services;

namespace WarehouseAssistant.Tests;

public sealed class StockServiceTests
{
    [Fact]
    public async Task GetStockAsync_CalculatesIncomeExpenseAndTransfer()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);
        var stockService = new StockService(db);
        var startedAt = new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero);

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt,
            FromWarehouseId: null,
            ToWarehouseId: 1,
            [
                new MovementItemRequest(1, 10),
                new MovementItemRequest(2, 3)
            ]));

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt.AddMinutes(10),
            FromWarehouseId: 1,
            ToWarehouseId: null,
            [new MovementItemRequest(1, 4)]));

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt.AddMinutes(20),
            FromWarehouseId: 1,
            ToWarehouseId: 2,
            [new MovementItemRequest(1, 2)]));

        var firstWarehouseStock = await stockService.GetStockAsync(1, startedAt.AddMinutes(30));
        var secondWarehouseStock = await stockService.GetStockAsync(2, startedAt.AddMinutes(30));

        Assert.Equal(4, firstWarehouseStock.Single(x => x.NomenclatureId == 1).Quantity);
        Assert.Equal(3, firstWarehouseStock.Single(x => x.NomenclatureId == 2).Quantity);
        Assert.Equal(2, secondWarehouseStock.Single(x => x.NomenclatureId == 1).Quantity);
    }

    [Fact]
    public async Task GetStockAsync_DoesNotIncludeFutureMovements()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);
        var stockService = new StockService(db);
        var startedAt = new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero);

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt,
            FromWarehouseId: null,
            ToWarehouseId: 1,
            [new MovementItemRequest(1, 10)]));

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt.AddHours(1),
            FromWarehouseId: 1,
            ToWarehouseId: null,
            [new MovementItemRequest(1, 7)]));

        var stock = await stockService.GetStockAsync(1, startedAt.AddMinutes(30));

        Assert.Equal(10, stock.Single(x => x.NomenclatureId == 1).Quantity);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateNomenclatureInOneMovement()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                DateTimeOffset.UtcNow,
                FromWarehouseId: null,
                ToWarehouseId: 1,
                [
                    new MovementItemRequest(1, 1),
                    new MovementItemRequest(1, 2)
                ])));

        Assert.Contains("не должны повторяться", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsZeroQuantity()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                DateTimeOffset.UtcNow,
                FromWarehouseId: null,
                ToWarehouseId: 1,
                [new MovementItemRequest(1, 0)])));

        Assert.Contains("Количество", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsNullItems()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                DateTimeOffset.UtcNow,
                FromWarehouseId: null,
                ToWarehouseId: 1,
                Items: null)));

        Assert.Equal("Добавьте хотя бы одну ТМЦ.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyItems()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                DateTimeOffset.UtcNow,
                FromWarehouseId: null,
                ToWarehouseId: 1,
                Items: [])));

        Assert.Equal("Добавьте хотя бы одну ТМЦ.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsSameSourceAndDestinationWarehouse()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                DateTimeOffset.UtcNow,
                FromWarehouseId: 1,
                ToWarehouseId: 1,
                [new MovementItemRequest(1, 1)])));

        Assert.Contains("не должны совпадать", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsExpenseThatMakesStockNegative()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);
        var startedAt = new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero);

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt,
            FromWarehouseId: null,
            ToWarehouseId: 1,
            [new MovementItemRequest(1, 5)]));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                startedAt.AddMinutes(5),
                FromWarehouseId: 1,
                ToWarehouseId: null,
                [new MovementItemRequest(1, 6)])));

        Assert.Contains("Недостаточно остатка", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsBackdatedExpenseThatMakesFutureStockNegative()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);
        var startedAt = new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero);

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt,
            FromWarehouseId: null,
            ToWarehouseId: 1,
            [new MovementItemRequest(1, 10)]));

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt.AddHours(2),
            FromWarehouseId: 1,
            ToWarehouseId: null,
            [new MovementItemRequest(1, 10)]));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.CreateAsync(new CreateMovementRequest(
                startedAt.AddHours(1),
                FromWarehouseId: 1,
                ToWarehouseId: null,
                [new MovementItemRequest(1, 1)])));

        Assert.Contains("отрицательному остатку", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_RejectsDeletingIncomeUsedByLaterExpense()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);
        var startedAt = new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero);

        var income = await movementService.CreateAsync(new CreateMovementRequest(
            startedAt,
            FromWarehouseId: null,
            ToWarehouseId: 1,
            [new MovementItemRequest(1, 5)]));

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt.AddMinutes(5),
            FromWarehouseId: 1,
            ToWarehouseId: null,
            [new MovementItemRequest(1, 5)]));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            movementService.DeleteAsync(income.Id));

        Assert.Contains("Нельзя удалить движение", exception.Message);
        Assert.NotNull(await db.Movements.FindAsync(income.Id));
    }

    [Fact]
    public async Task DeleteAsync_AllowsDeletingExpense()
    {
        await using var db = CreateDbContext();
        var movementService = new MovementService(db);
        var stockService = new StockService(db);
        var startedAt = new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero);

        await movementService.CreateAsync(new CreateMovementRequest(
            startedAt,
            FromWarehouseId: null,
            ToWarehouseId: 1,
            [new MovementItemRequest(1, 5)]));

        var expense = await movementService.CreateAsync(new CreateMovementRequest(
            startedAt.AddMinutes(5),
            FromWarehouseId: 1,
            ToWarehouseId: null,
            [new MovementItemRequest(1, 3)]));

        var deleted = await movementService.DeleteAsync(expense.Id);
        var stock = await stockService.GetStockAsync(1, startedAt.AddMinutes(10));

        Assert.True(deleted);
        Assert.Equal(5, stock.Single(x => x.NomenclatureId == 1).Quantity);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new WarehouseDbContext(options);
        db.Database.EnsureCreated();

        return db;
    }
}
