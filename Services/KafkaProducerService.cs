using Confluent.Kafka;

namespace MyAuthenticationBackend.AppServices;
public class KafkaProducerService
{
    private readonly IProducer<Null, string> _producer;

    public KafkaProducerService(IProducer<Null, string> producer)
    {
        _producer = producer;
    }

    public void SendLoginMessage(int userId)
    {
        var message = new Message<Null, string> { Value = $"user {userId} has logged in!" };
        _producer.Produce("test-topic", message, deliveryReport =>
        {
            if (deliveryReport.Error.IsError)
                Console.Error.WriteLine($"Kafka delivery failed: {deliveryReport.Error.Reason}");
            else
                Console.WriteLine($"Kafka message delivered to {deliveryReport.TopicPartitionOffset}");
        });
    }
}
