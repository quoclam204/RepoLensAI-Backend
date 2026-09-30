using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleApi.Models;
using SampleApi.Services;

namespace SampleApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public WeatherController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _weatherService.GetForecastsAsync());
    }

    [HttpPost]
    public IActionResult Create([FromBody] WeatherForecast forecast)
    {
        return CreatedAtAction(nameof(GetAll), forecast);
    }
}
