namespace MyEcommerceApp.Models
{
    public class ShoppingCart
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        
        // Computed property
        public decimal TotalAmount => CartItems.Sum(item => item.Price * item.Quantity);
    }

    public class CartItem
    {
        public int Id { get; set; }
        public int ShoppingCartId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Store price at time of adding to cart
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public ShoppingCart ShoppingCart { get; set; } = null!;
        public Product Product { get; set; } = null!;
        
        // Computed property
        public decimal Subtotal => Price * Quantity;
    }
}
