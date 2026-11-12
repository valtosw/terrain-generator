namespace TerrainForge.Configuration
{
    public sealed class ApiConfiguration
    {
        public const string GoogleMapsApiKeyEnvironmentVariable = "GOOGLE_MAPS_API_KEY";

        public string ApiKey => Environment.GetEnvironmentVariable(GoogleMapsApiKeyEnvironmentVariable)?.Trim() ?? string.Empty;
    }
}
