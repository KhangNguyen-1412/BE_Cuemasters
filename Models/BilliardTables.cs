using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class BilliardTable
    {
        public int Id { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public TableType Type { get; set; }
        public decimal HourlyRate { get; set; }
        public TableManualStatus Status { get; set; } = TableManualStatus.Available;
        public TableRealTimeStatus RealTimeStatus { get; set; } = TableRealTimeStatus.Available;
        public double? PositionX { get; set; }
        public double? PositionY { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<TableSession> TableSessions { get; set; } = new List<TableSession>();
    }

    public class Booking
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }  // Nullable for walk-in bookings
        public User? User { get; set; }

        // Null for unassigned online reservations; set when admin assigns a physical table at check-in,
        // or set immediately for walk-ins.
        public int? TableId { get; set; }
        public BilliardTable? Table { get; set; }

        // The category the customer reserved. Walk-ins still set this from the chosen table's Type.
        public TableType RequestedTableType { get; set; }

        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public decimal TotalTableCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public BookingType BookingType { get; set; } = BookingType.Online;

        // Deposit tracking
        public decimal DepositAmount { get; set; }
        public bool DepositForfeited { get; set; }

        // Actual session tracking (real timer, not booked time)
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public DateTime? AssignedAt { get; set; }  // When admin assigned a physical table at check-in
        public decimal? ActualCost { get; set; }  // Calculated at checkout

        // Walk-in guest name (no UserId required)
        public string? GuestName { get; set; }

        public byte[]? RowVersion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CancelledAt { get; set; }

        public ICollection<BookingSlot> Slots { get; set; } = new List<BookingSlot>();
    }

    public class BookingSlot
    {
        public long Id { get; set; }

        public Guid BookingId { get; set; }
        public Booking? Booking { get; set; }

        // Nullable: online reservations don't claim a specific table until check-in.
        public int? TableId { get; set; }
        public BilliardTable? Table { get; set; }

        // Category-level slot occupancy (drives the buffer/capacity check).
        public TableType RequestedTableType { get; set; }

        public DateTime SlotDate { get; set; }
        public TimeSpan SlotStart { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
