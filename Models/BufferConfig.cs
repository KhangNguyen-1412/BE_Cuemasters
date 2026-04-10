using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class BufferConfig
    {
        public int Id { get; set; }
        public TableType TableType { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeSpan? TimeFrom { get; set; }
        public TimeSpan? TimeTo { get; set; }
        public int BufferCount { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
