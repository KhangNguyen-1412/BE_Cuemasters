using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class FnBController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FnBController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AdminFnBMenuItemResponse>>> GetAllItems()
        {
            var items = await _context.FnBMenuItems
                .OrderByDescending(item => item.IsAvailable)
                .ThenBy(item => item.Name)
                .Select(item => new AdminFnBMenuItemResponse
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category.ToString(),
                    Price = item.Price,
                    ImageUrl = item.ImageUrl,
                    IsAvailable = item.IsAvailable,
                    OrderCount = _context.FnBOrderItems.Count(orderItem => orderItem.MenuItemId == item.Id)
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AdminFnBMenuItemResponse>> GetItemById(int id)
        {
            var item = await _context.FnBMenuItems
                .Where(menuItem => menuItem.Id == id)
                .Select(menuItem => new AdminFnBMenuItemResponse
                {
                    Id = menuItem.Id,
                    Name = menuItem.Name,
                    Category = menuItem.Category.ToString(),
                    Price = menuItem.Price,
                    ImageUrl = menuItem.ImageUrl,
                    IsAvailable = menuItem.IsAvailable,
                    OrderCount = _context.FnBOrderItems.Count(orderItem => orderItem.MenuItemId == menuItem.Id)
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return NotFound(new { Message = "F&B item not found." });
            }

            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<AdminFnBMenuItemResponse>> CreateItem([FromBody] AdminUpsertFnBMenuItemRequest request)
        {
            if (!TryParseItemRequest(request, out var category, out var errorResult))
            {
                return errorResult!;
            }

            var item = new FnBMenuItem
            {
                Name = request.Name.Trim(),
                Category = category,
                Price = request.Price,
                ImageUrl = request.ImageUrl,
                IsAvailable = request.IsAvailable
            };

            _context.FnBMenuItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetItemById), new { id = item.Id }, MapItem(item, 0));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AdminFnBMenuItemResponse>> UpdateItem(int id, [FromBody] AdminUpsertFnBMenuItemRequest request)
        {
            if (!TryParseItemRequest(request, out var category, out var errorResult))
            {
                return errorResult!;
            }

            var item = await _context.FnBMenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { Message = "F&B item not found." });
            }

            item.Name = request.Name.Trim();
            item.Category = category;
            item.Price = request.Price;
            item.ImageUrl = request.ImageUrl;
            item.IsAvailable = request.IsAvailable;

            await _context.SaveChangesAsync();

            var orderCount = await _context.FnBOrderItems.CountAsync(orderItem => orderItem.MenuItemId == id);
            return Ok(MapItem(item, orderCount));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.FnBMenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { Message = "F&B item not found." });
            }

            var hasOrderHistory = await _context.FnBOrderItems.AnyAsync(orderItem => orderItem.MenuItemId == id);

            if (hasOrderHistory)
            {
                item.IsAvailable = false;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "F&B item archived because it already appears in past orders." });
            }

            _context.FnBMenuItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static AdminFnBMenuItemResponse MapItem(FnBMenuItem item, int orderCount)
        {
            return new AdminFnBMenuItemResponse
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category.ToString(),
                Price = item.Price,
                ImageUrl = item.ImageUrl,
                IsAvailable = item.IsAvailable,
                OrderCount = orderCount
            };
        }

        private static bool TryParseItemRequest(
            AdminUpsertFnBMenuItemRequest request,
            out FnBCategory category,
            out ActionResult? errorResult)
        {
            errorResult = null;
            category = default;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Item name is required." });
                return false;
            }

            if (request.Price < 0)
            {
                errorResult = new BadRequestObjectResult(new { Message = "Item price must be zero or greater." });
                return false;
            }

            if (!Enum.TryParse(request.Category, true, out category))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Invalid F&B category." });
                return false;
            }

            return true;
        }
    }
}
