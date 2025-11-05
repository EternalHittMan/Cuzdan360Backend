using Microsoft.AspNetCore.Mvc;
using Cuzdan360Backend.Models;
using Microsoft.AspNetCore.Authorization;

namespace Cuzdan360Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[ResponseCache(Duration = 60)] // 60 saniye boyunca cache'le
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "NY", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Lviv"
    };

    [HttpGet]
    [Authorize] // Sadece kimlik doğrulaması yapılmış kullanıcılar erişebilir
    public IActionResult Get()
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    Summaries[Random.Shared.Next(Summaries.Length)]
                ))
            .ToArray();

        return Ok(forecast);
    }
}