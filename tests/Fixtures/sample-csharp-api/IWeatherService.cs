namespace SampleApi.Services;

public interface IWeatherService
{
    Task<IEnumerable<SampleApi.Models.WeatherForecast>> GetForecastsAsync();
}
