using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var context = new AppDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<AppDbContext>>());

            // Auto-apply migrations
            await context.Database.MigrateAsync();

            // Check if db is already seeded
            if (await context.Users.AnyAsync()) return;

            // 1. Create Admin & Staff Users
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@cuemasters.com",
                FullName = "System Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = Role.Admin,
            };
            context.Users.Add(adminUser);

            var staffUsers = new List<User>
            {
                new User { Id = Guid.NewGuid(), Email = "coach1@cuemasters.com", FullName = "Efren Reyes", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff@123"), Role = Role.Staff },
                new User { Id = Guid.NewGuid(), Email = "coach2@cuemasters.com", FullName = "Ronnie O'Sullivan", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff@123"), Role = Role.Staff },
                new User { Id = Guid.NewGuid(), Email = "coach3@cuemasters.com", FullName = "Shane Van Boening", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff@123"), Role = Role.Staff }
            };
            context.Users.AddRange(staffUsers);

            // 1.5 Create Coaches
            var coaches = new List<Coach>
            {
                new Coach { Id = Guid.NewGuid(), UserId = staffUsers[0].Id, Bio = "Legendary pocket billiards player.", HourlyRate = 500000, Specialty = Specialty.Pool, IsActive = true, Rating = 5, TotalSessions = 100 },
                new Coach { Id = Guid.NewGuid(), UserId = staffUsers[1].Id, Bio = "The Rocket - Snooker genius.", HourlyRate = 600000, Specialty = Specialty.Snooker, IsActive = true, Rating = 5, TotalSessions = 150 },
                new Coach { Id = Guid.NewGuid(), UserId = staffUsers[2].Id, Bio = "Multi-time US Open champion.", HourlyRate = 450000, Specialty = Specialty.Pool, IsActive = true, Rating = 4.8m, TotalSessions = 80 }
            };
            context.Coaches.AddRange(coaches);

            // 1.6 Create Coach Availability
            var availabilities = new List<CoachAvailability>();
            foreach(var coach in coaches)
            {
                for(int i = 0; i < 7; i++)
                {
                    availabilities.Add(new CoachAvailability
                    {
                        CoachId = coach.Id,
                        DayOfWeek = (DayOfWeek)i,
                        StartTime = new TimeSpan(9, 0, 0),
                        EndTime = new TimeSpan(17, 0, 0),
                        IsBlocked = false
                    });
                }
            }
            context.CoachAvailabilities.AddRange(availabilities);

            // 2. Create Membership Plans
            var plans = new List<MembershipPlan>
            {
                new MembershipPlan { Tier = MembershipTier.Free, Name = "Basic Player", MonthlyPrice = 0, TableDiscountPercent = 0, PriorityBooking = false, FreeCoachingSessionsPerMonth = 0, MaxAdvanceBookingDays = 0 },
                new MembershipPlan { Tier = MembershipTier.Silver, Name = "Cue Enthusiast", MonthlyPrice = 200000, TableDiscountPercent = 10, PriorityBooking = true, FreeCoachingSessionsPerMonth = 1, MaxAdvanceBookingDays = 1 },
                new MembershipPlan { Tier = MembershipTier.Gold, Name = "Master Club", MonthlyPrice = 500000, TableDiscountPercent = 20, PriorityBooking = true, FreeCoachingSessionsPerMonth = 3, MaxAdvanceBookingDays = 3 }
            };
            context.MembershipPlans.AddRange(plans);

            // 3. Create Tables — enough per category to make the walk-in buffer meaningful.
            //    Buffer constant (BookingService.WalkInBufferPerCategory = 2) reserves 2 tables per
            //    category for walk-ins, so online concurrent reservations are capped at (total - 2).
            //    Pool:    10 tables → 8 concurrent online per 30-min slot
            //    Snooker:  6 tables → 4 concurrent online per 30-min slot
            //    Carom:    4 tables → 2 concurrent online per 30-min slot
            var tables = new List<BilliardTable>();
            for (int p = 1; p <= 10; p++)
            {
                tables.Add(new BilliardTable
                {
                    TableNumber = $"P{p:D2}",
                    Type = TableType.Pool,
                    HourlyRate = 80000,
                    PositionX = 20 + (p - 1) % 5 * 20,
                    PositionY = 20 + (p - 1) / 5 * 30
                });
            }
            for (int s = 1; s <= 6; s++)
            {
                tables.Add(new BilliardTable
                {
                    TableNumber = $"S{s:D2}",
                    Type = TableType.Snooker,
                    HourlyRate = 100000,
                    PositionX = 20 + (s - 1) % 3 * 30,
                    PositionY = 90 + (s - 1) / 3 * 30
                });
            }
            for (int c = 1; c <= 4; c++)
            {
                tables.Add(new BilliardTable
                {
                    TableNumber = $"C{c:D2}",
                    Type = TableType.Carom,
                    HourlyRate = 120000,
                    PositionX = 20 + (c - 1) % 2 * 40,
                    PositionY = 160 + (c - 1) / 2 * 30
                });
            }
            context.Tables.AddRange(tables);

            // 4. Create Food and Beverages
            var menuItems = new List<FnBMenuItem>
            {
                new FnBMenuItem { Name = "Cà phê đen đá", Category = FnBCategory.Drinks, Price = 35000 },
                new FnBMenuItem { Name = "Khoai tây chiên", Category = FnBCategory.Snacks, Price = 50000 },
                new FnBMenuItem { Name = "Bò lúc lắc", Category = FnBCategory.MainCourse, Price = 150000 },
                new FnBMenuItem { Name = "Precision Signature", Category = FnBCategory.Combos, Price = 120000 } // using combos for cocktail to match category
            };
            context.FnBMenuItems.AddRange(menuItems);

            await context.SaveChangesAsync();
        }
    }
}
