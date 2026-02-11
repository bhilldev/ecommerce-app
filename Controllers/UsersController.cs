using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid user ID");

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == id && u.IsActive)
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

        if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.IsActive, cancellationToken))
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
            IsActive = true,
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
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
        int id, 
        UpdateUserDto dto, 
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid user ID");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Attempted to update non-existent or inactive user {UserId}", id);
            return NotFound($"User with ID {id} not found");
        }

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber ?? string.Empty;
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated user {UserId}", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UserExistsAsync(id, cancellationToken))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid user ID");

        var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        
        if (user == null)
        {
            _logger.LogWarning("Attempted to delete non-existent user {UserId}", id);
            return NotFound($"User with ID {id} not found");
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} soft deleted (marked as inactive)", id);
        return NoContent();
    }

    private async Task<bool> UserExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Id == id && u.IsActive, cancellationToken);
    }
}
