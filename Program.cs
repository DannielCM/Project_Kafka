using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using AuthenticationBackend.Endpoints;
using APIEndpoints.Endpoints;
using BackendAuthentication;
using CaptchaGen.NetCore;
using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MyAuthenticationBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Get connection string
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (connStr == null) throw new Exception("Connection string 'DefaultConnection' not found.");

// Register services
builder.Services.AddSingleton(new DbHelper(connStr));
builder.Services.AddSingleton<JwtServices>();
builder.Services.AddSingleton<UserServices>();
builder.Services.AddSingleton<CaptchaServices>();

// Kafka services
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddSingleton<IProducer<Null, string>>(sp =>
{
    var config = new ProducerConfig { BootstrapServers = "localhost:9092" };
    return new ProducerBuilder<Null, string>(config).Build();
});
builder.Services.AddHostedService<KafkaConsumerService>();

builder.Services.AddScoped<AuthenticationServices>();
builder.Services.AddMemoryCache();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Authentication & Authorization
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"message\":\"You are not authorized\"}");
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"message\":\"2FA requirement not satisfied\"}");
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapGet("/", (string? user = "user") =>
    Results.Json(new { message = $"Hello {user} from MyAuthenticationBackend!" }, statusCode: 200)
);

app.MapGet("/protected-endpoint", [Authorize(Roles = "admin")] (HttpContext context) =>
{
    var user = context.User;
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var role = user.FindFirst(ClaimTypes.Role)?.Value;

    return Results.Json(new { message = $"Hello user {userId} with role {role} from MyAuthenticationBackend!" }, statusCode: 200);
});

// Map endpoints
app.MapAuthEndpoints();
app.MapAPIEndpoints();
app.MapUserEndpoints();

// Run the app
app.Run();
