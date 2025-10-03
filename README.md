## DO THIS BEFORE RUNNING THE APPLICATION

dotnet restore

# Environment Variables

Please put this in your `appsettings.Development.json` and fill out your own data:
CREATE A FILE NAMED `appsettings.Development.json` IN THE ROOT DIRECTORY for local development if you dont have one yet. Avoid writing directly in appsettings.json.

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
  "Auth": {
    "TwoFactorRedirectUrl": "/verify-2fa"
  },
  "MyAnimeList": {
    "ClientId": "YOUR_CLIENT_ID"
  }
}
```

## MyAnimeList Client ID

To get authorization id from MAL, you need to create an application on their [developer portal](https://myanimelist.net/apiconfig).
Register your application and obtain the Client ID, then replace `YOUR_CLIENT_ID` in the configuration above.

## Database Schema Setup

The database schema is placed in the `data` folder in the root directory.  
You may use **MySQL Workbench** to import the `.sql` file.

---

## Kafka Service
Currently disabled
The Kafka service is **optional** and may be excluded from the installation.

## ONCE ALL THE ABOVE IS DONE, RUN THE APPLICATION

dotnet rn