using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;
        private readonly IAuditService _auditService;
        private readonly IEmailQueueService _emailQueueService;

        public AuthController(
            AppDbContext context,
            IAuthService authService,
            IAuditService auditService,
            IEmailQueueService emailQueueService)
        {
            _context = context;
            _authService = authService;
            _auditService = auditService;
            _emailQueueService = emailQueueService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { Message = "Email already in use" });
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = _authService.HashPassword(request.Password),
                Role = Role.Customer
            };

            await _context.Users.AddAsync(user);
            
            await _auditService.LogAsync(
                AuditAction.UserRegistered,
                nameof(User),
                user.Id.ToString(),
                user.Id.ToString(),
                newValue: new { user.Email, user.FullName, user.Role });

            await _context.SaveChangesAsync();

            // Queue welcome email
            await _emailQueueService.EnqueueEmailAsync(
                user.Email,
                "Welcome to CueMasters Billiards!",
                $"<h1>Welcome, {user.FullName}!</h1><p>Thank you for registering at CueMasters. We are excited to have you on board.</p>");

            var token = _authService.GenerateJwtToken(user);
            return Ok(new AuthResponse { Id = user.Id.ToString(), Token = token, Email = user.Email, FullName = user.FullName, Role = user.Role.ToString() });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !_authService.VerifyPassword(request.Password, user.PasswordHash))
            {
                await _auditService.LogAsync(
                    AuditAction.UserLoginFailed,
                    nameof(User),
                    request.Email,
                    notes: $"Failed login attempt for email: {request.Email}");
                
                await _context.SaveChangesAsync();
                return Unauthorized(new { Message = "Invalid email or password" });
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync(
                    AuditAction.UserLoginFailed,
                    nameof(User),
                    user.Id.ToString(),
                    user.Id.ToString(),
                    notes: "Login attempted on inactive account.");

                await _context.SaveChangesAsync();
                return StatusCode(403, new { Message = "User account is inactive" });
            }

            await _auditService.LogAsync(
                AuditAction.UserLogin,
                nameof(User),
                user.Id.ToString(),
                user.Id.ToString());

            await _context.SaveChangesAsync();

            var token = _authService.GenerateJwtToken(user);
            return Ok(new AuthResponse { Id = user.Id.ToString(), Token = token, Email = user.Email, FullName = user.FullName, Role = user.Role.ToString() });
        }
    }
}
