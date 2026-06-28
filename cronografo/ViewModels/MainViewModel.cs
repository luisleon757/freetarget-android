using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using cronografo.Models;
using cronografo.Services;
using Plugin.BLE.Abstractions.Contracts;

namespace cronografo.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IBluetoothLEService _bleService;
        private string _statusText = "Desconectado";
        private bool _isScanning;
        private bool _isConnected;
        private string _pelletWeightText = "0.52";
        private string _lastShotNumber = "-";
        private string _lastShotVelocity = "0.0";
        private string _lastShotEnergy = "0.0";

        private string _avgVelocity = "0.0";
        private string _minVelocity = "0.0";
        private string _maxVelocity = "0.0";
        private string _avgEnergy = "0.0";
        private string _minEnergy = "0.0";
        private string _maxEnergy = "0.0";

        private string _receivedBuffer = "";
        
        // Estado del parser
        private int? _currentParsingNumber;
        private float? _currentParsingVelocity;
        private float? _currentParsingEnergy;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<IDevice> Devices { get; } = new();
        public ObservableCollection<ShotModel> Shots { get; } = new();

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public string PelletWeightText
        {
            get => _pelletWeightText;
            set => SetProperty(ref _pelletWeightText, value);
        }

        public string LastShotNumber
        {
            get => _lastShotNumber;
            set => SetProperty(ref _lastShotNumber, value);
        }

        public string LastShotVelocity
        {
            get => _lastShotVelocity;
            set => SetProperty(ref _lastShotVelocity, value);
        }

        public string LastShotEnergy
        {
            get => _lastShotEnergy;
            set => SetProperty(ref _lastShotEnergy, value);
        }

        public string AvgVelocity
        {
            get => _avgVelocity;
            set => SetProperty(ref _avgVelocity, value);
        }

        public string MinVelocity
        {
            get => _minVelocity;
            set => SetProperty(ref _minVelocity, value);
        }

        public string MaxVelocity
        {
            get => _maxVelocity;
            set => SetProperty(ref _maxVelocity, value);
        }

        public string AvgEnergy
        {
            get => _avgEnergy;
            set => SetProperty(ref _avgEnergy, value);
        }

        public string MinEnergy
        {
            get => _minEnergy;
            set => SetProperty(ref _minEnergy, value);
        }

        public string MaxEnergy
        {
            get => _maxEnergy;
            set => SetProperty(ref _maxEnergy, value);
        }

        public ICommand ScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand UpdateWeightCommand { get; }
        public ICommand ResetCommand { get; }

        public MainViewModel(IBluetoothLEService bleService)
        {
            _bleService = bleService;
            _bleService.DataReceived += OnDataReceived;

            ScanCommand = new Command(async () => await ExecuteScanCommand());
            ConnectCommand = new Command<IDevice>(async (device) => await ExecuteConnectCommand(device));
            DisconnectCommand = new Command(async () => await ExecuteDisconnectCommand());
            UpdateWeightCommand = new Command(async () => await ExecuteUpdateWeightCommand());
            ResetCommand = new Command(async () => await ExecuteResetCommand());
        }

        private async Task ExecuteScanCommand()
        {
            if (IsScanning) return;

            IsScanning = true;
            StatusText = "Escaneando dispositivos...";
            Devices.Clear();

            try
            {
                await _bleService.StartScanningAsync((device) =>
                {
                    // Filtrar dispositivos sin nombre o duplicados
                    if (!string.IsNullOrEmpty(device.Name) && Devices.All(d => d.Id != device.Id))
                    {
                        // Priorizar CronoABC
                        if (device.Name.Equals("CronoABC", StringComparison.OrdinalIgnoreCase))
                        {
                            Devices.Insert(0, device);
                        }
                        else
                        {
                            Devices.Add(device);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Error al escanear: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                if (!IsConnected)
                {
                    StatusText = "Búsqueda finalizada.";
                }
            }
        }

        private async Task ExecuteConnectCommand(IDevice device)
        {
            if (device == null) return;

            StatusText = $"Conectando a {device.Name}...";
            await _bleService.StopScanningAsync();

            bool success = await _bleService.ConnectDeviceAsync(device);
            if (success)
            {
                IsConnected = true;
                StatusText = $"Conectado a {device.Name}";
                
                // Cargar estadísticas iniciales o peso
                await Task.Delay(500);
                await _bleService.SendCommandAsync("peso");
            }
            else
            {
                IsConnected = false;
                StatusText = "Error al intentar conectar.";
            }
        }

        private async Task ExecuteDisconnectCommand()
        {
            StatusText = "Desconectando...";
            await _bleService.DisconnectDeviceAsync();
            IsConnected = false;
            StatusText = "Desconectado";
        }

        private async Task ExecuteUpdateWeightCommand()
        {
            if (!IsConnected) return;

            if (float.TryParse(PelletWeightText, out float weight) && weight > 0)
            {
                await _bleService.SendCommandAsync($"peso {weight:F2}");
                StatusText = $"Peso actualizado a {weight:F2}g en el hardware.";
            }
            else
            {
                StatusText = "Masa inválida. Introduce un número mayor a 0.";
            }
        }

        private async Task ExecuteResetCommand()
        {
            if (IsConnected)
            {
                await _bleService.SendCommandAsync("reset");
            }

            Shots.Clear();
            RecalculateStatistics();
            LastShotNumber = "-";
            LastShotVelocity = "0.0";
            LastShotEnergy = "0.0";
            StatusText = "Sesión reiniciada.";
        }

        private void OnDataReceived(string text)
        {
            _receivedBuffer += text;
            ProcessBuffer();
        }

        private void ProcessBuffer()
        {
            while (true)
            {
                int newlineIdx = _receivedBuffer.IndexOf('\n');
                if (newlineIdx == -1) break;

                string line = _receivedBuffer.Substring(0, newlineIdx).Trim();
                _receivedBuffer = _receivedBuffer.Substring(newlineIdx + 1);

                if (string.IsNullOrEmpty(line)) continue;

                ParseLine(line);
            }
        }

        private void ParseLine(string line)
        {
            // Procesamiento de comandos informativos devueltos
            if (line.StartsWith("Nuevo peso:", StringComparison.OrdinalIgnoreCase) || 
                line.StartsWith("Peso actual:", StringComparison.OrdinalIgnoreCase))
            {
                // Extraer el peso del mensaje para actualizar la UI
                var match = Regex.Match(line, @"\d+(\.\d+)?");
                if (match.Success)
                {
                    PelletWeightText = match.Value;
                }
                return;
            }

            if (line.StartsWith("Estadísticas reiniciadas", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Parsing de disparos
            // Ejemplo:
            // Disparo #1
            //  250.5 m/s
            //  16.3 Jul
            if (line.StartsWith("Disparo #", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(line.Substring(9), out int num))
                {
                    _currentParsingNumber = num;
                }
            }
            else if (line.EndsWith("m/s", StringComparison.OrdinalIgnoreCase))
            {
                string valStr = line.Replace("m/s", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (float.TryParse(valStr, out float vel))
                {
                    _currentParsingVelocity = vel;
                }
            }
            else if (line.EndsWith("Jul", StringComparison.OrdinalIgnoreCase))
            {
                string valStr = line.Replace("Jul", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (float.TryParse(valStr, out float nrg))
                {
                    _currentParsingEnergy = nrg;
                }
            }

            // Si ya tenemos los tres componentes del disparo actual, guardarlo e informar
            if (_currentParsingNumber.HasValue && _currentParsingVelocity.HasValue && _currentParsingEnergy.HasValue)
            {
                var newShot = new ShotModel
                {
                    Number = _currentParsingNumber.Value,
                    Velocity = _currentParsingVelocity.Value,
                    Energy = _currentParsingEnergy.Value
                };

                // Insertar al inicio de la lista para ver el más nuevo primero
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Shots.Insert(0, newShot);
                    
                    LastShotNumber = $"Disparo #{newShot.Number}";
                    LastShotVelocity = $"{newShot.Velocity:F1}";
                    LastShotEnergy = $"{newShot.Energy:F1}";
                    
                    RecalculateStatistics();

                    // Anuncio por voz
                    AnnounceShot(newShot);
                });

                // Resetear estado del parser
                _currentParsingNumber = null;
                _currentParsingVelocity = null;
                _currentParsingEnergy = null;
            }
        }

        private void AnnounceShot(ShotModel shot)
        {
            // Decir velocidad en formato amigable
            // Reemplazar el punto decimal por "punto" para una pronunciación natural en español
            string velocidadPronunciar = shot.Velocity.ToString("F1").Replace(",", " punto ").Replace(".", " punto ");
            string mensaje = $"{shot.Velocity:F0} metros por segundo";
            
            // Usar TextToSpeech de MAUI
            Task.Run(async () =>
            {
                try
                {
                    await TextToSpeech.Default.SpeakAsync(mensaje, new SpeechOptions
                    {
                        Volume = 1.0f,
                        Locale = (await TextToSpeech.Default.GetLocalesAsync())
                                    .FirstOrDefault(l => l.Language.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error de TextToSpeech: {ex.Message}");
                }
            });
        }

        private void RecalculateStatistics()
        {
            if (Shots.Count == 0)
            {
                AvgVelocity = "0.0";
                MinVelocity = "0.0";
                MaxVelocity = "0.0";
                AvgEnergy = "0.0";
                MinEnergy = "0.0";
                MaxEnergy = "0.0";
                return;
            }

            var velocities = Shots.Select(s => s.Velocity).ToList();
            var energies = Shots.Select(s => s.Energy).ToList();

            AvgVelocity = $"{velocities.Average():F1}";
            MinVelocity = $"{velocities.Min():F1}";
            MaxVelocity = $"{velocities.Max():F1}";

            AvgEnergy = $"{energies.Average():F1}";
            MinEnergy = $"{energies.Min():F1}";
            MaxEnergy = $"{energies.Max():F1}";
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
