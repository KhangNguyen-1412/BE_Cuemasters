using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Data.Configurations
{
    public class CoachingSessionConfiguration : IEntityTypeConfiguration<CoachingSession>
    {
        public void Configure(EntityTypeBuilder<CoachingSession> builder)
        {
            // Protect against Double Booking for Coaches
            // Note: If CancelledAt or IsActive was on CoachingSession we would filter it here
            builder.HasIndex(cs => new { cs.CoachId, cs.SessionDate, cs.StartTime })
                   .IsUnique()
                   .HasFilter("[IsCompleted] = 0"); // A proxy for an active session to prevent overlapping bookings

            builder.HasOne(cs => cs.StudentUser)
                   .WithMany()
                   .HasForeignKey(cs => cs.StudentUserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(cs => cs.Coach)
                   .WithMany(c => c.Sessions)
                   .HasForeignKey(cs => cs.CoachId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class MembershipBenefitUsageConfiguration : IEntityTypeConfiguration<MembershipBenefitUsage>
    {
        public void Configure(EntityTypeBuilder<MembershipBenefitUsage> builder)
        {
            builder.HasIndex(mbu => new { mbu.UserMembershipId, mbu.BenefitType, mbu.BillingMonth })
                   .IsUnique();
        }
    }
}
