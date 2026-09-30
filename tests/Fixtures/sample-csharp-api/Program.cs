using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SampleApi.Data;
using SampleApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Database configuration evidence (placeholder connection string, static-analysis only).
builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseNpgsql("Host=localhost;Database=sampledb;Username=sample_user"));

// Authentication evidence (scheme registration only, no real credentials).
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(_ => { });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IWeatherService, WeatherService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
