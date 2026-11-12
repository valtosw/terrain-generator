namespace TerrainForge.Models
{
    public sealed record ElevationPoint(
        int Row,
        int Column,
        double Latitude,
        double Longitude,
        double ElevationMeters,
        double? ResolutionMeters);
}
