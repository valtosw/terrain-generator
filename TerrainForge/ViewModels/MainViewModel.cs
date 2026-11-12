using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Media3D;
using TerrainForge.Configuration;
using TerrainForge.Models;
using TerrainForge.Services;

namespace TerrainForge.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly GeocodingService _geocodingService;
        private readonly ElevationService _elevationService;
        private readonly TerrainGenerationService _terrainGenerationService;
        private CancellationTokenSource? _generationCancellation;
        private string _locationQuery = "Hoverla, Ukraine";
        private string _areaSizeText = "5";
        private bool _isBusy;
        private string _statusMessage = "Ready. Configure a Google Maps API key to begin.";
        private string? _errorMessage;
        private GeoLocation? _location;
        private TerrainData? _terrain;
        private RenderMode _renderMode = RenderMode.Solid;

        public MainViewModel(HttpClient httpClient)
        {
            var configuration = new ApiConfiguration();
            _geocodingService = new GeocodingService(httpClient, configuration);
            _elevationService = new ElevationService(httpClient, configuration);
            _terrainGenerationService = new TerrainGenerationService();
            GenerateCommand = new AsyncCommand(GenerateTerrainAsync, () => !IsBusy);
            CancelCommand = new AsyncCommand(CancelGeneration, () => IsBusy);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AsyncCommand GenerateCommand { get; }

        public AsyncCommand CancelCommand { get; }

        public string LocationQuery
        {
            get => _locationQuery;
            set
            {
                if (SetProperty(ref _locationQuery, value))
                {
                    GenerateCommand.Refresh();
                }

            }
        }

        public string AreaSizeText
        {
            get => _areaSizeText;
            set
            {
                if (SetProperty(ref _areaSizeText, value))
                {
                    GenerateCommand.Refresh();
                }

            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    GenerateCommand.Refresh();
                    CancelCommand.Refresh();
                    OnPropertyChanged(nameof(LoadingVisibility));
                }

            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }

            }
        }

        public GeoLocation? Location
        {
            get => _location;
            private set
            {
                if (SetProperty(ref _location, value))
                {
                    OnPropertyChanged(nameof(LocationName));
                    OnPropertyChanged(nameof(CoordinatesText));
                }

            }
        }

        public TerrainData? Terrain
        {
            get => _terrain;
            private set
            {
                if (SetProperty(ref _terrain, value))
                {
                    OnPropertyChanged(nameof(HasTerrain));
                    OnPropertyChanged(nameof(MinimumElevationText));
                    OnPropertyChanged(nameof(MaximumElevationText));
                    OnPropertyChanged(nameof(ElevationDifferenceText));
                    OnPropertyChanged(nameof(VertexCountText));
                    OnPropertyChanged(nameof(TriangleCountText));
                    OnPropertyChanged(nameof(AreaText));
                    OnPropertyChanged(nameof(ResolutionText));
                }

            }
        }

        public RenderMode RenderMode
        {
            get => _renderMode;
            set => SetProperty(ref _renderMode, value);
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasTerrain => Terrain is not null;

        public Visibility LoadingVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

        public string LocationName => Location?.FormattedAddress ?? "—";

        public string CoordinatesText => Location is null
            ? "—"
            : $"{Location.Latitude.ToString("F5", CultureInfo.InvariantCulture)}, {Location.Longitude.ToString("F5", CultureInfo.InvariantCulture)}";

        public string MinimumElevationText => Terrain?.MinimumElevationMeters.ToString("F0", CultureInfo.CurrentCulture) + " m" ?? "—";

        public string MaximumElevationText => Terrain?.MaximumElevationMeters.ToString("F0", CultureInfo.CurrentCulture) + " m" ?? "—";

        public string ElevationDifferenceText => Terrain is null
            ? "—"
            : $"{(Terrain.MaximumElevationMeters - Terrain.MinimumElevationMeters).ToString("F0", CultureInfo.CurrentCulture)} m";

        public string VertexCountText => Terrain?.VertexCount.ToString("N0", CultureInfo.CurrentCulture) ?? "—";

        public string TriangleCountText => Terrain?.TriangleCount.ToString("N0", CultureInfo.CurrentCulture) ?? "—";

        public string AreaText => Terrain is null ? "—" : $"{Terrain.AreaSizeKilometers.ToString("F1", CultureInfo.CurrentCulture)} × {Terrain.AreaSizeKilometers.ToString("F1", CultureInfo.CurrentCulture)} km";

        public string ResolutionText => Terrain?.AverageResolutionMeters is double resolution
            ? $"{resolution.ToString("F1", CultureInfo.CurrentCulture)} m"
            : "—";

        private async Task GenerateTerrainAsync()
        {
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(LocationQuery))
            {
                ErrorMessage = "Enter a location to generate terrain.";

                return;
            }

            if (!double.TryParse(AreaSizeText, NumberStyles.Float, CultureInfo.CurrentCulture, out var areaSizeKilometers) || areaSizeKilometers is < 0.1 or > 100)
            {
                ErrorMessage = "Terrain area must be between 0.1 and 100 km.";

                return;
            }

            IsBusy = true;
            StatusMessage = "Resolving location and requesting elevation data…";
            _generationCancellation = new CancellationTokenSource();
            var cancellationToken = _generationCancellation.Token;

            try
            {
                var location = await _geocodingService.ResolveAsync(LocationQuery.Trim(), cancellationToken);
                var elevations = await _elevationService.GetGridAsync(location, areaSizeKilometers, cancellationToken);
                var terrain = _terrainGenerationService.CreateTerrain(location, areaSizeKilometers, elevations);
                Location = location;
                Terrain = terrain;
                StatusMessage = $"Terrain generated from {elevations.Count} Google Elevation samples.";
            }

            catch (HttpRequestException)
            {
                ErrorMessage = "The Google Maps service could not be reached. Check your network connection and API availability.";
                StatusMessage = "Terrain generation failed.";
            }

            catch (InvalidOperationException exception)
            {
                ErrorMessage = exception.Message;
                StatusMessage = "Terrain generation failed.";
            }

            catch (OperationCanceledException)
            {
                StatusMessage = "Terrain generation was canceled.";
            }

            catch (Exception)
            {
                ErrorMessage = "Terrain generation failed because the service returned an unexpected response.";
                StatusMessage = "Terrain generation failed.";
            }

            finally
            {
                _generationCancellation?.Dispose();
                _generationCancellation = null;
                IsBusy = false;
            }

        }

        private Task CancelGeneration()
        {
            _generationCancellation?.Cancel();

            return Task.CompletedTask;
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);

            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
