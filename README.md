Environmental Variables

Please put this in the appsettings.json
Fill out your own data
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=YOUR_DATABASE_NAME;User Id=YOUR_USERNAME;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Key": "ThisIsASuperLongSecretKeyThatIsAtLeast32Chars",
    "Issuer": "mybackend",
    "Audience": "mybackend-user",
    "ExpireMinutes": 60
  },
  "MyAnimeList": {
    "ClientId": "YOUR_CLIENT_ID"
  },
}

REGARDING KAFKA SERVICE
Kafka service is an OPTIONAL service and may be excluded in the installation.