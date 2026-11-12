using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TerrainForge.Models;

namespace TerrainForge.Services
{
    public sealed class TerrainGenerationService
    {
        private const double VerticalExaggeration = 2.0;
        private const int GridResolution = 20;
        private const int SurfaceSubdivisionsPerCell = 4;
        private const int ElevationColorBandCount = 32;

        private static readonly Color[] ElevationGradientStops =
        [
            Color.FromRgb(74, 112, 77),
            Color.FromRgb(153, 151, 91),
            Color.FromRgb(157, 128, 101),
            Color.FromRgb(202, 202, 194)
        ];

        private static readonly Color[] ElevationColors = CreateElevationColors();

        public TerrainData CreateTerrain(
            GeoLocation location,
            double areaSizeKilometers,
            IReadOnlyList<ElevationPoint> elevationPoints)
        {
            ArgumentNullException.ThrowIfNull(location);
            ArgumentNullException.ThrowIfNull(elevationPoints);

            if (elevationPoints.Count != GridResolution * GridResolution)
            {
                throw new ArgumentException("A complete 20 by 20 elevation grid is required.", nameof(elevationPoints));
            }

            var minimumElevation = elevationPoints.Min(point => point.ElevationMeters);
            var maximumElevation = elevationPoints.Max(point => point.ElevationMeters);
            var elevationRange = Math.Max(maximumElevation - minimumElevation, 1);
            var surfaceGridResolution = ((GridResolution - 1) * SurfaceSubdivisionsPerCell) + 1;
            var surfacePointCount = surfaceGridResolution * surfaceGridResolution;
            var positions = new Point3D[surfacePointCount];
            var elevations = new double[surfacePointCount];
            var normals = new Vector3D[surfacePointCount];
            var trianglesByColor = new List<(int First, int Second, int Third)>[ElevationColorBandCount];
            var halfAreaMeters = areaSizeKilometers * 500;

            for (var bandIndex = 0; bandIndex < trianglesByColor.Length; bandIndex++)
            {
                trianglesByColor[bandIndex] = new List<(int First, int Second, int Third)>();
            }

            for (var row = 0; row < surfaceGridResolution; row++)
            {

                for (var column = 0; column < surfaceGridResolution; column++)
                {
                    var index = (row * surfaceGridResolution) + column;
                    var sourceRow = row / (double)SurfaceSubdivisionsPerCell;
                    var sourceColumn = column / (double)SurfaceSubdivisionsPerCell;
                    var lowerRow = Math.Min((int)sourceRow, GridResolution - 2);
                    var lowerColumn = Math.Min((int)sourceColumn, GridResolution - 2);
                    var rowFraction = sourceRow - lowerRow;
                    var columnFraction = sourceColumn - lowerColumn;
                    var topLeftElevation = elevationPoints[(lowerRow * GridResolution) + lowerColumn].ElevationMeters;
                    var topRightElevation = elevationPoints[(lowerRow * GridResolution) + lowerColumn + 1].ElevationMeters;
                    var bottomLeftElevation = elevationPoints[((lowerRow + 1) * GridResolution) + lowerColumn].ElevationMeters;
                    var bottomRightElevation = elevationPoints[((lowerRow + 1) * GridResolution) + lowerColumn + 1].ElevationMeters;
                    var topElevation = topLeftElevation + ((topRightElevation - topLeftElevation) * columnFraction);
                    var bottomElevation = bottomLeftElevation + ((bottomRightElevation - bottomLeftElevation) * columnFraction);
                    var elevation = topElevation + ((bottomElevation - topElevation) * rowFraction);
                    var x = -halfAreaMeters + (column / (double)(surfaceGridResolution - 1) * areaSizeKilometers * 1000);
                    var z = -halfAreaMeters + (row / (double)(surfaceGridResolution - 1) * areaSizeKilometers * 1000);
                    var y = (elevation - minimumElevation) * VerticalExaggeration;

                    elevations[index] = elevation;
                    positions[index] = new Point3D(x, y, z);
                }

            }

            var wireframePoints = new List<Point3D>(((surfaceGridResolution - 1) * (surfaceGridResolution - 1) * 2) * 6);
            var triangleCount = 0;

            for (var row = 0; row < surfaceGridResolution - 1; row++)
            {

                for (var column = 0; column < surfaceGridResolution - 1; column++)
                {
                    var topLeft = (row * surfaceGridResolution) + column;
                    var topRight = topLeft + 1;
                    var bottomLeft = topLeft + surfaceGridResolution;
                    var bottomRight = bottomLeft + 1;

                    AddTriangle(topLeft, bottomLeft, topRight);
                    AddTriangle(topRight, bottomLeft, bottomRight);
                }

            }

            var surface = new Model3DGroup();

            for (var bandIndex = 0; bandIndex < trianglesByColor.Length; bandIndex++)
            {
                var triangles = trianglesByColor[bandIndex];

                if (triangles.Count == 0)
                {
                    continue;
                }

                var mesh = new MeshGeometry3D();

                foreach (var triangle in triangles)
                {
                    AddMeshVertex(mesh, triangle.First);
                    AddMeshVertex(mesh, triangle.Second);
                    AddMeshVertex(mesh, triangle.Third);
                    var firstIndex = mesh.Positions.Count - 3;
                    mesh.TriangleIndices.Add(firstIndex);
                    mesh.TriangleIndices.Add(firstIndex + 1);
                    mesh.TriangleIndices.Add(firstIndex + 2);
                }

                var brush = new SolidColorBrush(ElevationColors[bandIndex]);
                brush.Freeze();
                var material = new DiffuseMaterial(brush);
                surface.Children.Add(new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                });
            }

            var averageResolution = elevationPoints
                .Where(point => point.ResolutionMeters.HasValue)
                .Select(point => point.ResolutionMeters.GetValueOrDefault())
                .DefaultIfEmpty()
                .Average();

            return new TerrainData(
                location,
                areaSizeKilometers,
                elevationPoints,
                surface,
                wireframePoints,
                minimumElevation,
                maximumElevation,
                averageResolution > 0 ? averageResolution : null,
                surfacePointCount,
                triangleCount);

            void AddTriangle(int first, int second, int third)
            {
                var firstPosition = positions[first];
                var secondPosition = positions[second];
                var thirdPosition = positions[third];
                var normal = Vector3D.CrossProduct(secondPosition - firstPosition, thirdPosition - firstPosition);

                if (normal.Y < 0)
                {
                    normal = -normal;
                }

                normals[first] += normal;
                normals[second] += normal;
                normals[third] += normal;
                wireframePoints.Add(firstPosition);
                wireframePoints.Add(secondPosition);
                wireframePoints.Add(secondPosition);
                wireframePoints.Add(thirdPosition);
                wireframePoints.Add(thirdPosition);
                wireframePoints.Add(firstPosition);

                var averageHeight = (elevations[first] + elevations[second] + elevations[third]) / 3;
                var normalizedHeight = (averageHeight - minimumElevation) / elevationRange;
                var colorIndex = Math.Min((int)(normalizedHeight * ElevationColorBandCount), ElevationColorBandCount - 1);
                trianglesByColor[colorIndex].Add((first, second, third));
                triangleCount++;
            }

            void AddMeshVertex(MeshGeometry3D mesh, int index)
            {
                mesh.Positions.Add(positions[index]);
                var normal = normals[index];
                normal.Normalize();
                mesh.Normals.Add(normal);
            }
        }

        private static Color[] CreateElevationColors()
        {
            var colors = new Color[ElevationColorBandCount];

            for (var colorIndex = 0; colorIndex < colors.Length; colorIndex++)
            {
                var gradientPosition = colorIndex / (double)(colors.Length - 1) * (ElevationGradientStops.Length - 1);
                var lowerColorIndex = Math.Min((int)gradientPosition, ElevationGradientStops.Length - 2);
                var colorRatio = gradientPosition - lowerColorIndex;
                colors[colorIndex] = Color.FromRgb(
                    InterpolateColorChannel(ElevationGradientStops[lowerColorIndex].R, ElevationGradientStops[lowerColorIndex + 1].R, colorRatio),
                    InterpolateColorChannel(ElevationGradientStops[lowerColorIndex].G, ElevationGradientStops[lowerColorIndex + 1].G, colorRatio),
                    InterpolateColorChannel(ElevationGradientStops[lowerColorIndex].B, ElevationGradientStops[lowerColorIndex + 1].B, colorRatio));
            }

            return colors;
        }

        private static byte InterpolateColorChannel(byte start, byte end, double ratio)
        {
            return (byte)Math.Round(start + ((end - start) * ratio));
        }
    }
}
