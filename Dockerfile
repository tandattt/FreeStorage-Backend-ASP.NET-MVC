# Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ImageUploadApp.csproj", "./"]
RUN dotnet restore "ImageUploadApp.csproj"

COPY . .
RUN dotnet publish "ImageUploadApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Run
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

EXPOSE 5011
ENV ASPNETCORE_URLS=http://+:5011
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ImageUploadApp.dll"]
