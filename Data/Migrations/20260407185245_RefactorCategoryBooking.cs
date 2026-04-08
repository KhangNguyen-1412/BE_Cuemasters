using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BilliardsBooking.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCategoryBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Tables_TableId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CoachingSessions_Bookings_BookingId",
                table: "CoachingSessions");

            migrationBuilder.DropIndex(
                name: "IX_CoachingSessions_CoachId_SessionDate_StartTime",
                table: "CoachingSessions");

            migrationBuilder.DropIndex(
                name: "IX_BookingSlots_TableId_SlotDate_SlotStart",
                table: "BookingSlots");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "CoachingSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CoachingSessions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LinkedAt",
                table: "CoachingSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "BookingSlots",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "RequestedTableType",
                table: "BookingSlots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Bookings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedTableType",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CoachingSessions_CoachId_SessionDate_StartTime",
                table: "CoachingSessions",
                columns: new[] { "CoachId", "SessionDate", "StartTime" },
                unique: true,
                filter: "[CancelledAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_RequestedTableType_SlotDate_SlotStart_IsActive",
                table: "BookingSlots",
                columns: new[] { "RequestedTableType", "SlotDate", "SlotStart", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_TableId",
                table: "BookingSlots",
                column: "TableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Tables_TableId",
                table: "Bookings",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CoachingSessions_Bookings_BookingId",
                table: "CoachingSessions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Tables_TableId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CoachingSessions_Bookings_BookingId",
                table: "CoachingSessions");

            migrationBuilder.DropIndex(
                name: "IX_CoachingSessions_CoachId_SessionDate_StartTime",
                table: "CoachingSessions");

            migrationBuilder.DropIndex(
                name: "IX_BookingSlots_RequestedTableType_SlotDate_SlotStart_IsActive",
                table: "BookingSlots");

            migrationBuilder.DropIndex(
                name: "IX_BookingSlots_TableId",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "CoachingSessions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CoachingSessions");

            migrationBuilder.DropColumn(
                name: "LinkedAt",
                table: "CoachingSessions");

            migrationBuilder.DropColumn(
                name: "RequestedTableType",
                table: "BookingSlots");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RequestedTableType",
                table: "Bookings");

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "BookingSlots",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachingSessions_CoachId_SessionDate_StartTime",
                table: "CoachingSessions",
                columns: new[] { "CoachId", "SessionDate", "StartTime" },
                unique: true,
                filter: "[IsCompleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSlots_TableId_SlotDate_SlotStart",
                table: "BookingSlots",
                columns: new[] { "TableId", "SlotDate", "SlotStart" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Tables_TableId",
                table: "Bookings",
                column: "TableId",
                principalTable: "Tables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CoachingSessions_Bookings_BookingId",
                table: "CoachingSessions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }
    }
}
