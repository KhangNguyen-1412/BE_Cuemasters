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
        public double? PositionX { get; set; }
        public double? PositionY { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }

    public class Booking
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public int TableId { get; set; }
        public BilliardTable? Table { get; set; }

        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public decimal TotalTableCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

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

        public int TableId { get; set; }
        public BilliardTable? Table { get; set; }

        public DateTime SlotDate { get; set; }
        public TimeSpan SlotStart { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
