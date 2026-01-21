using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;

namespace ExpensesApp.Controllers;

[ApiController]
[Route("api/[controller]")]
private readonly EcommerceDbContext _context;
public class ProductsController : ControllerBase {
  public ProductsController(EcommerceDbContext context)
  {
      _context = context;
  }
}
       
