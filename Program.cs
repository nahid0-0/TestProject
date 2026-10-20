using Chess;
using Chess.Api.Auth;
using Chess.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add Chess services
builder.Services.AddChess(builder.Configuration);

// Add Controllers and Exception Handling
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add Authentication and Authorization
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.Scheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlayerOnly", policy => policy.RequireRole("Player"));
    options.AddPolicy("CoachOrAdmin", policy => policy.RequireRole("Coach", "Admin"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
