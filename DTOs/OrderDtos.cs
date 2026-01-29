namespace MyEcommerceApp.DTOs;

public class CreateOrderDto
{
    public int UserId { get; set; }
    public string ShippingStreet { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string ShippingZipCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? CardLastFourDigits { get; set; }
    public string? CardBrand { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public PaymentDto? Payment { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Subtotal { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? CardLastFourDigits { get; set; }
    public DateTime PaymentDate { get; set; }
}

public class UpdateOrderStatusDto
{
    public OrderStatus Status { get; set; }
}
