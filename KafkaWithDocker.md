# Kafka Quick Start via (Docker)

This guide shows how to quickly test Kafka in Docker: creating a topic, sending, and receiving messages.  

**Prerequisites:** Docker and Docker Compose installed, Kafka + ZooKeeper running in Docker.  

**Steps:**  

1. Enter Kafka container:  
```bash
docker exec -it kafka bash

Create a topic:
kafka-topics --create --topic test-topic --bootstrap-server localhost:9092 --partitions 1 --replication-factor 1
kafka-topics --list --bootstrap-server localhost:9092
# Should display: test-topic

Produce a message:
kafka-console-producer --broker-list localhost:9092 --topic test-topic
# Type a message, e.g. "Hello Kafka!" and press Enter
# Press Ctrl+C to exit

Consume messages:
kafka-console-consumer --bootstrap-server localhost:9092 --topic test-topic --from-beginning
# Messages sent will appear
# Press Ctrl+C to stop
Notes: Replace test-topic with your topic name if needed. All commands are run inside the Kafka container. Assumes Kafka is available on localhost:9092 inside the container.