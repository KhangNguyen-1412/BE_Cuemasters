using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BilliardsBooking.API.Data.Migrations
{
    public partial class DeprecateBookingTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Bookings]', N'U') IS NOT NULL
BEGIN
    PRINT 'Bookings table retained for rollback and audit compatibility. New writes must use Reservations/TableSessions/Invoices.';
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
