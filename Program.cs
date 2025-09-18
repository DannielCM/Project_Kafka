using AuthenticationBackend.Endpoints;
using BackendAuthentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// environment variables
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (connStr == null)
{
    throw new Exception("Connection string 'DefaultConnection' not found.");
}

// builder configs
builder.Services.AddSingleton(new DbHelper(connStr));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
            )
        };
    });
builder.Services.AddAuthorization();

var server = builder.Build();

// routes
server.MapGet("/", (string? user = "user") => // default route
{
    return Results.Json(new { message = $"Hello {user} from MyAuthenticationBackend!" }, statusCode: 200);
});
server.MapGet("/protected-endpoint", [Authorize(Roles = "admin")] (HttpContext context) =>
{
    var user = context.User;

    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    var role = user.FindFirst(ClaimTypes.Role)?.Value;

    return Results.Json(new { message = $"Hello user {userId} with role {role} from MyAuthenticationBackend!" }, statusCode: 200);
});
server.MapAuthEndpoints();

server.Run();