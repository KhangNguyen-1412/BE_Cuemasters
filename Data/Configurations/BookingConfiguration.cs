using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Data.Configurations
{
    public class BookingSlotConfiguration : IEntityTypeConfiguration<BookingSlot>
    {
        public void Configure(EntityTypeBuilder<BookingSlot> builder)
        {
            // Protect against Double Booking by database-level unique constrained filter
            builder.HasIndex(bs => new { bs.TableId, bs.SlotDate, bs.SlotStart })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            builder.Property(bs => bs.IsActive).HasDefaultValue(true);

            builder.HasOne(bs => bs.Table)
                .WithMany()
                .HasForeignKey(bs => bs.TableId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.Property(b => b.RowVersion).IsRowVersion();
            
            // Delete slots cascadingly if booking is deleted
            builder.HasMany(b => b.Slots)
                .WithOne(bs => bs.Booking)
                .HasForeignKey(bs => bs.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
