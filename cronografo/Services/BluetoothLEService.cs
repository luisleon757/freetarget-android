using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace cronografo.Services
{
    public class BluetoothLEService : IBluetoothLEService
    {
        private readonly IBluetoothLE _bluetooth = CrossBluetoothLE.Current;
        private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;
        
        private IDevice? _connectedDevice;
        private IService? _uartService;
        private ICharacteristic? _txCharacteristic;
        private ICharacteristic? _rxCharacteristic;

        private const string ServiceUuidStr = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
        private const string TxUuidStr      = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";
        private const string RxUuidStr      = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E";

        public event Action<string>? DataReceived;

        public bool IsConnected => _connectedDevice != null && _connectedDevice.State == DeviceState.Connected;
        public IDevice? ConnectedDevice => _connectedDevice;

        public BluetoothLEService()
        {
            _adapter.DeviceDisconnected += OnDeviceDisconnected;
        }

        public async Task StartScanningAsync(Action<IDevice> onDeviceFound)
        {
            if (!_bluetooth.IsOn)
            {
                Debug.WriteLine("El Bluetooth está apagado.");
                return;
            }

            _adapter.DeviceDiscovered += (s, e) =>
            {
                if (e.Device != null)
                {
                    onDeviceFound(e.Device);
                }
            };

            // Escanear por 10 segundos o hasta que se detenga
            _adapter.ScanTimeout = 10000;
            _adapter.ScanMode = ScanMode.Balanced;
            await _adapter.StartScanningForDevicesAsync();
        }

        public async Task StopScanningAsync()
        {
            if (_adapter.IsScanning)
            {
                await _adapter.StopScanningForDevicesAsync();
            }
        }

        public async Task<bool> ConnectDeviceAsync(IDevice device)
        {
            try
            {
                await StopScanningAsync();
                
                await _adapter.ConnectToDeviceAsync(device);
                _connectedDevice = device;

                // Buscar servicio UART
                _uartService = await _connectedDevice.GetServiceAsync(Guid.Parse(ServiceUuidStr));
                if (_uartService == null)
                {
                    Debug.WriteLine("Servicio UART no encontrado.");
                    await DisconnectDeviceAsync();
                    return false;
                }

                // Obtener características
                _txCharacteristic = await _uartService.GetCharacteristicAsync(Guid.Parse(TxUuidStr));
                _rxCharacteristic = await _uartService.GetCharacteristicAsync(Guid.Parse(RxUuidStr));

                if (_txCharacteristic == null || _rxCharacteristic == null)
                {
                    Debug.WriteLine("Características TX/RX no encontradas.");
                    await DisconnectDeviceAsync();
                    return false;
                }

                // Suscribirse a las notificaciones
                _txCharacteristic.ValueUpdated += OnTxValueUpdated;
                await _txCharacteristic.StartUpdatesAsync();

                Debug.WriteLine($"Conectado exitosamente a {_connectedDevice.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al conectar: {ex.Message}");
                await DisconnectDeviceAsync();
                return false;
            }
        }

        public async Task DisconnectDeviceAsync()
        {
            if (_connectedDevice == null) return;

            try
            {
                if (_txCharacteristic != null)
                {
                    _txCharacteristic.ValueUpdated -= OnTxValueUpdated;
                    await _txCharacteristic.StopUpdatesAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al detener actualizaciones: {ex.Message}");
            }

            try
            {
                await _adapter.DisconnectDeviceAsync(_connectedDevice);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al desconectar: {ex.Message}");
            }

            _txCharacteristic = null;
            _rxCharacteristic = null;
            _uartService = null;
            _connectedDevice = null;
        }

        public async Task SendCommandAsync(string command)
        {
            if (!IsConnected || _rxCharacteristic == null) return;

            try
            {
                // Asegurar que el comando termina con salto de línea
                if (!command.EndsWith("\n"))
                {
                    command += "\n";
                }

                byte[] data = Encoding.UTF8.GetBytes(command);
                await _rxCharacteristic.WriteAsync(data);
                Debug.WriteLine($"Comando enviado: {command.Trim()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al enviar comando: {ex.Message}");
            }
        }

        private void OnTxValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
        {
            if (e.Characteristic?.Value != null)
            {
                string text = Encoding.UTF8.GetString(e.Characteristic.Value);
                DataReceived?.Invoke(text);
            }
        }

        private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
        {
            if (_connectedDevice != null && e.Device?.Id == _connectedDevice.Id)
            {
                Debug.WriteLine("Dispositivo desconectado externamente.");
                _txCharacteristic = null;
                _rxCharacteristic = null;
                _uartService = null;
                _connectedDevice = null;
            }
        }
    }
}
