namespace APIEndpoints.Endpoints;
public class SeasonalAnimeRequest
{
    public int Year { get; set; }
    public string Season { get; set; } = null!;
    public int Limit { get; set; } = 10;
}