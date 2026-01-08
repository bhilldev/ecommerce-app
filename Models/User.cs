namespace MyEcommerceApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ShoppingCart? ShoppingCart { get; set; }
        public ICollection<Address> Addresses { get; set; } = new List<Address>();
    }

    public class Address
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        
        // Navigation property
        public User User { get; set; } = null!;
    }
}
