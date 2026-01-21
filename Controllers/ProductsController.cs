using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;

namespace ExpensesApp.Controllers;

[ApiController]
[Route("api/[controller]")]
private readonly EcommerceDbContext _context;
public class ProductsController : ControllerBase 
{
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
    var item = await _context.Products.FindAsync(id);
      if (product == null)
          return NotFound();
      return product;
  }
}
       
