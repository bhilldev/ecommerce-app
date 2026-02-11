using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;

namespace ExpensesApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase 
{
  private readonly EcommerceDbContext _context;
  public PaymentController(EcommerceDbContext context)
  {
      _context = context;
  }
}
