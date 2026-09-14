# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Smart-X-Ecosystem.csproj", "."]
RUN dotnet restore "./Smart-X-Ecosystem.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Smart-X-Ecosystem.csproj" -c Release -o /app/build

# Publish Stage
FROM build AS publish
RUN dotnet publish "Smart-X-Ecosystem.csproj" -c Release -o /app/publish

# Run Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Smart-X-Ecosystem.dll"]