namespace TerrainForge.Models
{
    public sealed record GeoLocation(
        string FormattedAddress,
        double Latitude,
        double Longitude);
}
