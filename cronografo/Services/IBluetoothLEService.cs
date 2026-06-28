using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.Contracts;

namespace cronografo.Services
{
    public interface IBluetoothLEService
    {
        Task StartScanningAsync(Action<IDevice> onDeviceFound);
        Task StopScanningAsync();
        Task<bool> ConnectDeviceAsync(IDevice device);
        Task DisconnectDeviceAsync();
        Task SendCommandAsync(string command);
        event Action<string>? DataReceived;
        bool IsConnected { get; }
        IDevice? ConnectedDevice { get; }
    }
}
