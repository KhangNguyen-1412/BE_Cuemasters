using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Data.Configurations
{
    public class BookingSlotConfiguration : IEntityTypeConfiguration<BookingSlot>
    {
        public void Configure(EntityTypeBuilder<BookingSlot> builder)
        {
            // Category-level slots: no table-specific uniqueness (TableId is nullable until check-in).
            // The BookingService.CreateBookingAsync enforces category capacity with a buffer.
            // Composite index to accelerate category-capacity counts.
            builder.HasIndex(bs => new { bs.RequestedTableType, bs.SlotDate, bs.SlotStart, bs.IsActive });

            builder.Property(bs => bs.IsActive).HasDefaultValue(true);

            builder.HasOne(bs => bs.Table)
                .WithMany()
                .HasForeignKey(bs => bs.TableId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }

    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.Property(b => b.RowVersion).IsRowVersion();
            builder.Property(b => b.TotalTableCost).HasPrecision(18, 2);
            builder.Property(b => b.DiscountAmount).HasPrecision(18, 2);
            builder.Property(b => b.DepositAmount).HasPrecision(18, 2);
            builder.Property(b => b.ActualCost).HasPrecision(18, 2);

            // TableId is nullable: online reservations remain unassigned until the admin checks them in.
            builder.HasOne(b => b.Table)
                .WithMany(t => t.Bookings)
                .HasForeignKey(b => b.TableId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Delete slots cascadingly if booking is deleted
            builder.HasMany(b => b.Slots)
                .WithOne(bs => bs.Booking)
                .HasForeignKey(bs => bs.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
