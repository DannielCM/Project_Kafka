using Confluent.Kafka;
using Microsoft.Extensions.Hosting;

namespace MyAuthenticationBackend.Services;

public class KafkaConsumerService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly ConsumerConfig _config;
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    public KafkaConsumerService(IConfiguration configuration)
    {
        _configuration = configuration;

        _config = new ConsumerConfig
        {
            BootstrapServers = "127.0.0.1:9092",
            GroupId = "login-logger-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting Kafka consumer service...");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTask = Task.Run(() => StartConsumingAsync(_cts.Token));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping Kafka consumer service...");
        _cts?.Cancel();
        if (_consumerTask != null)
            await _consumerTask;
    }

    private async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(_config)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                    Console.WriteLine($"Kafka fatal error: {e.Reason}");
            })
            .Build();

        consumer.Subscribe("user-login");

        Console.WriteLine("Kafka Consumer started. Listening to 'user-login'...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // use a short timeout to avoid blocking indefinitely
                var cr = consumer.Consume(TimeSpan.FromSeconds(1));
                if (cr != null)
                {
                    Console.WriteLine($"Received message: {cr.Message.Value}");
                    await SaveLogAsync(cr.Message.Value);
                }
            }
            catch (ConsumeException ex)
            {
                // only log serious errors
                Console.WriteLine($"Consume error: {ex.Error.Reason}");
            }
            catch (OperationCanceledException)
            {
                // normal cancellation
            }
            catch (Exception ex)
            {
                // catch-all to prevent crash
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }

        consumer.Close();
        Console.WriteLine("Kafka consumer stopped.");
    }

    private async Task SaveLogAsync(string message)
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "kafka_login_logs.txt");
        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";

        await File.AppendAllTextAsync(logFilePath, logEntry);
    }
}
