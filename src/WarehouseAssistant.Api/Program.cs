using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using WarehouseAssistant.Api.Data;
using WarehouseAssistant.Api.Endpoints;
using WarehouseAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WarehouseDb")));

builder.Services.AddScoped<MovementService>();
builder.Services.AddScoped<StockService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.MapCatalogEndpoints();
app.MapMovementEndpoints();
app.MapStockEndpoints();

app.Run();
