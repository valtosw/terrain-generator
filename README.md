

## Google Cloud setup

1. Create or select a Google Cloud project and attach an active billing account.
2. Enable **Geocoding API** and **Elevation API**.
3. Create an API key and restrict it to those two APIs. Apply suitable application restrictions for your development environment.
4. Set the key before launch. In PowerShell for the current terminal session:

   ```powershell
   $env:GOOGLE_MAPS_API_KEY = "your-api-key"
   dotnet run --project .\TerrainForge\TerrainForge.csproj
   ```


## Build and run

Prerequisites: Windows, .NET 10 SDK, and Windows Desktop Runtime. From the repository root:

```powershell
dotnet restore .\TerrainGenerator.slnx
dotnet build .\TerrainGenerator.slnx
dotnet run --project .\TerrainForge\TerrainForge.csproj
```