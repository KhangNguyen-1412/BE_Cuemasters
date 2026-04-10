using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class ReservationSlot
    {
        public long Id { get; set; }
        public Guid ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        public TableType RequestedTableType { get; set; }
        public DateTime SlotDate { get; set; }
        public TimeSpan SlotStart { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
