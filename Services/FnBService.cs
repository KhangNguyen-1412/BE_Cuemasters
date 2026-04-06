using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Services
{
    public interface IFnBService
    {
        Task<List<FnBMenuItemResponse>> GetMenuItemsAsync();
        Task<FnBOrderResponse?> CreateFnBOrderAsync(Guid bookingId, List<int> itemIds);
    }

    public class FnBService : IFnBService
    {
        private readonly AppDbContext _context;

        public FnBService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<FnBMenuItemResponse>> GetMenuItemsAsync()
        {
             return await _context.FnBMenuItems
                .Where(f => f.IsAvailable)
                .Select(f => new FnBMenuItemResponse
                {
                    Id = f.Id,
                    Name = f.Name,
                    Description = "",
                    Price = f.Price,
                    Category = f.Category.ToString(),
                    ImageUrl = f.ImageUrl,
                    IsAvailable = f.IsAvailable
                })
                .ToListAsync();
        }

        public async Task<FnBOrderResponse?> CreateFnBOrderAsync(Guid bookingId, List<int> itemIds)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return null;

            var fnbOrder = new FnBOrder
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                TotalAmount = 0
            };

            decimal totalAmount = 0;
            _context.FnBOrders.Add(fnbOrder);
            await _context.SaveChangesAsync();

            // Group by item to create quantities
            var groupedItems = itemIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in groupedItems)
            {
                var menuItem = await _context.FnBMenuItems.FindAsync(kvp.Key);
                if (menuItem != null && menuItem.IsAvailable)
                {
                    var itemTotal = menuItem.Price * kvp.Value;
                    totalAmount += itemTotal;

                    _context.FnBOrderItems.Add(new FnBOrderItem
                    {
                        FnBOrderId = fnbOrder.Id,
                        MenuItemId = kvp.Key,
                        Quantity = kvp.Value,
                        UnitPrice = menuItem.Price
                    });
                }
            }

            fnbOrder.TotalAmount = totalAmount;
            booking.TotalTableCost += totalAmount; // Update booking grand total
            
            await _context.SaveChangesAsync();

            return new FnBOrderResponse
            {
                Id = fnbOrder.Id.ToString(),
                TotalPrice = fnbOrder.TotalAmount,
                Status = "Pending"
            };
        }
    }
}