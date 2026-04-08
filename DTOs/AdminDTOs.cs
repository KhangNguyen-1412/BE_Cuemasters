using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.DTOs
{
    public class AdminDashboardStatsResponse
    {
        public decimal Revenue { get; set; }
        public int ActiveSessions { get; set; }
        public int TotalBookings { get; set; }
        public int AvailableTables { get; set; }
        public int TotalTables { get; set; }
        public int ActiveCoaches { get; set; }
        public int MenuItems { get; set; }
        public int ActiveMemberships { get; set; }
        public int NoShowsToday { get; set; }
        public decimal ForfeitedDepositsToday { get; set; }
    }

    public class AdminTableResponse
    {
        public int Id { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public string ManualStatus { get; set; } = string.Empty;
        public string DisplayStatus { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public double? PositionX { get; set; }
        public double? PositionY { get; set; }
        public string? CurrentCustomerName { get; set; }
        public DateTime? CurrentSessionStartedAt { get; set; }
        public DateTime? NextBookingStartTime { get; set; }
        public string? NextBookingId { get; set; }
        public string? NextCustomerName { get; set; }
        public decimal CurrentSessionAmount { get; set; }
    }

    public class AdminUpsertTableRequest
    {
        public string TableNumber { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public double? PositionX { get; set; }
        public double? PositionY { get; set; }
    }

    public class AdminBookingResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        // Null for unassigned online reservations.
        public int? TableId { get; set; }
        public string? TableNumber { get; set; }
        // Category the customer reserved.
        public TableType RequestedTableType { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FnBTotal { get; set; }
        public decimal CoachingTotal { get; set; }
        public decimal PaymentAmount { get; set; }
        public string? PaymentStatus { get; set; }
        public string Status { get; set; } = string.Empty;
        public string BookingType { get; set; } = "Online";
        public decimal DepositAmount { get; set; }
        public bool DepositForfeited { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public decimal? ActualCost { get; set; }
        public string? GuestName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CoachName { get; set; }
    }

    public class AdminBookingListResponse
    {
        public List<AdminBookingResponse> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class AdminUpdateBookingStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class AdminCoachResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Specialty { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public decimal Rating { get; set; }
        public int TotalSessions { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
    }

    public class AdminUpsertCoachRequest
    {
        public string? UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Specialty { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AdminFnBMenuItemResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; }
        public int OrderCount { get; set; }
    }

    public class AdminUpsertFnBMenuItemRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class AdminMembershipPlanResponse
    {
        public int Id { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public int TableDiscountPercent { get; set; }
        public bool PriorityBooking { get; set; }
        public int FreeCoachingSessionsPerMonth { get; set; }
        public int MaxAdvanceBookingDays { get; set; }
        public bool IsActive { get; set; }
        public int ActiveSubscribers { get; set; }
    }

    public class AdminUpsertMembershipPlanRequest
    {
        public string Tier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public int TableDiscountPercent { get; set; }
        public bool PriorityBooking { get; set; }
        public int FreeCoachingSessionsPerMonth { get; set; }
        public int MaxAdvanceBookingDays { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AdminRevenuePointResponse
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int BookingCount { get; set; }
    }

    public class AdminRevenueSourceResponse
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class AdminHeatmapCellResponse
    {
        public int DayOfWeek { get; set; }
        public int Hour { get; set; }
        public int BookingCount { get; set; }
        public decimal OccupancyRate { get; set; }
    }

    public class AdminPeakHourResponse
    {
        public int Hour { get; set; }
        public int BookingCount { get; set; }
        public decimal OccupancyRate { get; set; }
    }

    public class AdminAnalyticsResponse
    {
        public string Period { get; set; } = string.Empty;
        public List<AdminRevenuePointResponse> RevenueByPeriod { get; set; } = new();
        public List<AdminRevenueSourceResponse> RevenueBySource { get; set; } = new();
        public List<AdminHeatmapCellResponse> OccupancyHeatmap { get; set; } = new();
        public List<AdminPeakHourResponse> PeakHours { get; set; } = new();
        public decimal AverageOccupancyRate { get; set; }
        public decimal PeakOccupancyRate { get; set; }
    }
    public class AdminCreateBookingRequest : CreateBookingRequest
    {
        public Guid UserId { get; set; }
    }

    public class CheckoutRequest
    {
        public string PaymentMethod { get; set; } = "Cash";
    }

    public class CheckoutSummary
    {
        public Guid BookingId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string BookingType { get; set; } = "Online";
        public DateTime? CheckedInAt { get; set; }
        public DateTime CheckedOutAt { get; set; }
        public decimal ActualDurationHours { get; set; }
        public decimal TableCost { get; set; }
        public decimal FnBCost { get; set; }
        public decimal CoachingCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal TotalCost { get; set; }
        public decimal AmountDue { get; set; }
    }

    public class WalkInRequest
    {
        public string GuestName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Admin check-in request. Dynamically assigns a specific table to an online reservation
    /// that was booked by category (TableId was null until this point).
    /// </summary>
    public class CheckInRequest
    {
        public int TableId { get; set; }
    }

    /// <summary>
    /// Admin links an independent (table-agnostic) CoachingSession into a running table
    /// booking's bill at checkout. Used by the "Add Coach Fee" button in CheckoutPanel.
    /// </summary>
    public class LinkCoachSessionRequest
    {
        public Guid CoachingSessionId { get; set; }
    }

    public class PendingCheckinResponse
    {
        public Guid BookingId { get; set; }
        public string? UserFullName { get; set; }
        public string? UserEmail { get; set; }
        public string? GuestName { get; set; }
        public TableType RequestedTableType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal DepositAmount { get; set; }
        public int AvailableTableCount { get; set; }
    }

    public class UpcomingWarningResponse
    {
        public Guid BookingId { get; set; }
        public int? TableId { get; set; }
        public string? TableNumber { get; set; }
        public string? CurrentCustomerName { get; set; }
        public DateTime EndsAt { get; set; }
        public TableType Category { get; set; }
        public int MinutesRemaining { get; set; }
    }

    public class LinkableCoachSessionResponse
    {
        public Guid Id { get; set; }
        public Guid CoachId { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public decimal Cost { get; set; }
    }
}
