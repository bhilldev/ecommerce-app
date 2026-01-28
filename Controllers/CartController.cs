using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;
using MyEcommerceApp.DTOs;

namespace MyEcommerceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase 
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<CartController> _logger;

    public CartController(EcommerceDbContext context, ILogger<CartController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<CartDto>> GetUserCart(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return BadRequest("Invalid user ID");

        var cart = await _context.ShoppingCarts
            .AsNoTracking()
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .Where(c => c.UserId == userId)
            .Select(c => new CartDto
            {
                Id = c.Id,
                UserId = c.UserId,
                Items = c.CartItems.Select(ci => new CartItemDto
                {
                    Id = ci.Id,
                    ProductId = ci.ProductId,
                    ProductName = ci.Product.Name,
                    ProductImageUrl = ci.Product.ImageUrl,
                    Price = ci.Price,
                    Quantity = ci.Quantity,
                    Subtotal = ci.Price * ci.Quantity
                }).ToList(),
                TotalAmount = c.CartItems.Sum(ci => ci.Price * ci.Quantity)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (cart == null)
        {
            _logger.LogWarning("Cart not found for user {UserId}", userId);
            return NotFound($"Cart not found for user {userId}");
        }

        return Ok(cart);
    }

    [HttpPost("{userId}/items")]
    public async Task<ActionResult<CartItemDto>> AddItemToCart(
        int userId, 
        AddToCartDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return BadRequest("Invalid user ID");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var cart = await _context.ShoppingCarts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            _logger.LogWarning("Cart not found for user {UserId}", userId);
            return NotFound($"Cart not found for user {userId}");
        }

        var product = await _context.Products.FindAsync(new object[] { dto.ProductId }, cancellationToken);
        
        if (product == null || !product.IsActive)
        {
            _logger.LogWarning("Product {ProductId} not found or inactive", dto.ProductId);
            return NotFound($"Product with ID {dto.ProductId} not found");
        }

        if (product.StockQuantity < dto.Quantity)
        {
            return BadRequest($"Insufficient stock. Available: {product.StockQuantity}");
        }

        var existingCartItem = cart.CartItems
            .FirstOrDefault(ci => ci.ProductId == dto.ProductId);

        if (existingCartItem != null)
        {
            existingCartItem.Quantity += dto.Quantity;
            existingCartItem.Price = product.DiscountPrice ?? product.Price;
            _logger.LogInformation("Updated quantity for product {ProductId} in cart {CartId}", dto.ProductId, cart.Id);
        }
        else
        {
            var cartItem = new CartItem
            {
                ShoppingCartId = cart.Id,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                Price = product.DiscountPrice ?? product.Price,
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);
            _logger.LogInformation("Added product {ProductId} to cart {CartId}", dto.ProductId, cart.Id);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var addedItem = await _context.CartItems
            .AsNoTracking()
            .Include(ci => ci.Product)
            .Where(ci => ci.ShoppingCartId == cart.Id && ci.ProductId == dto.ProductId)
            .Select(ci => new CartItemDto
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                ProductName = ci.Product.Name,
                ProductImageUrl = ci.Product.ImageUrl,
                Price = ci.Price,
                Quantity = ci.Quantity,
                Subtotal = ci.Price * ci.Quantity
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(addedItem);
    }

    [HttpPut("items/{cartItemId}")]
    public async Task<IActionResult> UpdateCartItemQuantity(
        int cartItemId, 
        UpdateCartItemDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (cartItemId <= 0)
            return BadRequest("Invalid cart item ID");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.Quantity <= 0)
            return BadRequest("Quantity must be greater than 0");

        var cartItem = await _context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId, cancellationToken);

        if (cartItem == null)
        {
            _logger.LogWarning("Cart item {CartItemId} not found", cartItemId);
            return NotFound($"Cart item with ID {cartItemId} not found");
        }

        if (cartItem.Product.StockQuantity < dto.Quantity)
        {
            return BadRequest($"Insufficient stock. Available: {cartItem.Product.StockQuantity}");
        }

        cartItem.Quantity = dto.Quantity;
        
        var cart = await _context.ShoppingCarts.FindAsync(new object[] { cartItem.ShoppingCartId }, cancellationToken);
        if (cart != null)
        {
            cart.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated cart item {CartItemId} quantity to {Quantity}", cartItemId, dto.Quantity);
        return NoContent();
    }

    [HttpDelete("items/{cartItemId}")]
    public async Task<IActionResult> RemoveItemFromCart(int cartItemId, CancellationToken cancellationToken = default)
    {
        if (cartItemId <= 0)
            return BadRequest("Invalid cart item ID");

        var cartItem = await _context.CartItems.FindAsync(new object[] { cartItemId }, cancellationToken);

        if (cartItem == null)
        {
            _logger.LogWarning("Cart item {CartItemId} not found", cartItemId);
            return NotFound($"Cart item with ID {cartItemId} not found");
        }

        var cart = await _context.ShoppingCarts.FindAsync(new object[] { cartItem.ShoppingCartId }, cancellationToken);
        
        _context.CartItems.Remove(cartItem);
        
        if (cart != null)
        {
            cart.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Removed cart item {CartItemId} from cart", cartItemId);
        return NoContent();
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> ClearCart(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return BadRequest("Invalid user ID");

        var cart = await _context.ShoppingCarts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart == null)
        {
            _logger.LogWarning("Cart not found for user {UserId}", userId);
            return NotFound($"Cart not found for user {userId}");
        }

        _context.CartItems.RemoveRange(cart.CartItems);
        cart.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cleared cart for user {UserId}", userId);
        return NoContent();
    }
}
