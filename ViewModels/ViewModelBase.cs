using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace NovaNet.ViewModels;

/// <summary>Base class for all ViewModels with common functionality.</summary>
public abstract partial class ViewModelBase : ObservableObject, IDisposable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    protected ViewModelBase()
    {
        PropertyChanged += OnPropertyChanged;
    }

    protected virtual void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) { }

    /// <summary>Executes an async operation with busy/error handling.</summary>
    protected async Task ExecuteAsync(Func<Task> operation, string? busyMessage = null, bool showError = true)
    {
        if (IsBusy) return;

        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        if (busyMessage != null) StatusMessage = busyMessage;

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            if (showError)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
            if (busyMessage != null) StatusMessage = string.Empty;
        }
    }

    /// <summary>Executes an async operation with result and busy/error handling.</summary>
    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string? busyMessage = null, bool showError = true)
    {
        if (IsBusy) return default;

        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;
        if (busyMessage != null) StatusMessage = busyMessage;

        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            if (showError)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            return default;
        }
        finally
        {
            IsBusy = false;
            if (busyMessage != null) StatusMessage = string.Empty;
        }
    }

    public virtual void Dispose()
    {
        PropertyChanged -= OnPropertyChanged;
        GC.SuppressFinalize(this);
    }
}