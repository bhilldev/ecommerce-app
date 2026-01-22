using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;
using MyEcommerceApp.DTOs;

namespace MyEcommerceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase 
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(EcommerceDbContext context, ILogger<ProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAllProducts(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest("Invalid pagination parameters");

        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                Category = p.Category,
                Brand = p.Brand,
                ImageUrl = p.ImageUrl,
                StockQuantity = p.StockQuantity
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} products for page {Page}", products.Count, page);
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProductById(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid product ID");

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.IsActive)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                Category = p.Category,
                Brand = p.Brand,
                ImageUrl = p.ImageUrl,
                StockQuantity = p.StockQuantity
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null)
        {
            _logger.LogWarning("Product with ID {ProductId} not found", id);
            return NotFound($"Product with ID {id} not found");
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct(
        CreateProductDto createDto, 
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = new Product
        {
            Name = createDto.Name,
            Description = createDto.Description,
            Price = createDto.Price,
            DiscountPrice = createDto.DiscountPrice,
            Category = createDto.Category,
            Brand = createDto.Brand,
            ImageUrl = createDto.ImageUrl,
            StockQuantity = createDto.StockQuantity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created product {ProductId}: {ProductName}", product.Id, product.Name);

        var productDto = new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            Category = product.Category,
            Brand = product.Brand,
            ImageUrl = product.ImageUrl,
            StockQuantity = product.StockQuantity
        };

        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, productDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(
        int id, 
        UpdateProductDto updateDto, 
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid product ID");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        
        if (product == null)
        {
            _logger.LogWarning("Attempted to update non-existent product {ProductId}", id);
            return NotFound($"Product with ID {id} not found");
        }

        product.Name = updateDto.Name;
        product.Description = updateDto.Description;
        product.Price = updateDto.Price;
        product.DiscountPrice = updateDto.DiscountPrice;
        product.Category = updateDto.Category;
        product.Brand = updateDto.Brand;
        product.ImageUrl = updateDto.ImageUrl;
        product.StockQuantity = updateDto.StockQuantity;
        product.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated product {ProductId}", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ProductExistsAsync(id, cancellationToken))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid product ID");

        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        
        if (product == null)
        {
            _logger.LogWarning("Attempted to delete non-existent product {ProductId}", id);
            return NotFound($"Product with ID {id} not found");
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft deleted product {ProductId}", id);
        return NoContent();
    }

    private async Task<bool> ProductExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Products.AnyAsync(e => e.Id == id, cancellationToken);
    }
}
