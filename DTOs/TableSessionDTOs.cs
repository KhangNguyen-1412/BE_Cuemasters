namespace BilliardsBooking.API.DTOs
{
    public class TableSessionResponse
    {
        public Guid Id { get; set; }
        public int TableId { get; set; }
        public string? TableNumber { get; set; }
        public Guid? ReservationId { get; set; }
        public Guid? UserId { get; set; }
        public string? GuestName { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? InvoiceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RunningTotalResponse
    {
        public decimal TableTimeCost { get; set; }
        public decimal FnBTotal { get; set; }
        public decimal CoachingTotal { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DepositApplied { get; set; }
        public decimal EstimatedBalanceDue { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class AddSessionFnBRequest
    {
        public List<FnBOrderRequest> Items { get; set; } = new();
    }

    public class TransferTableRequest
    {
        public int NewTableId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ExtendSessionRequest
    {
        public int AdditionalMinutes { get; set; }
    }

    public class FloorPlanSnapshotResponse
    {
        public DateTime Date { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<TableSnapshotDto> Tables { get; set; } = new();
    }

    public class TableSnapshotDto
    {
        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string RealTimeStatus { get; set; } = string.Empty;
        public Guid? ActiveSessionId { get; set; }
    }
}
