using System;
using System.Collections.Generic;

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
        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
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
        public bool IsActive { get; set; } = true;
    }

    public class AdminRevenuePointResponse
    {
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int BookingCount { get; set; }
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
        public List<AdminHeatmapCellResponse> OccupancyHeatmap { get; set; } = new();
        public List<AdminPeakHourResponse> PeakHours { get; set; } = new();
        public decimal AverageOccupancyRate { get; set; }
        public decimal PeakOccupancyRate { get; set; }
    }
    public class AdminCreateBookingRequest : CreateBookingRequest
    {
        public Guid UserId { get; set; }
    }
}
