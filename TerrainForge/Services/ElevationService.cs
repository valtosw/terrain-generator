using System.Net.Http;
using System.Text.Json;
using TerrainForge.Configuration;
using TerrainForge.Models;

namespace TerrainForge.Services
{
    public sealed class ElevationService
    {
        private const int GridResolution = 20;
        private const int MaximumLocationsPerRequest = 512;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly ApiConfiguration _configuration;

        public ElevationService(HttpClient httpClient, ApiConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<ElevationPoint>> GetGridAsync(
            GeoLocation center,
            double areaSizeKilometers,
            CancellationToken cancellationToken)
        {
            if (GridResolution * GridResolution > MaximumLocationsPerRequest)
            {
                throw new InvalidOperationException("The terrain grid exceeds the Google Elevation request limit.");
            }

            var coordinates = CreateGrid(center, areaSizeKilometers);
            var locations = string.Join('|', coordinates.Select(point =>
                $"{point.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{point.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}"));
            var requestUri = $"https://maps.googleapis.com/maps/api/elevation/json?locations={Uri.EscapeDataString(locations)}&key={Uri.EscapeDataString(_configuration.ApiKey)}";
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<ElevationResponse>(responseStream, JsonOptions, cancellationToken);

            if (payload is null)
            {
                throw new InvalidOperationException("Google Elevation returned an empty response.");
            }

            if (!string.Equals(payload.Status, "OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(MapGoogleStatus(payload.Status, payload.ErrorMessage));
            }

            var results = payload.Results;

            if (results is null || results.Count != coordinates.Count)
            {
                throw new InvalidOperationException($"Google Elevation returned {results?.Count ?? 0} samples; {coordinates.Count} are required.");
            }

            var elevationPoints = new List<ElevationPoint>(results.Count);

            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];

                if (!double.IsFinite(result.Elevation) ||
                    result.Location is null ||
                    !IsValidCoordinate(result.Location.Latitude, result.Location.Longitude))
                {
                    throw new InvalidOperationException($"Google Elevation returned invalid data for grid sample {index + 1}.");
                }

                elevationPoints.Add(new ElevationPoint(
                    index / GridResolution,
                    index % GridResolution,
                    result.Location.Latitude,
                    result.Location.Longitude,
                    result.Elevation,
                    result.Resolution));
            }

            return elevationPoints;
        }

        private static List<(double Latitude, double Longitude)> CreateGrid(GeoLocation center, double areaSizeKilometers)
        {
            var coordinates = new List<(double Latitude, double Longitude)>(GridResolution * GridResolution);
            var latitudeOffset = areaSizeKilometers / 111.32 / 2;
            var longitudeScale = Math.Max(Math.Abs(Math.Cos(center.Latitude * Math.PI / 180)), 0.01);
            var longitudeOffset = areaSizeKilometers / (111.32 * longitudeScale) / 2;

            for (var row = 0; row < GridResolution; row++)
            {
                var rowRatio = row / (double)(GridResolution - 1);

                for (var column = 0; column < GridResolution; column++)
                {
                    var columnRatio = column / (double)(GridResolution - 1);
                    var latitude = center.Latitude + latitudeOffset - (rowRatio * latitudeOffset * 2);
                    var longitude = center.Longitude - longitudeOffset + (columnRatio * longitudeOffset * 2);

                    coordinates.Add((latitude, longitude));
                }

            }

            return coordinates;
        }

        private static bool IsValidCoordinate(double latitude, double longitude)
        {
            return double.IsFinite(latitude) &&
                latitude is >= -90 and <= 90 &&
                double.IsFinite(longitude) &&
                longitude is >= -180 and <= 180;
        }

        private static string MapGoogleStatus(string? status, string? errorMessage)
        {
            var message = status switch
            {
                "OVER_QUERY_LIMIT" => "Google Elevation quota was exceeded. Check the project's quota and billing setup.",
                "REQUEST_DENIED" => "Google denied the Elevation request. Check the API key, enabled API, and billing setup.",
                "INVALID_REQUEST" => "Google could not process the elevation grid.",
                "NOT_FOUND" => "Google could not find elevation data for the requested area.",
                _ => "Google Elevation returned an unexpected status."
            };

            return string.IsNullOrWhiteSpace(errorMessage) ? message : $"{message} Details: {errorMessage}";
        }
    }
}
