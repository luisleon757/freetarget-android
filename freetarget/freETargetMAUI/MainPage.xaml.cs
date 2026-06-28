using freETarget;
using freETarget.targets;
using freETargetMAUI.Models;
using freETargetMAUI.Services;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace freETargetMAUI;

public partial class MainPage : ContentPage
{
    private aTarget _currentTarget;
    private Session _currentSession;
    private List<Shot> _domainShots;
    private ObservableCollection<ShotViewModel> _shotsUI;
    private int _shotCounter = 0;
    private string _currentShooterName = "Invitado";
    private Random _random = new Random();
    private TargetConnectionService _connectionService;
    private freETarget.StorageController _storageController;
    private IDispatcherTimer _matchTimer;
    private TimeSpan _countdownTime;
    private decimal _targetScaleFactor = 1.0m;

    public MainPage(freETarget.StorageController storageController)
    {
        InitializeComponent();
        _storageController = storageController;
        
        _domainShots = new List<Shot>();
        _shotsUI = new ObservableCollection<ShotViewModel>();
        ShotsListView.ItemsSource = _shotsUI;
        
        _connectionService = new TargetConnectionService();
        _connectionService.OnConnectionStateChanged += ConnectionService_OnConnectionStateChanged;
        _connectionService.OnShotReceived += ConnectionService_OnShotReceived;
        _connectionService.OnRawDataReceived += ConnectionService_OnRawDataReceived;
        
        _matchTimer = Application.Current.Dispatcher.CreateTimer();
        _matchTimer.Interval = TimeSpan.FromSeconds(1);
        _matchTimer.Tick += MatchTimer_Tick;
        _countdownTime = TimeSpan.FromMinutes(90);

        SetupInitialEvent();
    }

    private void MatchTimer_Tick(object? sender, EventArgs e)
    {
        if (_countdownTime.TotalSeconds > 0)
        {
            _countdownTime = _countdownTime.Subtract(TimeSpan.FromSeconds(1));
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CountdownLabel.Text = _countdownTime.ToString(@"hh\:mm\:ss");
            });
        }
        else
        {
            _matchTimer.Stop();
        }
    }

    private void OnToggleTimerClicked(object? sender, EventArgs e)
    {
        if (_matchTimer.IsRunning)
        {
            _matchTimer.Stop();
        }
        else
        {
            _matchTimer.Start();
        }
    }

    private void SetupInitialEvent()
    {
        _currentTarget = new AirPistol(4.5m);
        
        Event ev = new Event(-1, "Training", false, Event.EventType.Practice, 10, _currentTarget, 60, 4.5m, 0, 0, 0, 0, 0, Colors.Transparent, false, 0, 0, 0, 0, 0);
        _currentSession = Session.createNewSession(ev, _currentShooterName);
        
        AppTargetDrawable.Target = _currentTarget;
        AppTargetDrawable.CurrentSession = _currentSession;
        AppTargetDrawable.Shots = _domainShots;
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        IntegerScoreLabel.Text = $"{_domainShots.Sum(s => s.score)}";
        ScoreLabel.Text = $"({_domainShots.Sum(s => s.decimalScore):F1})";
        
        int skip = (_domainShots.Count - 1) / 10 * 10;
        if (skip < 0) skip = 0;
        
        AppTargetDrawable.Shots = _domainShots.Skip(skip).ToList(); 
        TargetGraphicsView.Invalidate(); 
        
        if (_currentSession != null)
        {
            EndSessionButton.IsVisible = _currentSession.sessionType == Event.EventType.Match;
        }
    }

    private async void OnStartEventClicked(object? sender, EventArgs e)
    {
        // Placeholder for Event Configuration dialog or screen
    }

    private async void OnEndSessionClicked(object? sender, EventArgs e)
    {
        if (_matchTimer != null && _matchTimer.IsRunning) _matchTimer.Stop();
        if (_currentSession != null)
        {
            Microsoft.Maui.Storage.Preferences.Default.Set($"Timer_{_currentSession.startTime.Ticks}", _countdownTime.TotalSeconds);
        }

        if (_domainShots.Count > 0)
        {
            _currentSession.endTime = DateTime.Now;
            _currentSession.Shots = _domainShots.ToList();
            if (_currentSession.id > 0)
            {
                _storageController.updateSession(_currentSession);
                await DisplayAlert("Sesión Actualizada", $"Se han guardado {_domainShots.Count} disparos en tu sesión existente.", "Aceptar");
            }
            else
            {
                _storageController.storeSession(_currentSession, true);
                await DisplayAlert("Sesión Guardada", $"Se han guardado {_domainShots.Count} disparos en una nueva sesión.", "Aceptar");
            }
            
            // Reiniciar sesión visualmente
            _domainShots.Clear();
            _shotsUI.Clear();
            _shotCounter = 0;
            _currentSession = Session.createNewSession(_currentSession.eventType, _currentSession.user);
            AppTargetDrawable.CurrentSession = _currentSession;
            UpdateUI();
        }
        else
        {
            await DisplayAlert("Atención", "No hay disparos en la sesión actual.", "Aceptar");
        }
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        if (_connectionService.IsConnected)
        {
            _connectionService.Disconnect();
            ConnectButton.Text = "Conectar WiFi";
            ConnectButton.BackgroundColor = Color.FromArgb("#10B981"); // Green
            ConnectionStatusDot.Fill = Color.FromArgb("#EF4444"); // Red
            ConnectionStatusLabel.Text = "Desconectado";
        }
        else
        {
            ConnectionStatusLabel.Text = "Conectando...";
            await _connectionService.ConnectAsync();
            if (_connectionService.IsConnected)
            {
                ConnectButton.Text = "Desconectar";
                ConnectButton.BackgroundColor = Color.FromArgb("#EF4444"); // Red
                ConnectionStatusDot.Fill = Color.FromArgb("#10B981"); // Green
                ConnectionStatusLabel.Text = "Conectado";
            }
            else
            {
                ConnectionStatusLabel.Text = "Desconectado";
            }
        }
    }

    private void ConnectionService_OnConnectionStateChanged(object? sender, string stateMessage)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionStatusLabel.Text = stateMessage;
        });
    }

    private void ConnectionService_OnRawDataReceived(object? sender, string rawData)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
             var currentText = RawDataDebugLabel.Text;
             if (currentText.Length > 200) currentText = currentText.Substring(currentText.Length - 200);
             RawDataDebugLabel.Text = currentText + "\n" + rawData;
        });
    }

    private void ConnectionService_OnShotReceived(object? sender, ShotReceivedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            decimal normalizedX = e.X / _targetScaleFactor;
            decimal normalizedY = e.Y / _targetScaleFactor;
            ProcessRealShot(normalizedX, normalizedY, e.ShotNumber);
        });
    }

    private void ProcessRealShot(decimal x, decimal y, int shotNumber)
    {
        if (_currentSession.sessionType == Event.EventType.Match)
        {
            if (_domainShots.Count >= 60)
            {
                return; // Ignorar si ya terminó
            }
        }

        Shot shot = new Shot(0, 0, 0);
        shot.index = _shotCounter++;
        shot.setX(x);
        shot.setY(y);
        shot.miss = false;
        
        shot.computeScore(_currentTarget);

        _domainShots.Add(shot); 
        
        foreach(var s in _shotsUI) s.IsLatest = false;
        
        var svm = new ShotViewModel { Shot = shot, DisplayText = shot.decimalScore.ToString("F1"), IsLatest = true };
        _shotsUI.Add(svm);
        
        if (_domainShots.Count > 0 && _domainShots.Count % 10 == 0)
        {
            var seriesShots = _domainShots.Skip(_domainShots.Count - 10).Take(10);
            int intSum = seriesShots.Sum(s => s.score);
            _shotsUI.Add(new ShotViewModel { IsSum = true, DisplayText = intSum.ToString() });
        }
        
        UpdateUI();
    }

    private async void OnLightIntensityChanged(object? sender, EventArgs e)
    {
        if (LightIntensityPicker.SelectedIndex == -1) return;
        
        string? selected = (string)LightIntensityPicker.Items[LightIntensityPicker.SelectedIndex];
        int val = 0;
        
        if (selected == "99%") val = 99;
        else if (selected == "75%") val = 75;
        else if (selected == "50%") val = 50;
        else if (selected == "25%") val = 25;
        else if (selected == "0%") val = 0;
        
        if (_connectionService != null && _connectionService.IsConnected)
        {
            string cmd = $"{{\"LED_BRIGHT\": {val}}}";
            await _connectionService.SendMessageAsync(cmd);
        }
    }

    private void LoadShooters()
    {
        var users = _storageController.findAllUsers();
        
        string customUsersRaw = Microsoft.Maui.Storage.Preferences.Default.Get("CustomShooters", "");
        if (!string.IsNullOrEmpty(customUsersRaw)) {
            var customUsers = customUsersRaw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(var cu in customUsers) {
                if (!users.Contains(cu)) users.Add(cu);
            }
        }
        
        if (!users.Contains("Invitado")) users.Insert(0, "Invitado");
        users.Add("+ Nuevo Tirador");
        
        string lastShooter = Microsoft.Maui.Storage.Preferences.Default.Get("LastShooter", "Invitado");
        if (users.Contains(lastShooter)) {
            _currentShooterName = lastShooter;
            if (_currentSession != null) {
                _currentSession.user = _currentShooterName;
            }
        }
        
        ShooterPicker.ItemsSource = users;
        ShooterPicker.SelectedItem = _currentShooterName;
        
        string lastScale = Microsoft.Maui.Storage.Preferences.Default.Get("LastScale", "100%");
        ScalePicker.SelectedItem = lastScale;
        SetScaleFactor(lastScale);
    }

    private void SaveCustomShooter(string newUser) {
        string customUsersRaw = Microsoft.Maui.Storage.Preferences.Default.Get("CustomShooters", "");
        var customUsers = customUsersRaw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!customUsers.Contains(newUser)) {
            customUsers.Add(newUser);
            Microsoft.Maui.Storage.Preferences.Default.Set("CustomShooters", string.Join("|", customUsers));
        }
    }

    private async void OnShooterChanged(object? sender, EventArgs e)
    {
        if (ShooterPicker.SelectedIndex == -1) return;
        
        string selectedUser = (string)ShooterPicker.SelectedItem;
        
        if (selectedUser == "+ Nuevo Tirador")
        {
            string newUser = await DisplayPromptAsync("Nuevo Tirador", "Introduce el nombre del nuevo tirador:", "Aceptar", "Cancelar");
            if (!string.IsNullOrWhiteSpace(newUser))
            {
                _currentShooterName = newUser;
                SaveCustomShooter(newUser);
                
                // Actualizar la UI
                var items = ShooterPicker.ItemsSource as List<string>;
                if (items != null)
                {
                    items.Insert(items.Count - 1, newUser);
                    ShooterPicker.ItemsSource = null; // Forza recarga visual
                    ShooterPicker.ItemsSource = items;
                }
                ShooterPicker.SelectedItem = newUser;
            }
            else
            {
                // Revert to previous selected
                ShooterPicker.SelectedItem = _currentShooterName;
            }
        }
        else
        {
            _currentShooterName = selectedUser;
        }
        
        Microsoft.Maui.Storage.Preferences.Default.Set("LastShooter", _currentShooterName);
        
        if (_currentSession != null)
        {
            _currentSession.user = _currentShooterName;
        }
    }

    private void OnScaleChanged(object? sender, EventArgs e)
    {
        if (ScalePicker.SelectedIndex == -1) return;
        string? selected = (string)ScalePicker.Items[ScalePicker.SelectedIndex];
        SetScaleFactor(selected);
        Microsoft.Maui.Storage.Preferences.Default.Set("LastScale", selected);
    }

    private void SetScaleFactor(string? scaleStr)
    {
        if (scaleStr == "80%") _targetScaleFactor = 0.8m;
        else if (scaleStr == "60%") _targetScaleFactor = 0.6m;
        else _targetScaleFactor = 1.0m;
    }


    private async void OnTargetInteraction(object? sender, TouchEventArgs e)
    {
        if (_currentSession.sessionType == Event.EventType.Practice && e.Touches.Length > 0)
        {
            var point = e.Touches[0];
            // Si el toque es en el cuadrante superior derecho (área del triángulo azul)
            if (point.X > TargetGraphicsView.Width / 2 && point.Y < TargetGraphicsView.Height / 2)
            {
                _currentSession.sessionType = Event.EventType.Match;
                _domainShots.Clear();
                _shotsUI.Clear();
                _shotCounter = 0;
                UpdateUI();
                
                _countdownTime = TimeSpan.FromMinutes(90);
                CountdownLabel.Text = _countdownTime.ToString(@"hh\:mm\:ss");
                _matchTimer.Start();
                
                await DisplayAlert("Modo Competición", "La competición ha iniciado. Dispones de 60 disparos.", "Aceptar");
            }
        }
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        var historyPage = new HistoryPage(_storageController, _currentShooterName);
        historyPage.OnSessionToLoad += (s, session) => {
            LoadSessionIntoMainPage(session);
        };
        await Navigation.PushModalAsync(historyPage);
    }

    private async void OnAnalyticsClicked(object sender, EventArgs e)
    {
        var analyticsPage = new AnalyticsPage();
        analyticsPage.InitializeWithStorage(_storageController, _currentShooterName);
        await Navigation.PushModalAsync(analyticsPage);
    }

    private void LoadSessionIntoMainPage(Session session)
    {
        _currentSession = session;
        _currentShooterName = session.user;
        
        if (ShooterPicker.Items.Contains(_currentShooterName))
        {
            ShooterPicker.SelectedItem = _currentShooterName;
        }
        
        _domainShots.Clear();
        _shotsUI.Clear();
        _shotCounter = 0;
        
        int tempCount = 0;
        foreach (var shot in session.Shots)
        {
            _domainShots.Add(shot);
            _shotCounter = Math.Max(_shotCounter, shot.count);
            
            _shotsUI.Add(new ShotViewModel
            {
                Shot = shot,
                DisplayText = shot.decimalScore.ToString("F1"),
                IsLatest = false,
                IsSum = false
            });
            
            tempCount++;
            if (tempCount % 10 == 0)
            {
                var seriesShots = _domainShots.Skip(tempCount - 10).Take(10);
                int intSum = seriesShots.Sum(s => s.score);
                _shotsUI.Add(new ShotViewModel { IsSum = true, DisplayText = intSum.ToString() });
            }
        }
        
        if (session.eventType != null && session.eventType.Target != null) {
            _currentTarget = session.eventType.Target;
        }
        
        AppTargetDrawable.Target = _currentTarget;
        AppTargetDrawable.CurrentSession = _currentSession;
        
        UpdateUI();

        if (session.sessionType == Event.EventType.Match)
        {
            double savedSeconds = Microsoft.Maui.Storage.Preferences.Default.Get($"Timer_{session.startTime.Ticks}", 90 * 60.0);
            _countdownTime = TimeSpan.FromSeconds(savedSeconds);
            CountdownLabel.Text = _countdownTime.ToString(@"hh\:mm\:ss");
            _matchTimer.Start();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _storageController.InitializeAsync();
        LoadShooters();
    }
}
