using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.DTOs
{
    public class ReservationResponse
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public TableType RequestedTableType { get; set; }
        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal DepositAmount { get; set; }
        public bool DepositForfeited { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public Guid? TableSessionId { get; set; }
    }

    public class ReservationListResponse
    {
        public List<ReservationResponse> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class RefundReservationRequest
    {
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
