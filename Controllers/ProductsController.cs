using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;

namespace MyEcommerceApp.Controllers; // Changed from ExpensesApp

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase 
{
    private readonly EcommerceDbContext _context; // Moved inside the class

    public ProductsController(EcommerceDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetAllProducts()
    {
        var products = await _context.Products.ToListAsync();
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProductById(int id)
    {
        var product = await _context.Products.FindAsync(id); // Changed 'item' to 'product'
        if (product == null)
            return NotFound();
        return Ok(product); // Added Ok()
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduct(int id, Product product)
    {
        if (id != product.Id) // Changed 'blog' to 'product'
            return BadRequest();

        _context.Entry(product).State = EntityState.Modified; // Changed 'blog' to 'product'

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ProductExists(id)) // Changed method name
                return NotFound();
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id); // Changed 'blog' to 'product' and fixed table name
        if (product == null)
            return NotFound();

        _context.Products.Remove(product); // Changed 'Product' to 'Products' and 'blog' to 'product'
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private bool ProductExists(int id)
    {
        return _context.Products.Any(e => e.Id == id); // Changed 'Product' to 'Products'
    }
}
