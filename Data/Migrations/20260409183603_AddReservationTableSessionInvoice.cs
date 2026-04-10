using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BilliardsBooking.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationTableSessionInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachingSessions_Bookings_BookingId",
                table: "CoachingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_FnBOrders_Bookings_BookingId",
                table: "FnBOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_UserMemberships_UserMembershipId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "BookingId",
                table: "FnBOrders",
                newName: "TableSessionId");

            migrationBuilder.RenameIndex(
                name: "IX_FnBOrders_BookingId",
                table: "FnBOrders",
                newName: "IX_FnBOrders_TableSessionId");

            migrationBuilder.RenameColumn(
                name: "BookingId",
                table: "CoachingSessions",
                newName: "TableSessionId");

            migrationBuilder.RenameIndex(
                name: "IX_CoachingSessions_BookingId",
                table: "CoachingSessions",
                newName: "IX_CoachingSessions_TableSessionId");

            migrationBuilder.AddColumn<int>(
                name: "RealTimeStatus",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BufferConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TableType = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    TimeFrom = table.Column<TimeSpan>(type: "time", nullable: true),
                    TimeTo = table.Column<TimeSpan>(type: "time", nullable: true),
                    BufferCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BufferConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedTableType = table.Column<int>(type: "int", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepositForfeited = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationSlots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedTableType = table.Column<int>(type: "int", nullable: false),
                    SlotDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotStart = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationSlots_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TableSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableSessions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableSessions_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    TableNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TableType = table.Column<int>(type: "int", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceStartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceEndedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionDurationHours = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TableHourlyRateSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TableTimeCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FnBTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CoachingTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MembershipDiscountPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MembershipTierSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepositApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_TableSessions_TableSessionId",
                        column: x => x.TableSessionId,
                        principalTable: "TableSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BufferConfigs_TableType_DayOfWeek_TimeFrom_TimeTo_IsActive",
                table: "BufferConfigs",
                columns: new[] { "TableType", "DayOfWeek", "TimeFrom", "TimeTo", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ServiceDate",
                table: "Invoices",
                column: "ServiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TableSessionId",
                table: "Invoices",
                column: "TableSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UserId",
                table: "Invoices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_BookingDate_StartTime_EndTime_RequestedTableType_Status",
                table: "Reservations",
                columns: new[] { "BookingDate", "StartTime", "EndTime", "RequestedTableType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId",
                table: "Reservations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSlots_RequestedTableType_SlotDate_SlotStart_IsActive",
                table: "ReservationSlots",
                columns: new[] { "RequestedTableType", "SlotDate", "SlotStart", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSlots_ReservationId",
                table: "ReservationSlots",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_TableSessions_ReservationId",
                table: "TableSessions",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TableSessions_TableId_Status",
                table: "TableSessions",
                columns: new[] { "TableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TableSessions_UserId",
                table: "TableSessions",
                column: "UserId");

            migrationBuilder.Sql(@"
INSERT INTO Reservations (Id, UserId, RequestedTableType, BookingDate, StartTime, EndTime, Status, DepositAmount, DepositForfeited, CreatedAt, CancelledAt, ConfirmedAt)
SELECT b.Id,
       b.UserId,
       b.RequestedTableType,
       b.BookingDate,
       b.StartTime,
       b.EndTime,
       b.Status,
       b.DepositAmount,
       b.DepositForfeited,
       b.CreatedAt,
       b.CancelledAt,
       CASE WHEN b.Status IN (1, 2, 3, 5) THEN ISNULL(b.CreatedAt, GETUTCDATE()) ELSE NULL END
FROM Bookings b
WHERE b.BookingType = 0
  AND (EXISTS (SELECT 1 FROM FnBOrders f WHERE f.TableSessionId = b.Id)
       OR EXISTS (SELECT 1 FROM CoachingSessions c WHERE c.TableSessionId = b.Id))
  AND NOT EXISTS (SELECT 1 FROM Reservations r WHERE r.Id = b.Id);
");

            migrationBuilder.Sql(@"
INSERT INTO TableSessions (Id, TableId, ReservationId, UserId, GuestName, Type, StartedAt, EndedAt, Status, CreatedAt)
SELECT b.Id,
       b.TableId,
       CASE WHEN b.BookingType = 0 THEN b.Id ELSE NULL END,
       b.UserId,
       b.GuestName,
       b.BookingType,
       COALESCE(
           b.CheckedInAt,
           DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))
       ),
       b.CheckedOutAt,
       CASE WHEN b.CheckedOutAt IS NULL THEN 0 ELSE 1 END,
       b.CreatedAt
FROM Bookings b
WHERE b.TableId IS NOT NULL
  AND (EXISTS (SELECT 1 FROM FnBOrders f WHERE f.TableSessionId = b.Id)
       OR EXISTS (SELECT 1 FROM CoachingSessions c WHERE c.TableSessionId = b.Id))
  AND NOT EXISTS (SELECT 1 FROM TableSessions ts WHERE ts.Id = b.Id);
");

            migrationBuilder.Sql(@"
DELETE i
FROM FnBOrderItems i
INNER JOIN FnBOrders o ON o.Id = i.FnBOrderId
LEFT JOIN TableSessions ts ON ts.Id = o.TableSessionId
WHERE ts.Id IS NULL;

DELETE o
FROM FnBOrders o
LEFT JOIN TableSessions ts ON ts.Id = o.TableSessionId
WHERE ts.Id IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE c
SET c.TableSessionId = NULL
FROM CoachingSessions c
LEFT JOIN TableSessions ts ON ts.Id = c.TableSessionId
WHERE c.TableSessionId IS NOT NULL
  AND ts.Id IS NULL;
");

            migrationBuilder.AddForeignKey(
                name: "FK_CoachingSessions_TableSessions_TableSessionId",
                table: "CoachingSessions",
                column: "TableSessionId",
                principalTable: "TableSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FnBOrders_TableSessions_TableSessionId",
                table: "FnBOrders",
                column: "TableSessionId",
                principalTable: "TableSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_UserMemberships_UserMembershipId",
                table: "Payments",
                column: "UserMembershipId",
                principalTable: "UserMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachingSessions_TableSessions_TableSessionId",
                table: "CoachingSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_FnBOrders_TableSessions_TableSessionId",
                table: "FnBOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_UserMemberships_UserMembershipId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BufferConfigs");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "ReservationSlots");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "TableSessions");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RealTimeStatus",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "TableSessionId",
                table: "FnBOrders",
                newName: "BookingId");

            migrationBuilder.RenameIndex(
                name: "IX_FnBOrders_TableSessionId",
                table: "FnBOrders",
                newName: "IX_FnBOrders_BookingId");

            migrationBuilder.RenameColumn(
                name: "TableSessionId",
                table: "CoachingSessions",
                newName: "BookingId");

            migrationBuilder.RenameIndex(
                name: "IX_CoachingSessions_TableSessionId",
                table: "CoachingSessions",
                newName: "IX_CoachingSessions_BookingId");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CoachingSessions_Bookings_BookingId",
                table: "CoachingSessions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FnBOrders_Bookings_BookingId",
                table: "FnBOrders",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_UserMemberships_UserMembershipId",
                table: "Payments",
                column: "UserMembershipId",
                principalTable: "UserMemberships",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
