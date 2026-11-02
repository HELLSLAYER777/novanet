using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovaNet.Models;
using NovaNet.Services;
using System.Collections.ObjectModel;

namespace NovaNet.ViewModels;

/// <summary>ViewModel for the Optimize page.</summary>
public sealed partial class OptimizeViewModel : ViewModelBase
{
    readonly ObservableCollection<TweakViewModel> _tweaks = new();

    public ObservableCollection<TweakViewModel> Tweaks => _tweaks;

    [ObservableProperty]
    private int _appliedCount;

    [ObservableProperty]
    private int _pendingCount;

    public OptimizeViewModel()
    {
        LoadTweaks();
    }

    void LoadTweaks()
    {
        _tweaks.Clear();
        foreach (var t in Tweaks.All)
        {
            bool applied = Store.Data.Applied.Contains(t.Id);
            _tweaks.Add(new TweakViewModel(t, applied));
        }
        UpdateCounts();
    }

    [RelayCommand]
    async Task ApplyAllAsync()
    {
        await ExecuteAsync(async () =>
        {
            foreach (var vm in _tweaks.Where(v => !v.IsOn))
            {
                vm.IsOn = true;
                await Task.Delay(50); // Small delay to avoid overwhelming
            }
            UpdateCounts();
        }, "Applying all tweaks...");
    }

    [RelayCommand]
    async Task RevertAllAsync()
    {
        await ExecuteAsync(async () =>
        {
            foreach (var vm in _tweaks.Where(v => v.IsOn))
            {
                vm.IsOn = false;
                await Task.Delay(50);
            }
            UpdateCounts();
        }, "Reverting all tweaks...");
    }

    [RelayCommand]
    void Refresh() => LoadTweaks();

    void UpdateCounts()
    {
        AppliedCount = _tweaks.Count(v => v.IsOn);
        PendingCount = _tweaks.Count(v => !v.IsOn);
    }

    partial void OnAppliedCountChanged(int value) => OnPropertyChanged(nameof(AppliedCount));
    partial void OnPendingCountChanged(int value) => OnPropertyChanged(nameof(PendingCount));
}