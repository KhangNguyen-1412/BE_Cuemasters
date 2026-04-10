using Microsoft.EntityFrameworkCore;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<BilliardTable> Tables { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<BookingSlot> BookingSlots { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<ReservationSlot> ReservationSlots { get; set; } = null!;
        public DbSet<TableSession> TableSessions { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;
        public DbSet<BufferConfig> BufferConfigs { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        public DbSet<Coach> Coaches { get; set; } = null!;
        public DbSet<CoachAvailability> CoachAvailabilities { get; set; } = null!;
        public DbSet<CoachingSession> CoachingSessions { get; set; } = null!;
        
        public DbSet<FnBMenuItem> FnBMenuItems { get; set; } = null!;
        public DbSet<FnBOrder> FnBOrders { get; set; } = null!;
        public DbSet<FnBOrderItem> FnBOrderItems { get; set; } = null!;
        
        public DbSet<MembershipPlan> MembershipPlans { get; set; } = null!;
        public DbSet<UserMembership> UserMemberships { get; set; } = null!;
        public DbSet<MembershipBenefitUsage> MembershipBenefitUsages { get; set; } = null!;
        
        public DbSet<Payment> Payments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
