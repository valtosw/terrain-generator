using System.Text.Json.Serialization;

namespace TerrainForge.Models
{
    internal sealed class GeocodingResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; init; }

        [JsonPropertyName("results")]
        public List<GeocodingResult>? Results { get; init; }
    }

    internal sealed class GeocodingResult
    {
        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; init; }

        [JsonPropertyName("geometry")]
        public GeocodingGeometry? Geometry { get; init; }
    }

    internal sealed class GeocodingGeometry
    {
        [JsonPropertyName("location")]
        public CoordinateResponse? Location { get; init; }
    }

    internal sealed class ElevationResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; init; }

        [JsonPropertyName("results")]
        public List<ElevationResult>? Results { get; init; }
    }

    internal sealed class ElevationResult
    {
        [JsonPropertyName("elevation")]
        public double Elevation { get; init; }

        [JsonPropertyName("resolution")]
        public double? Resolution { get; init; }

        [JsonPropertyName("location")]
        public CoordinateResponse? Location { get; init; }
    }

    internal sealed class CoordinateResponse
    {
        [JsonPropertyName("lat")]
        public double Latitude { get; init; }

        [JsonPropertyName("lng")]
        public double Longitude { get; init; }
    }
}
