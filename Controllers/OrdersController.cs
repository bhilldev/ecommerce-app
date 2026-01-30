using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;
using MyEcommerceApp.DTOs;

namespace MyEcommerceApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase 
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(EcommerceDbContext context, ILogger<OrdersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        CreateOrderDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FindAsync(new object[] { dto.UserId }, cancellationToken);
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("User {UserId} not found or inactive", dto.UserId);
            return NotFound($"User with ID {dto.UserId} not found");
        }

        var cart = await _context.ShoppingCarts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.UserId == dto.UserId, cancellationToken);

        if (cart == null || !cart.CartItems.Any())
        {
            _logger.LogWarning("Cart is empty for user {UserId}", dto.UserId);
            return BadRequest("Cart is empty. Cannot create order.");
        }

        foreach (var cartItem in cart.CartItems)
        {
            if (cartItem.Product.StockQuantity < cartItem.Quantity)
            {
                return BadRequest($"Insufficient stock for {cartItem.Product.Name}. Available: {cartItem.Product.StockQuantity}");
            }
        }

        var order = new Order
        {
            UserId = dto.UserId,
            OrderNumber = GenerateOrderNumber(),
            TotalAmount = cart.CartItems.Sum(ci => ci.Price * ci.Quantity),
            Status = OrderStatus.Pending,
            OrderDate = DateTime.UtcNow,
            ShippingStreet = dto.ShippingStreet,
            ShippingCity = dto.ShippingCity,
            ShippingState = dto.ShippingState,
            ShippingZipCode = dto.ShippingZipCode,
            ShippingCountry = dto.ShippingCountry
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var cartItem in cart.CartItems)
        {
            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = cartItem.ProductId,
                Quantity = cartItem.Quantity,
                Price = cartItem.Price,
                Subtotal = cartItem.Price * cartItem.Quantity
            };

            _context.OrderItems.Add(orderItem);

            cartItem.Product.StockQuantity -= cartItem.Quantity;
        }

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Method = dto.PaymentMethod,
            Status = PaymentStatus.Completed,
            TransactionId = "FAKE_" + Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(),
            CardLastFourDigits = dto.CardLastFourDigits,
            CardBrand = dto.CardBrand,
            PaymentDate = DateTime.UtcNow
        };

        _context.Payments.Add(payment);

        _context.CartItems.RemoveRange(cart.CartItems);
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderNumber} created for user {UserId}", order.OrderNumber, dto.UserId);

        var orderDto = await GetOrderDtoById(order.Id, cancellationToken);
        return CreatedAtAction(nameof(GetOrderById), new { orderId = order.Id }, orderDto);
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int orderId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            return BadRequest("Invalid order ID");

        var orderDto = await GetOrderDtoById(orderId, cancellationToken);

        if (orderDto == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return NotFound($"Order with ID {orderId} not found");
        }

        return Ok(orderDto);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetUserOrders(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return BadRequest("Invalid user ID");

        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Payment)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                UserId = o.UserId,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                OrderDate = o.OrderDate,
                ShippedDate = o.ShippedDate,
                DeliveredDate = o.DeliveredDate,
                ShippingAddress = $"{o.ShippingStreet}, {o.ShippingCity}, {o.ShippingState} {o.ShippingZipCode}, {o.ShippingCountry}",
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductImageUrl = oi.Product.ImageUrl,
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    Subtotal = oi.Subtotal
                }).ToList(),
                Payment = o.Payment != null ? new PaymentDto
                {
                    Id = o.Payment.Id,
                    Amount = o.Payment.Amount,
                    Method = o.Payment.Method.ToString(),
                    Status = o.Payment.Status.ToString(),
                    TransactionId = o.Payment.TransactionId,
                    CardLastFourDigits = o.Payment.CardLastFourDigits,
                    PaymentDate = o.Payment.PaymentDate
                } : null
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} orders for user {UserId}", orders.Count, userId);
        return Ok(orders);
    }

    [HttpPut("{orderId}/status")]
    public async Task<IActionResult> UpdateOrderStatus(
        int orderId,
        UpdateOrderStatusDto dto,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            return BadRequest("Invalid order ID");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = await _context.Orders.FindAsync(new object[] { orderId }, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return NotFound($"Order with ID {orderId} not found");
        }

        order.Status = dto.Status;

        if (dto.Status == OrderStatus.Shipped && !order.ShippedDate.HasValue)
        {
            order.ShippedDate = DateTime.UtcNow;
        }

        if (dto.Status == OrderStatus.Delivered && !order.DeliveredDate.HasValue)
        {
            order.DeliveredDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, dto.Status);
        return NoContent();
    }

    [HttpDelete("{orderId}")]
    public async Task<IActionResult> CancelOrder(int orderId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            return BadRequest("Invalid order ID");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return NotFound($"Order with ID {orderId} not found");
        }

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
        {
            return BadRequest("Cannot cancel order that has been shipped or delivered");
        }

        foreach (var orderItem in order.OrderItems)
        {
            orderItem.Product.StockQuantity += orderItem.Quantity;
        }

        order.Status = OrderStatus.Cancelled;

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);
        if (payment != null)
        {
            payment.Status = PaymentStatus.Refunded;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} cancelled and stock restored", orderId);
        return NoContent();
    }

    private string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
    }

    private async Task<OrderDto?> GetOrderDtoById(int orderId, CancellationToken cancellationToken)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.Payment)
            .Where(o => o.Id == orderId)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                UserId = o.UserId,
                TotalAmount = o.TotalAmount,
                Status = o.Status.ToString(),
                OrderDate = o.OrderDate,
                ShippedDate = o.ShippedDate,
                DeliveredDate = o.DeliveredDate,
                ShippingAddress = $"{o.ShippingStreet}, {o.ShippingCity}, {o.ShippingState} {o.ShippingZipCode}, {o.ShippingCountry}",
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductImageUrl = oi.Product.ImageUrl,
                    Quantity = oi.Quantity,
                    Price = oi.Price,
                    Subtotal = oi.Subtotal
                }).ToList(),
                Payment = o.Payment != null ? new PaymentDto
                {
                    Id = o.Payment.Id,
                    Amount = o.Payment.Amount,
                    Method = o.Payment.Method.ToString(),
                    Status = o.Payment.Status.ToString(),
                    TransactionId = o.Payment.TransactionId,
                    CardLastFourDigits = o.Payment.CardLastFourDigits,
                    PaymentDate = o.Payment.PaymentDate
                } : null
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
