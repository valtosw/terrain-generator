using System.Windows.Media.Media3D;

namespace TerrainForge.Models
{
    public sealed record TerrainData(
        GeoLocation Location,
        double AreaSizeKilometers,
        IReadOnlyList<ElevationPoint> ElevationPoints,
        Model3D Surface,
        IReadOnlyList<Point3D> WireframePoints,
        double MinimumElevationMeters,
        double MaximumElevationMeters,
        double? AverageResolutionMeters,
        int VertexCount,
        int TriangleCount);
}
