using Cuzdan360Backend.Data;
using Cuzdan360Backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Cuzdan360Backend.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public UserRepository(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<User> GetUserByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        var cacheKey = $"User_{userId}";
        if (!_cache.TryGetValue(cacheKey, out User user))
        {
            user = await _context.Users.FindAsync(userId);
            _cache.Set(cacheKey, user, TimeSpan.FromMinutes(10)); // 10 dakika cache'de tut
        }


        return user;
    }

    public async Task<User> GetUserByResetTokenAsync(string token)
    {
        return await _context.Users.FirstOrDefaultAsync(u =>
            u.ResetToken == token && u.ResetTokenExpiry > DateTime.UtcNow);
    }

    public async Task<User> GetUserByEmailVerificationTokenAsync(string token)
    {
        return await _context.Users.FirstOrDefaultAsync(u =>
            u.EmailVerificationToken == token && u.EmailVerificationTokenExpiry > DateTime.UtcNow);
    }

    public async Task<User> GetUserByMfaCodeAsync(string email, string otp)
    {
        return await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == email && u.MfaCode == otp && u.MfaCodeExpiry > DateTime.UtcNow);
    }

    public async Task<User> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return await _context.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshToken && u.RefreshTokenExpiry > DateTime.UtcNow);
    }

    public async Task AddUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        _cache.Remove($"User_{user.Id}"); // Cache'i temizle
    }
}