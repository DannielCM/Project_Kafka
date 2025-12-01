using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using Microsoft.Extensions.Caching.Memory;
using MyAuthenticationBackend.Models;

namespace MyAuthenticationBackend.Services;

public class CaptchaServices
{
    private readonly IMemoryCache cache;
    private const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public CaptchaServices(IMemoryCache memoryCache)
    {
        cache = memoryCache;
    }

    public string GenerateCode(int length = 5)
    {
        var rnd = new Random();
        return new string(Enumerable
            .Repeat(chars, length)
            .Select(x => x[rnd.Next(x.Length)])
            .ToArray());
    }

    public byte[] GenerateImage(string code, int height = 60, int width = 200, int fontSize = 30)
    {
        using var image = new Image<Rgba32>(width, height);

        image.Mutate(ctx =>
        {
            ctx.Fill(Color.White);

            var font = SystemFonts.CreateFont("Arial", fontSize);
            ctx.DrawText(code, font, Color.Black, new PointF(20, 10));

            // ---- FIXED: Draw lines using PathBuilder + ctx.Draw() ----
            var rnd = new Random();
            var pen = Pens.Solid(Color.Gray, 1);

            for (int i = 0; i < 5; i++)
            {
                var p1 = new PointF(rnd.Next(width), rnd.Next(height));
                var p2 = new PointF(rnd.Next(width), rnd.Next(height));

                var path = new PathBuilder()
                    .AddLine(p1, p2)
                    .Build();

                ctx.Draw(pen, path);
            }
            // ----------------------------------------------------------
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public (string captchaId, byte[] imageBytes) CreateCaptcha()
    {
        string code = GenerateCode();
        byte[] image = GenerateImage(code);

        string captchaId = Guid.NewGuid().ToString();
        cache.Set(captchaId, code, TimeSpan.FromMinutes(5));

        return (captchaId, image);
    }

    public CaptchaValidationResult ValidateCaptcha(string captchaId, string userInput)
    {
        captchaId = captchaId?.Trim() ?? "";
        userInput = userInput?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(userInput))
        {
            return new CaptchaValidationResult
            {
                Success = false,
                Message = "captchaId and captchaValue cannot be empty!"
            };
        }

        if (!cache.TryGetValue(captchaId, out string? storedText) ||
            storedText == null ||
            !storedText.Equals(userInput, StringComparison.OrdinalIgnoreCase))
        {
            return new CaptchaValidationResult
            {
                Success = false,
                Message = "Invalid captcha."
            };
        }

        return new CaptchaValidationResult
        {
            Success = true,
            Message = "Captcha validated successfully."
        };
    }
}
