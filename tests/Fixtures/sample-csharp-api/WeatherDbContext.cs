using Microsoft.EntityFrameworkCore;
using SampleApi.Models;

namespace SampleApi.Data;

public class WeatherDbContext : DbContext
{
    public DbSet<WeatherForecast> Forecasts => Set<WeatherForecast>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherForecast>().HasKey(f => f.Id);
    }
}
