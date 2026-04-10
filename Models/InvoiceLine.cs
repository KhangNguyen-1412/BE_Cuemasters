using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class InvoiceLine
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public InvoiceLineType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Total { get; set; }
    }
}
