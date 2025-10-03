using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;


namespace APIEndpoints.Endpoints;
public static class APIEndpoints
{
    public static void MapAPIEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api/extapi").WithTags("API");
        
        group.MapGet("/my-anime-list/anime/season/{year}/{season}", [Authorize(Policy = "Require2FAVerified")] async (IConfiguration config, int year, string season, [FromQuery] int limit = 10) =>
        {
            try
            {
                var client_id = config["MyAnimeList:ClientId"];
                var seasonalUrl = $"https://api.myanimelist.net/v2/anime/season/{year}/{season}?limit={limit}";

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-MAL-CLIENT-ID", client_id);

                var response = await httpClient.GetAsync(seasonalUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return Results.Problem($"Error fetching seasonal data: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(json);

                return Results.Json(jsonDoc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return Results.Problem("An unexpected error occurred while fetching data from MyAnimeList.");
            }
        });
    }
}