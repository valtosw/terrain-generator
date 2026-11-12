using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using TerrainForge.Models;
using TerrainForge.ViewModels;

namespace TerrainForge
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            _viewModel = new MainViewModel(httpClient);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName is nameof(MainViewModel.Terrain) or nameof(MainViewModel.RenderMode))
            {
                UpdateViewport(_viewModel.Terrain, _viewModel.RenderMode);
            }

        }

        private void UpdateViewport(TerrainData? terrain, RenderMode renderMode)
        {
            var persistentVisual = TerrainViewport.Children
                .OfType<ModelVisual3D>()
                .FirstOrDefault(visual => visual.Content is Model3DGroup modelGroup && modelGroup.Children.Count == 2);
            TerrainViewport.Children.Clear();

            if (persistentVisual is not null)
            {
                TerrainViewport.Children.Add(persistentVisual);
            }

            if (terrain is null)
            {
                return;
            }

            if (renderMode == RenderMode.Wireframe)
            {
                var lines = new LinesVisual3D
                {
                    Points = new Point3DCollection(terrain.WireframePoints),
                    Color = Color.FromRgb(82, 79, 70),
                    Thickness = 1
                };
                TerrainViewport.Children.Add(lines);
            }

            else
            {
                TerrainViewport.Children.Add(new ModelVisual3D
                {
                    Content = terrain.Surface
                });
            }

            TerrainViewport.ZoomExtents(0.35);
        }
    }
}
