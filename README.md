# Environment Variables

Please put this in your `appsettings.json` and fill out your own data:

```json
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
  }
}

## Database Schema Setup

The database schema is placed in the `data` folder in the root directory.  
You may use **MySQL Workbench** to import the `.sql` file.

---

## Kafka Service

The Kafka service is **optional** and may be excluded from the installation.