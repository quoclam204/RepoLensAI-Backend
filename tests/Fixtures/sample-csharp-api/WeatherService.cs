using SampleApi.Models;

namespace SampleApi.Services;

public class WeatherService : IWeatherService
{
    public Task<IEnumerable<WeatherForecast>> GetForecastsAsync()
    {
        return Task.FromResult<IEnumerable<WeatherForecast>>([]);
    }
}
