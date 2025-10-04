using Microsoft.Extensions.Caching.Memory;
using MyAuthenticationBackend.Models;

namespace MyAuthenticationBackend.Services;
public class CaptchaServices
{
    private readonly IMemoryCache cache;
    public CaptchaServices(IMemoryCache memoryCache)
    {
        cache = memoryCache;
    }

    public CaptchaValidationResult ValidateCaptcha(string captchaId, string userInput)
    {
        captchaId = captchaId?.Trim() ?? "";
        userInput = userInput?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(userInput))
        {
            return new CaptchaValidationResult { Success = false, Message = "captchaId and captchaValue cannot be empty!" };
        }

        if (!cache.TryGetValue(captchaId, out string? storedText) || storedText == null || !storedText.Equals(userInput, StringComparison.OrdinalIgnoreCase))
        {
            return new CaptchaValidationResult { Success = false, Message = "Invalid captcha." };
        }

        return new CaptchaValidationResult { Success = true, Message = "Captcha validated successfully." };
    }
}