using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using LoLProximityChat.Core;
using LoLProximityChat.Core.Models;
using LoLProximityChat.WPF.ViewModels;
using Color       = System.Windows.Media.Color;
using Brushes     = System.Windows.Media.Brushes;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment   = System.Windows.VerticalAlignment;

namespace LoLProximityChat.WPF.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _vm = null!;
        private bool _settingsOpen = false;

        private DispatcherTimer? _pendingTimer;
        private bool _blinkState = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Delay(200);
                if (_vm != null)
                {
                    await _vm.JoinTestRoomAsync();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Join Error");
            }
        }

        public void Initialize(AppConfig config)
        {
            _vm = new MainViewModel(config);
            _vm.Orchestrator.OnStateChanged += OnStateChanged;

            DiscordUsernameText.Text = config.DiscordUsername;
        }

        // ======================
        // STATUS LOGIC
        // ======================

        private void OnStateChanged(OrchestratorState state)
        {
            Dispatcher.Invoke(() =>
            {
                StopPendingAnimation();

                switch (state)
                {
                    case OrchestratorState.Idle:
                        SetStatus(Color.FromRgb(255, 100, 100), "OFF");
                        break;

                    case OrchestratorState.Disconnected:
                        StartPendingAnimation();
                        StatusText.Text = "PENDING";
                        break;

                    case OrchestratorState.InGame:
                        SetStatus(Color.FromRgb(93, 202, 165), "CONNECTED");
                        break;
                }
            });
        }

        private void SetStatus(Color color, string text)
        {
            StatusDot.Fill = new SolidColorBrush(color);
            StatusText.Text = text;
        }

        private void StartPendingAnimation()
        {
            _pendingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            _pendingTimer.Tick += (_, _) =>
            {
                _blinkState = !_blinkState;

                StatusDot.Fill = new SolidColorBrush(
                    _blinkState
                        ? Color.FromRgb(255, 180, 0)
                        : Color.FromRgb(60, 60, 60)
                );
            };

            _pendingTimer.Start();
        }

        private void StopPendingAnimation()
        {
            if (_pendingTimer != null)
            {
                _pendingTimer.Stop();
                _pendingTimer = null;
            }
        }

        // ======================
        // UI EVENTS
        // ======================

        private void OnQuitClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void OnDragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnToggleSettings(object sender, MouseButtonEventArgs e)
        {
            _settingsOpen            = !_settingsOpen;
            PlayersPanel.Visibility  = _settingsOpen ? Visibility.Collapsed : Visibility.Visible;
            SettingsPanel.Visibility = _settingsOpen ? Visibility.Visible   : Visibility.Collapsed;
        }

        private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeLabel != null)
                VolumeLabel.Text = $"{(int)e.NewValue}%";
        }

        // ======================
        // PLAYERS
        // ======================

        public void UpdatePlayers(List<PlayerViewModel> players, string roomId, string channelName)
        {
            Dispatcher.Invoke(() =>
            {
                RoomIdText.Text      = roomId;
                ChannelText.Text     = channelName;
                PlayerCountText.Text = $"{players.Count} joueurs connectés";

                PlayersPanel.Children.Clear();
                foreach (var player in players)
                    PlayersPanel.Children.Add(CreatePlayerRow(player));
            });
        }

        private UIElement CreatePlayerRow(PlayerViewModel player)
        {
            var grid = new Grid { Margin = new Thickness(16, 8, 16, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarGrid = new Grid
            {
                Width  = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(avatarGrid, 0);

            if (player.IsSpeaking)
            {
                for (int i = 0; i < 2; i++)
                {
                    var ring = new Ellipse
                    {
                        Width = 32,
                        Height = 32,
                        Stroke = new SolidColorBrush(Color.FromRgb(93, 202, 165)),
                        StrokeThickness = 1.5
                    };

                    var scaleTransform = new ScaleTransform(1, 1, 16, 16);
                    ring.RenderTransform = scaleTransform;

                    var scaleAnim = new DoubleAnimation(1, 1.8, TimeSpan.FromSeconds(1.4))
                    {
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(i * 0.7)
                    };

                    var opacityAnim = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.4))
                    {
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(i * 0.7)
                    };

                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                    ring.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

                    avatarGrid.Children.Add(ring);
                }
            }

            var avatarBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 53)),
                BorderThickness = new Thickness(player.IsLocal ? 1.5 : 0),
                BorderBrush = player.IsLocal
                    ? new SolidColorBrush(Color.FromRgb(93, 202, 165))
                    : Brushes.Transparent
            };

            avatarBorder.Child = new TextBlock
            {
                Text = player.Initial.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            avatarGrid.Children.Add(avatarBorder);
            grid.Children.Add(avatarGrid);

            return grid;
        }
    }
}