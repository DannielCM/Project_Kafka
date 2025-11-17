using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using APIEndpoints.Endpoints;
using MyAuthenticationBackend.Models;

namespace MyAuthenticationBackend.Services;
public class EmailService
{
    private readonly SmtpSettings _config;

    public EmailService(IOptions<SmtpSettings> settings)
    {
        _config = settings.Value;
        Console.WriteLine($"SMTP Config - Host: {_config.Host}, Port: {_config.Port}, Username: {_config.Username}");
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("My App", _config.Username));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_config.Host, _config.Port, MailKit.Security.SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_config.Username, _config.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}