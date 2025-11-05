using Cuzdan360Backend.Models;
using System.Threading.Tasks;

namespace Cuzdan360Backend.Repositories;

public interface IUserRepository
{
    Task<User> GetUserByUsernameAsync(string username);
    Task<User> GetUserByEmailAsync(string email);
    Task<User> GetUserByIdAsync(int userId);
    Task<User> GetUserByResetTokenAsync(string token);
    Task<User> GetUserByEmailVerificationTokenAsync(string token);
    Task<User> GetUserByMfaCodeAsync(string email, string otp);
    Task<User> GetUserByRefreshTokenAsync(string refreshToken);
    Task AddUserAsync(User user);
    Task UpdateUserAsync(User user);
}