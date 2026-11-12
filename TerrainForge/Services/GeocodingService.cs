using System.Net;
using System.Net.Http;
using System.Text.Json;
using TerrainForge.Configuration;
using TerrainForge.Models;

namespace TerrainForge.Services
{
    public sealed class GeocodingService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly ApiConfiguration _configuration;

        public GeocodingService(HttpClient httpClient, ApiConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<GeoLocation> ResolveAsync(string address, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(address);

            var apiKey = _configuration.ApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Set the GOOGLE_MAPS_API_KEY environment variable before generating terrain.");
            }

            var requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(apiKey)}";
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == HttpStatusCode.RequestUriTooLong)
            {
                throw new InvalidOperationException("The geocoding request was too long.");
            }

            response.EnsureSuccessStatusCode();
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<GeocodingResponse>(responseStream, JsonOptions, cancellationToken);

            if (payload is null)
            {
                throw new InvalidOperationException("Google Geocoding returned an empty response.");
            }

            if (!string.Equals(payload.Status, "OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(MapGoogleStatus(payload.Status, payload.ErrorMessage));
            }

            var result = payload.Results?.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.FormattedAddress) &&
                candidate.Geometry?.Location is { } candidateLocation &&
                IsValidCoordinate(candidateLocation.Latitude, candidateLocation.Longitude));
            var coordinate = result?.Geometry?.Location;

            if (coordinate is null || string.IsNullOrWhiteSpace(result?.FormattedAddress))
            {
                throw new InvalidOperationException("Google Geocoding did not return a usable location.");
            }

            return new GeoLocation(result.FormattedAddress, coordinate.Latitude, coordinate.Longitude);
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
                "ZERO_RESULTS" => "No matching location was found. Try a more specific place name.",
                "OVER_QUERY_LIMIT" => "Google Geocoding quota was exceeded. Check the project's quota and billing setup.",
                "REQUEST_DENIED" => "Google denied the Geocoding request. Check the API key, enabled API, and billing setup.",
                "INVALID_REQUEST" => "Google could not understand the location request.",
                _ => "Google Geocoding returned an unexpected status."
            };

            return string.IsNullOrWhiteSpace(errorMessage) ? message : $"{message} Details: {errorMessage}";
        }
    }
}
