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
    private Random _random = new Random();
    private TargetConnectionService _connectionService;

    public MainPage()
    {
        InitializeComponent();
        
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
        _currentSession = Session.createNewSession(ev, "Test User");
        
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
    }

    private void OnStartEventClicked(object? sender, EventArgs e)
    {
        // Placeholder for Event Configuration dialog or screen
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        if (_connectionService.IsConnected)
        {
            _connectionService.Disconnect();
            ConnectButton.Text = "Conectar WiFi";
            ConnectButton.BackgroundColor = Color.FromArgb("#10B981"); // Green
            ConnectionStatusDot.Fill = Color.FromArgb("#EF4444"); // Red
        }
        else
        {
            await _connectionService.ConnectAsync();
            if (_connectionService.IsConnected)
            {
                ConnectButton.Text = "Desconectar";
                ConnectButton.BackgroundColor = Color.FromArgb("#EF4444"); // Red
                ConnectionStatusDot.Fill = Color.FromArgb("#10B981"); // Green
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
        
        string? selected = LightIntensityPicker.Items[LightIntensityPicker.SelectedIndex];
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
}
