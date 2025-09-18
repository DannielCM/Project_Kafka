using AuthenticationBackend.Endpoints;
using BackendAuthentication;

var builder = WebApplication.CreateBuilder(args);

// environment variables
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (connStr == null)
{
    throw new Exception("Connection string 'DefaultConnection' not found.");
}

// builder configs
builder.Services.AddSingleton(new DbHelper(connStr));

var server = builder.Build();

// routes
server.MapGet("/", (string? user = "user") => // default route
{
    return Results.Json(new { message = $"Hello {user} from MyAuthenticationBackend!" }, statusCode: 200);
});
server.MapAuthEndpoints();

server.Run();