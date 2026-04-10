using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BilliardsBooking.API.Data.Migrations
{
    public partial class DataMigrationBookingToNewModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[Bookings]', N'U') IS NOT NULL
BEGIN
    INSERT INTO Reservations (Id, UserId, RequestedTableType, BookingDate, StartTime, EndTime, Status, DepositAmount, DepositForfeited, CreatedAt, CancelledAt, ConfirmedAt)
    SELECT b.Id, b.UserId, b.RequestedTableType, b.BookingDate, b.StartTime, b.EndTime, b.Status, b.DepositAmount, b.DepositForfeited, b.CreatedAt, b.CancelledAt,
           CASE WHEN b.Status IN (1, 2, 3, 5) THEN ISNULL(b.CreatedAt, GETUTCDATE()) ELSE NULL END
    FROM Bookings b
    WHERE b.BookingType = 0
      AND NOT EXISTS (SELECT 1 FROM Reservations r WHERE r.Id = b.Id);

    INSERT INTO ReservationSlots (Id, ReservationId, RequestedTableType, SlotDate, SlotStart, IsActive)
    SELECT bs.Id, bs.BookingId, bs.RequestedTableType, bs.SlotDate, bs.SlotStart, bs.IsActive
    FROM BookingSlots bs
    INNER JOIN Bookings b ON b.Id = bs.BookingId AND b.BookingType = 0
    WHERE NOT EXISTS (SELECT 1 FROM ReservationSlots rs WHERE rs.Id = bs.Id);

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
      AND (b.CheckedInAt IS NOT NULL OR b.BookingType = 1)
      AND NOT EXISTS (SELECT 1 FROM TableSessions ts WHERE ts.Id = b.Id);

    INSERT INTO Invoices
    (
        Id, TableSessionId, UserId, GuestName, TableId, TableNumber, TableType, ServiceDate, ServiceStartedAt, ServiceEndedAt,
        PaymentCompletedAt, SessionDurationHours, TableHourlyRateSnapshot, TableTimeCost, FnBTotal, CoachingTotal, DiscountAmount,
        MembershipDiscountPercent, MembershipTierSnapshot, DepositApplied, GrandTotal, BalanceDue, CreatedAt
    )
    SELECT
        b.Id,
        b.Id,
        b.UserId,
        b.GuestName,
        t.Id,
        t.TableNumber,
        t.Type,
        CAST(COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))) AS date),
        COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))),
        b.CheckedOutAt,
        (SELECT MAX(p.CreatedAt) FROM Payments p WHERE p.BookingId = b.Id AND p.Status = 1),
        CAST(ROUND(DATEDIFF(SECOND, COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))), b.CheckedOutAt) / 3600.0, 2) AS decimal(18,2)),
        t.HourlyRate,
        CAST(ROUND((DATEDIFF(SECOND, COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))), b.CheckedOutAt) / 3600.0) * t.HourlyRate, 2) AS decimal(18,2)),
        ISNULL((SELECT SUM(o.TotalAmount) FROM FnBOrders o WHERE o.TableSessionId = b.Id), 0),
        ISNULL((SELECT SUM(cs.Cost) FROM CoachingSessions cs WHERE cs.TableSessionId = b.Id), 0),
        b.DiscountAmount,
        0,
        NULL,
        CASE WHEN b.BookingType = 0 THEN b.DepositAmount ELSE 0 END,
        CAST(ROUND(
            ((DATEDIFF(SECOND, COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))), b.CheckedOutAt) / 3600.0) * t.HourlyRate)
            + ISNULL((SELECT SUM(o.TotalAmount) FROM FnBOrders o WHERE o.TableSessionId = b.Id), 0)
            + ISNULL((SELECT SUM(cs.Cost) FROM CoachingSessions cs WHERE cs.TableSessionId = b.Id), 0)
            - b.DiscountAmount, 2) AS decimal(18,2)),
        CAST(ROUND(
            (((DATEDIFF(SECOND, COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))), b.CheckedOutAt) / 3600.0) * t.HourlyRate)
            + ISNULL((SELECT SUM(o.TotalAmount) FROM FnBOrders o WHERE o.TableSessionId = b.Id), 0)
            + ISNULL((SELECT SUM(cs.Cost) FROM CoachingSessions cs WHERE cs.TableSessionId = b.Id), 0)
            - b.DiscountAmount)
            - CASE WHEN b.BookingType = 0 THEN b.DepositAmount ELSE 0 END, 2) AS decimal(18,2)),
        COALESCE(b.CheckedOutAt, b.CreatedAt)
    FROM Bookings b
    INNER JOIN Tables t ON t.Id = b.TableId
    WHERE b.CheckedOutAt IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM Invoices i WHERE i.Id = b.Id);

    INSERT INTO InvoiceLines (Id, InvoiceId, Type, Description, UnitPrice, Quantity, Total)
    SELECT NEWID(), b.Id, 0,
           CONCAT(t.TableNumber, ' - migrated table time'),
           t.HourlyRate,
           CAST(ROUND(DATEDIFF(SECOND, COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))), b.CheckedOutAt) / 3600.0, 2) AS decimal(18,2)),
           CAST(ROUND((DATEDIFF(SECOND, COALESCE(b.CheckedInAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))), b.CheckedOutAt) / 3600.0) * t.HourlyRate, 2) AS decimal(18,2))
    FROM Bookings b
    INNER JOIN Tables t ON t.Id = b.TableId
    WHERE b.CheckedOutAt IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM InvoiceLines l WHERE l.InvoiceId = b.Id AND l.Type = 0);

    INSERT INTO InvoiceLines (Id, InvoiceId, Type, Description, UnitPrice, Quantity, Total)
    SELECT NEWID(), i.Id, 1, 'Migrated F&B total', i.FnBTotal, 1, i.FnBTotal
    FROM Invoices i
    WHERE i.FnBTotal > 0
      AND NOT EXISTS (SELECT 1 FROM InvoiceLines l WHERE l.InvoiceId = i.Id AND l.Type = 1);

    INSERT INTO InvoiceLines (Id, InvoiceId, Type, Description, UnitPrice, Quantity, Total)
    SELECT NEWID(), i.Id, 2, 'Migrated coaching total', i.CoachingTotal, 1, i.CoachingTotal
    FROM Invoices i
    WHERE i.CoachingTotal > 0
      AND NOT EXISTS (SELECT 1 FROM InvoiceLines l WHERE l.InvoiceId = i.Id AND l.Type = 2);

    INSERT INTO InvoiceLines (Id, InvoiceId, Type, Description, UnitPrice, Quantity, Total)
    SELECT NEWID(), i.Id, 3, 'Migrated discount', -i.DiscountAmount, 1, -i.DiscountAmount
    FROM Invoices i
    WHERE i.DiscountAmount > 0
      AND NOT EXISTS (SELECT 1 FROM InvoiceLines l WHERE l.InvoiceId = i.Id AND l.Type = 3);

    INSERT INTO InvoiceLines (Id, InvoiceId, Type, Description, UnitPrice, Quantity, Total)
    SELECT NEWID(), i.Id, 4, 'Migrated deposit deduction', -i.DepositApplied, 1, -i.DepositApplied
    FROM Invoices i
    WHERE i.DepositApplied > 0
      AND NOT EXISTS (SELECT 1 FROM InvoiceLines l WHERE l.InvoiceId = i.Id AND l.Type = 4);

    UPDATE p
    SET
        ReservationId = CASE
            WHEN p.UserMembershipId IS NOT NULL THEN NULL
            WHEN b.BookingType = 0 AND b.CheckedOutAt IS NULL THEN b.Id
            WHEN b.BookingType = 0 AND b.Status = 5 THEN b.Id
            ELSE p.ReservationId
        END,
        InvoiceId = CASE
            WHEN p.UserMembershipId IS NOT NULL THEN NULL
            WHEN b.CheckedOutAt IS NOT NULL THEN b.Id
            ELSE p.InvoiceId
        END,
        Type = CASE
            WHEN p.UserMembershipId IS NOT NULL THEN 4
            WHEN b.BookingType = 1 THEN 2
            WHEN b.CheckedOutAt IS NOT NULL THEN 1
            ELSE 0
        END,
        CompletedAt = CASE WHEN p.Status = 1 THEN p.CreatedAt ELSE p.CompletedAt END
    FROM Payments p
    LEFT JOIN Bookings b ON b.Id = p.BookingId;

    INSERT INTO Payments (Id, UserId, BookingId, ReservationId, UserMembershipId, InvoiceId, Type, Amount, Method, Status, TransactionId, Notes, CreatedAt, CompletedAt)
    SELECT NEWID(),
           b.UserId,
           NULL,
           b.Id,
           NULL,
           NULL,
           0,
           b.DepositAmount,
           0,
           1,
           NULL,
           'Migrated deposit payment',
           ISNULL((SELECT MIN(p.CreatedAt) FROM Payments p WHERE p.BookingId = b.Id), b.CreatedAt),
           ISNULL((SELECT MIN(p.CreatedAt) FROM Payments p WHERE p.BookingId = b.Id), b.CreatedAt)
    FROM Bookings b
    WHERE b.BookingType = 0
      AND b.CheckedOutAt IS NOT NULL
      AND b.DepositAmount > 0
      AND NOT EXISTS (SELECT 1 FROM Payments p WHERE p.ReservationId = b.Id AND p.Type = 0 AND p.Amount = b.DepositAmount);

    INSERT INTO Payments (Id, UserId, BookingId, ReservationId, UserMembershipId, InvoiceId, Type, Amount, Method, Status, TransactionId, Notes, CreatedAt, CompletedAt)
    SELECT NEWID(),
           b.UserId,
           NULL,
           b.Id,
           NULL,
           NULL,
           5,
           b.DepositAmount,
           0,
           1,
           NULL,
           'Deposit forfeited - no-show',
           ISNULL(b.CancelledAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2))),
           ISNULL(b.CancelledAt, DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('00:00:00' AS time), b.StartTime), CAST(b.BookingDate AS datetime2)))
    FROM Bookings b
    WHERE b.BookingType = 0
      AND b.Status = 5
      AND b.DepositForfeited = 1
      AND b.DepositAmount > 0
      AND NOT EXISTS (SELECT 1 FROM Payments p WHERE p.ReservationId = b.Id AND p.Type = 5);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM InvoiceLines WHERE Description LIKE 'Migrated %';
DELETE FROM Invoices WHERE Id IN (SELECT Id FROM Bookings);
DELETE FROM TableSessions WHERE Id IN (SELECT Id FROM Bookings);
DELETE FROM ReservationSlots WHERE ReservationId IN (SELECT Id FROM Bookings WHERE BookingType = 0);
DELETE FROM Reservations WHERE Id IN (SELECT Id FROM Bookings WHERE BookingType = 0);
DELETE FROM Payments WHERE Notes IN ('Migrated deposit payment', 'Deposit forfeited - no-show');
");
        }
    }
}
