namespace BilliardsBooking.API.DTOs
{
    public class FnBMenuItemResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty; // Drink, Food, Snack
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; }
    }
}