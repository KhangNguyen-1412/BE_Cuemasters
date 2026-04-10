using BilliardsBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BilliardsBooking.API.Data.Configurations
{
    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.Property(r => r.RowVersion).IsRowVersion();

            builder.HasIndex(r => new { r.BookingDate, r.StartTime, r.EndTime, r.RequestedTableType, r.Status });

            builder.Property(r => r.DepositAmount).HasPrecision(18, 2);

            builder.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(r => r.Slots)
                .WithOne(s => s.Reservation)
                .HasForeignKey(s => s.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.TableSession)
                .WithOne(s => s.Reservation)
                .HasForeignKey<TableSession>(s => s.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class ReservationSlotConfiguration : IEntityTypeConfiguration<ReservationSlot>
    {
        public void Configure(EntityTypeBuilder<ReservationSlot> builder)
        {
            builder.HasIndex(s => new { s.RequestedTableType, s.SlotDate, s.SlotStart, s.IsActive });
            builder.Property(s => s.IsActive).HasDefaultValue(true);
        }
    }

    public class TableSessionConfiguration : IEntityTypeConfiguration<TableSession>
    {
        public void Configure(EntityTypeBuilder<TableSession> builder)
        {
            builder.Property(s => s.RowVersion).IsRowVersion();

            builder.HasIndex(s => new { s.TableId, s.Status });

            builder.HasOne(s => s.Table)
                .WithMany(t => t.TableSessions)
                .HasForeignKey(s => s.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(s => s.FnBOrders)
                .WithOne(o => o.TableSession)
                .HasForeignKey(o => o.TableSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(s => s.CoachingSessions)
                .WithOne(c => c.TableSession)
                .HasForeignKey(c => c.TableSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.HasIndex(i => i.ServiceDate);

            builder.Property(i => i.TableHourlyRateSnapshot).HasPrecision(18, 2);
            builder.Property(i => i.TableTimeCost).HasPrecision(18, 2);
            builder.Property(i => i.FnBTotal).HasPrecision(18, 2);
            builder.Property(i => i.CoachingTotal).HasPrecision(18, 2);
            builder.Property(i => i.DiscountAmount).HasPrecision(18, 2);
            builder.Property(i => i.MembershipDiscountPercent).HasPrecision(18, 2);
            builder.Property(i => i.DepositApplied).HasPrecision(18, 2);
            builder.Property(i => i.GrandTotal).HasPrecision(18, 2);
            builder.Property(i => i.BalanceDue).HasPrecision(18, 2);
            builder.Property(i => i.SessionDurationHours).HasPrecision(18, 2);

            builder.HasOne(i => i.TableSession)
                .WithOne(s => s.Invoice)
                .HasForeignKey<Invoice>(i => i.TableSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(i => i.Lines)
                .WithOne(l => l.Invoice)
                .HasForeignKey(l => l.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
    {
        public void Configure(EntityTypeBuilder<InvoiceLine> builder)
        {
            builder.Property(l => l.UnitPrice).HasPrecision(18, 2);
            builder.Property(l => l.Quantity).HasPrecision(18, 2);
            builder.Property(l => l.Total).HasPrecision(18, 2);
        }
    }

    public class BufferConfigConfiguration : IEntityTypeConfiguration<BufferConfig>
    {
        public void Configure(EntityTypeBuilder<BufferConfig> builder)
        {
            builder.HasIndex(c => new { c.TableType, c.DayOfWeek, c.TimeFrom, c.TimeTo, c.IsActive });
        }
    }

    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedAt });
        }
    }

    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.Property(p => p.Amount).HasPrecision(18, 2);

            builder.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Reservation)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.UserMembership)
                .WithMany()
                .HasForeignKey(p => p.UserMembershipId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class BilliardTableConfiguration : IEntityTypeConfiguration<BilliardTable>
    {
        public void Configure(EntityTypeBuilder<BilliardTable> builder)
        {
            builder.Property(t => t.HourlyRate).HasPrecision(18, 2);
        }
    }

    public class CoachConfiguration : IEntityTypeConfiguration<Coach>
    {
        public void Configure(EntityTypeBuilder<Coach> builder)
        {
            builder.Property(c => c.HourlyRate).HasPrecision(18, 2);
            builder.Property(c => c.Rating).HasPrecision(18, 2);
        }
    }

    public class CoachingSessionMoneyConfiguration : IEntityTypeConfiguration<CoachingSession>
    {
        public void Configure(EntityTypeBuilder<CoachingSession> builder)
        {
            builder.Property(c => c.Cost).HasPrecision(18, 2);
        }
    }

    public class FnBMenuItemConfiguration : IEntityTypeConfiguration<FnBMenuItem>
    {
        public void Configure(EntityTypeBuilder<FnBMenuItem> builder)
        {
            builder.Property(i => i.Price).HasPrecision(18, 2);
        }
    }

    public class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
    {
        public void Configure(EntityTypeBuilder<MembershipPlan> builder)
        {
            builder.Property(p => p.MonthlyPrice).HasPrecision(18, 2);
        }
    }

    public class FnBOrderConfiguration : IEntityTypeConfiguration<FnBOrder>
    {
        public void Configure(EntityTypeBuilder<FnBOrder> builder)
        {
            builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        }
    }

    public class FnBOrderItemConfiguration : IEntityTypeConfiguration<FnBOrderItem>
    {
        public void Configure(EntityTypeBuilder<FnBOrderItem> builder)
        {
            builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
        }
    }
}
