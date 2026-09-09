using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX;

public partial class FavoritesPage : Page
{
    public FavoritesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshFavorites();
    }

    public void RefreshFavorites()
    {
        var app = (App)Application.Current;
        var favorites = app.LibraryService.Wallpapers.Where(w => w.IsFavorite).ToList();
        foreach (var w in favorites)
        {
            w.IsActive = app.WallpaperService != null && app.WallpaperService.IsWallpaperActive(w.Id);
        }

        FavoritesGrid.ItemsSource = null;
        FavoritesGrid.ItemsSource = favorites;
        FavoriteCountText.Text = $"{favorites.Count} favorite{(favorites.Count != 1 ? "s" : "")}";
        EmptyState.Visibility = favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void WallpaperCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WallpaperInfo wallpaper)
        {
            await MonitorPickerHelper.ApplyWallpaperWithPickerAsync(wallpaper, RefreshFavorites);
        }
    }
}
