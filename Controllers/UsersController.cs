using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyEcommerceApp.Data;
using MyEcommerceApp.Models;
using MyEcommerceApp.DTOs;

namespace MyEcommerceApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase 
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(EcommerceDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid user ID");

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found", id);
            return NotFound($"User with ID {id} not found");
        }

        return Ok(user);
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(
        RegisterUserDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (await _context.Users.AnyAsync(u => u.Email == dto.Email, cancellationToken))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", dto.Email);
            return BadRequest("Email is already registered");
        }

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = dto.Password,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var cart = new ShoppingCart
        {
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.ShoppingCarts.Add(cart);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New user registered: {UserId} - {Email}", user.Id, user.Email);

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber
        };

        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, userDto);
    }
}
