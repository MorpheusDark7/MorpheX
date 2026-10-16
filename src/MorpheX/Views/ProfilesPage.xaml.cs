using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MorpheX.Core.Models;
using MorpheX.Views;
using Serilog;
using Wpf.Ui.Controls;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;

namespace MorpheX;

public partial class ProfilesPage : Page
{
    public ProfilesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService != null)
        {
            app.ProfileService.ProfilesChanged -= OnProfilesChanged;
            app.ProfileService.ProfilesChanged += OnProfilesChanged;
        }

        RenderProfiles();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService != null)
        {
            app.ProfileService.ProfilesChanged -= OnProfilesChanged;
        }
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(RenderProfiles);
    }

    public void RenderProfiles()
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        var profiles = app.ProfileService.Profiles;
        var activeId = app.ProfileService.ActiveProfileId;
        var activeProfile = profiles.FirstOrDefault(p => p.Id == activeId) ?? profiles.FirstOrDefault();

        // 1. Update Active Hero Banner
        if (activeProfile != null)
        {
            ActiveProfileNameText.Text = activeProfile.Name;
            string wpSummary = activeProfile.IncludeWallpaper
                ? $"{activeProfile.MonitorWallpapers.Count} monitor wallpaper(s)"
                : "No wallpapers locked";
            ActiveProfileSummaryText.Text = $"{wpSummary}  •  Updated {activeProfile.UpdatedAt:MMM d, yyyy h:mm tt}";
        }

        ProfileCountText.Text = profiles.Count == 1 ? "1 saved profile" : $"{profiles.Count} saved profiles";

        // 2. Render Cards
        ProfilesContainer.Children.Clear();

        foreach (var profile in profiles)
        {
            bool isActive = profile.Id == activeId;
            var card = CreateProfileCard(profile, isActive, profiles.Count);
            ProfilesContainer.Children.Add(card);
        }
    }

    private UIElement CreateProfileCard(DesktopProfile profile, bool isActive, int totalCount)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.FindResource("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = isActive
                ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                : (Brush)Application.Current.FindResource("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(isActive ? 1.5 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left info
        var leftPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var nameBlock = new System.Windows.Controls.TextBlock
        {
            Text = profile.Name,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.FindResource("TextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        nameRow.Children.Add(nameBlock);

        if (isActive)
        {
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new System.Windows.Controls.TextBlock
            {
                Text = "ACTIVE",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            nameRow.Children.Add(badge);
        }

        leftPanel.Children.Add(nameRow);

        // Tags / pills row
        var tagsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };

        string wpTag = profile.IncludeWallpaper
            ? $"{profile.MonitorWallpapers.Count} monitor(s)"
            : "No wallpapers";
        tagsRow.Children.Add(CreateTag(wpTag));

        var dateBlock = new System.Windows.Controls.TextBlock
        {
            Text = $"Updated {profile.UpdatedAt:MMM d, yyyy}",
            FontSize = 11,
            Opacity = 0.5,
            Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        tagsRow.Children.Add(dateBlock);

        leftPanel.Children.Add(tagsRow);
        Grid.SetColumn(leftPanel, 0);
        grid.Children.Add(leftPanel);

        // Right buttons
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        if (!isActive)
        {
            var activateBtn = new Wpf.Ui.Controls.Button
            {
                Content = "Activate",
                Icon = new SymbolIcon(SymbolRegular.Play24),
                Appearance = ControlAppearance.Primary,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 0)
            };
            activateBtn.Click += async (_, _) => await ApplyProfile(profile.Id);
            btnPanel.Children.Add(activateBtn);
        }

        var saveBtn = new Wpf.Ui.Controls.Button
        {
            Content = "Save Live Setup",
            Icon = new SymbolIcon(SymbolRegular.Save24),
            Appearance = ControlAppearance.Secondary,
            Height = 32,
            Margin = new Thickness(0, 0, 6, 0)
        };
        saveBtn.Click += async (_, _) => await SaveProfile(profile.Id);
        btnPanel.Children.Add(saveBtn);

        var renameBtn = new Wpf.Ui.Controls.Button
        {
            Content = "Rename",
            Icon = new SymbolIcon(SymbolRegular.Edit24),
            Appearance = ControlAppearance.Secondary,
            Height = 32,
            Margin = new Thickness(0, 0, 6, 0)
        };
        renameBtn.Click += async (_, _) => await RenameProfile(profile.Id, profile.Name);
        btnPanel.Children.Add(renameBtn);

        var deleteBtn = new Wpf.Ui.Controls.Button
        {
            Content = "Delete",
            Icon = new SymbolIcon(SymbolRegular.Delete24),
            Appearance = ControlAppearance.Danger,
            Height = 32,
            IsEnabled = totalCount > 1
        };
        deleteBtn.Click += async (_, _) => await DeleteProfile(profile.Id, profile.Name);
        btnPanel.Children.Add(deleteBtn);

        Grid.SetColumn(btnPanel, 1);
        grid.Children.Add(btnPanel);

        card.Child = grid;
        return card;
    }

    private static Border CreateTag(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = (Brush)Application.Current.FindResource("TextFillColorSecondaryBrush")
            }
        };
    }

    private async Task ApplyProfile(string profileId)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        await app.ProfileService.ApplyProfileAsync(
            profileId,
            app.WallpaperService,
            app.LibraryService,
            app.WidgetService,
            notice => ModernDialogWindow.Warning("Profile Notice", "Wallpaper Skipped", notice));

        RenderProfiles();
    }

    private async Task SaveProfile(string profileId)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        bool ok = await app.ProfileService.UpdateProfileWithCurrentSetupAsync(profileId);
        if (ok)
        {
            RenderProfiles();
            ModernDialogWindow.Success("Desktop Profiles", "Profile Saved", "Saved your current desktop setup, wallpapers, and widgets to this profile.");
        }
    }

    private async Task RenameProfile(string profileId, string currentName)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        var dialog = new ProfileNameDialog(currentName: currentName, title: "Rename Profile", heading: "Enter new profile name", showWallpaperCheck: false)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ProfileName))
        {
            await app.ProfileService.RenameProfileAsync(profileId, dialog.ProfileName);
            RenderProfiles();
        }
    }

    private async Task DeleteProfile(string profileId, string profileName)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        if (app.ProfileService.Profiles.Count <= 1)
        {
            ModernDialogWindow.Information("Desktop Profiles", "Cannot delete", "You must keep at least one desktop profile.");
            return;
        }

        bool ok = await app.ProfileService.DeleteProfileAsync(profileId);
        if (ok)
        {
            var next = app.ProfileService.Profiles.FirstOrDefault();
            if (next != null)
            {
                await app.ProfileService.ApplyProfileAsync(next.Id, app.WallpaperService, app.LibraryService, app.WidgetService);
            }
            RenderProfiles();
        }
    }

    private async void NewProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        var dialog = new ProfileNameDialog(currentName: $"Profile {app.ProfileService.Profiles.Count + 1}", title: "New Desktop Profile", heading: "Name your new profile", showWallpaperCheck: true)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ProfileName))
        {
            await app.ProfileService.CreateProfileAsync(dialog.ProfileName, dialog.IncludeWallpaper);
            RenderProfiles();
        }
    }

    private async void SaveCurrentSetupBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        var activeId = app.ProfileService.ActiveProfileId;
        if (!string.IsNullOrEmpty(activeId))
        {
            await SaveProfile(activeId);
        }
    }

    private async void QuickRenameBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        var activeId = app.ProfileService.ActiveProfileId;
        var profile = app.ProfileService.Profiles.FirstOrDefault(p => p.Id == activeId);
        if (profile != null)
        {
            await RenameProfile(profile.Id, profile.Name);
        }
    }

    private async void QuickUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        if (app?.ProfileService == null) return;

        var activeId = app.ProfileService.ActiveProfileId;
        if (!string.IsNullOrEmpty(activeId))
        {
            await SaveProfile(activeId);
        }
    }
}
