using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class FnBMenuItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public FnBCategory Category { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class FnBOrder
    {
        public Guid Id { get; set; }

        public Guid TableSessionId { get; set; }
        public TableSession? TableSession { get; set; }

        public decimal TotalAmount { get; set; }

        public ICollection<FnBOrderItem> Items { get; set; } = new List<FnBOrderItem>();
    }

    public class FnBOrderItem
    {
        public int Id { get; set; }
        
        public Guid FnBOrderId { get; set; }
        public FnBOrder? FnBOrder { get; set; }

        public int MenuItemId { get; set; }
        public FnBMenuItem? MenuItem { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
