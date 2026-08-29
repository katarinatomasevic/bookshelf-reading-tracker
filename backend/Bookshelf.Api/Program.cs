using System.Text;
using Bookshelf.Api.Middleware;
using Bookshelf.Application;
using Bookshelf.Infrastructure;
using Bookshelf.Infrastructure.Persistence;
using Bookshelf.Infrastructure.Persistence.DemoData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// The demo shelf used for the defence is seeded through this host rather than through a tool of
// its own, so that it hashes passwords with the same call as AuthService and writes through the
// same DbContext and entity configuration as the running application. Kept to a flag because the
// alternative — a fifth .NET project — would make the "four projects" the architecture rests on
// no longer true.
//
//     dotnet run --project backend/Bookshelf.Api -- --seed-demo
//     dotnet run --project backend/Bookshelf.Api -- --seed-demo --refresh
//
// The flags are stripped before the arguments reach the configuration builder: the command-line
// provider reads arguments as key/value pairs and a bare switch is not one.
var seedDemo = args.Contains("--seed-demo");
var refreshDemo = args.Contains("--refresh");

var builder = WebApplication.CreateBuilder(
    args.Where(argument => argument is not ("--seed-demo" or "--refresh")).ToArray());

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

    if (seedDemo)
    {
        await DemoDataSeeder.RunAsync(dbContext, refreshDemo);

        // A seed that wrote inconsistent data is worse than one that failed, because it is only
        // noticed on the screen it was meant to fill. The exit code says which happened.
        return await DemoDataCheck.RunAsync(dbContext) ? 0 : 1;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Reached only when the host shuts down. Present because the demo seed above returns an exit code,
// which makes the entry point one that has to return a value on every path.
return 0;
