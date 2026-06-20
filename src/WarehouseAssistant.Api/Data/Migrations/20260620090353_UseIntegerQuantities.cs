using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarehouseAssistant.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseIntegerQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "MovementItems"
                        WHERE "Quantity" <> trunc("Quantity")
                    ) THEN
                        RAISE EXCEPTION 'Cannot convert MovementItems.Quantity to integer: fractional values exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "MovementItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "MovementItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
