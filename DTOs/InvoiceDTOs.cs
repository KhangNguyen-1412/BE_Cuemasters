namespace BilliardsBooking.API.DTOs
{
    public class InvoiceResponse
    {
        public Guid Id { get; set; }
        public Guid TableSessionId { get; set; }
        public Guid? UserId { get; set; }
        public string? GuestName { get; set; }
        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string TableType { get; set; } = string.Empty;
        public DateTime ServiceDate { get; set; }
        public DateTime ServiceStartedAt { get; set; }
        public DateTime ServiceEndedAt { get; set; }
        public DateTime? PaymentCompletedAt { get; set; }
        public decimal SessionDurationHours { get; set; }
        public decimal TableHourlyRateSnapshot { get; set; }
        public decimal TableTimeCost { get; set; }
        public decimal FnBTotal { get; set; }
        public decimal CoachingTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal MembershipDiscountPercent { get; set; }
        public string? MembershipTierSnapshot { get; set; }
        public decimal DepositApplied { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal BalanceDue { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<InvoiceLineResponse> Lines { get; set; } = new();
        public List<PaymentResponse> Payments { get; set; } = new();
    }

    public class InvoiceLineResponse
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Total { get; set; }
    }

    public class BufferConfigResponse
    {
        public int Id { get; set; }
        public string TableType { get; set; } = string.Empty;
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeSpan? TimeFrom { get; set; }
        public TimeSpan? TimeTo { get; set; }
        public int BufferCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpsertBufferConfigRequest
    {
        public string TableType { get; set; } = string.Empty;
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeSpan? TimeFrom { get; set; }
        public TimeSpan? TimeTo { get; set; }
        public int BufferCount { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AuditLogResponse
    {
        public long Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? ActorUserId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public string? PreviousValue { get; set; }
        public string? NewValue { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
