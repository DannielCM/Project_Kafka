using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace APIEndpoints.Endpoints;
public static class APIEndpoints
{
    public static void MapAPIEndpoints(this WebApplication server)
    {
        var group = server.MapGroup("/api").WithTags("API");
        
        group.MapGet("/my-anime-list/anime/season/{year}/{season}", [Authorize] async (IConfiguration config, int year, string season, [FromQuery] int limit = 10) =>
        {
            Console.WriteLine("Hello, world!");

            try
            {
                var client_id = config["MyAnimeList:ClientId"];

                var url = $"https://api.myanimelist.net/v2/anime/season/{year}/{season}?limit={limit}";


                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-MAL-CLIENT-ID", client_id);

                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return Results.Problem($"Error fetching data: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();

                return Results.Json(json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return Results.Problem("An unexpected error occurred while fetching data from MyAnimeList.");
            }
        });
    }
}