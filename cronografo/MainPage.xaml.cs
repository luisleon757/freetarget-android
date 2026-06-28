using System;
using System.Threading.Tasks;
using cronografo.ViewModels;
using Microsoft.Maui.Controls;

namespace cronografo
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CheckAndRequestBluetoothPermissions();
        }

        private async Task CheckAndRequestBluetoothPermissions()
        {
            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.Bluetooth>();
                }

                // También se necesita ubicación (para escaneo BLE en versiones previas a Android 12)
                var locationStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (locationStatus != PermissionStatus.Granted)
                {
                    locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al solicitar permisos: {ex.Message}");
            }
        }
    }
}
