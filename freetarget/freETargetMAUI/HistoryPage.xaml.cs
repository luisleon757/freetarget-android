using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using freETarget;

namespace freETargetMAUI;

public partial class HistoryPage : ContentPage
{
    private StorageController _storageController;
    private string _targetUser;

    public event EventHandler<Session> OnSessionToLoad;

    public HistoryPage(StorageController storageController, string targetUser)
    {
        InitializeComponent();
        _storageController = storageController;
        _targetUser = targetUser;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ShooterLabel.Text = $"Historial de: {_targetUser}";
        RefreshList();
    }

    private void RefreshList()
    {
        var sessions = _storageController.findAllSessionSummariesForUser(_targetUser);
        SessionsList.ItemsSource = sessions;
    }

    private void OnSessionSelected(object sender, SelectionChangedEventArgs e)
    {
        ((CollectionView)sender).SelectedItem = null; // deselect visually
    }

    private async void OnLoadSessionClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        if (button != null && button.CommandParameter is long sessionId)
        {
            var fullSession = _storageController.findSession(sessionId);
            if (fullSession != null)
            {
                OnSessionToLoad?.Invoke(this, fullSession);
                await Navigation.PopModalAsync();
            }
            else
            {
                await DisplayAlert("Error", "No se pudo recuperar la sesión o el evento base de la base de datos.", "Aceptar");
            }
        }
    }

    private async void OnDeleteSessionClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        if (button != null && button.CommandParameter is long sessionId)
        {
            bool answer = await DisplayAlert("Confirmar", "¿Seguro que quieres borrar esta sesión de forma permanente?", "Sí, Borrar", "Cancelar");
            if (answer)
            {
                _storageController.deleteSession(sessionId);
                RefreshList();
            }
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
