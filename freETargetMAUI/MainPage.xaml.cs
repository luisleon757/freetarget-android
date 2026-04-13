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
        
        SetupInitialEvent();
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
            ProcessRealShot(e.X, e.Y, e.ShotNumber);
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
        
        ShooterPicker.ItemsSource = users;
        ShooterPicker.SelectedItem = _currentShooterName;
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
        
        if (_currentSession != null)
        {
            _currentSession.user = _currentShooterName;
        }
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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _storageController.InitializeAsync();
        LoadShooters();
    }
}
