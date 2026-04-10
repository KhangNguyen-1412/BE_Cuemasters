using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Data.Configurations
{
    public class CoachingSessionConfiguration : IEntityTypeConfiguration<CoachingSession>
    {
        public void Configure(EntityTypeBuilder<CoachingSession> builder)
        {
            // Prevent two active sessions for the same coach at the same start time.
            // Filter on CancelledAt (null = active) since sessions are no longer auto-completed at creation.
            builder.HasIndex(cs => new { cs.CoachId, cs.SessionDate, cs.StartTime })
                   .IsUnique()
                   .HasFilter("[CancelledAt] IS NULL");

            builder.HasOne(cs => cs.StudentUser)
                   .WithMany()
                   .HasForeignKey(cs => cs.StudentUserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(cs => cs.Coach)
                   .WithMany(c => c.Sessions)
                   .HasForeignKey(cs => cs.CoachId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(cs => cs.TableSession)
                   .WithMany(ts => ts.CoachingSessions)
                   .HasForeignKey(cs => cs.TableSessionId)
                   .OnDelete(DeleteBehavior.SetNull)
                   .IsRequired(false);
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
