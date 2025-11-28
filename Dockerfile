# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
ENV DEBIAN_FRONTEND=noninteractive

# Install Liberation Sans font
RUN apt-get update && \
    apt-get install -y --no-install-recommends fonts-liberation && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENTRYPOINT ["dotnet", "MyAuthenticationBackend.dll"]
