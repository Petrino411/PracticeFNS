using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NTFSChecker.Core.Interfaces;

namespace NTFSChecker.AvaloniaUI.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    private readonly IDirectoryChecker _directoryChecker;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IUserGroupHelper _reGroupHelper;
    private CancellationTokenSource? _cts;


    private int _dirs;
    private int _files;
    private readonly Stopwatch _stopwatch = new();
    [ObservableProperty] private string labelText;
    [ObservableProperty] private string labelTimer;
    [ObservableProperty] private double progressMax;
    [ObservableProperty] private double progressValue;

    [ObservableProperty] private string selectedFolderPath;
    [ObservableProperty] private int? selectedLogInd;

    public MainPageViewModel(IDirectoryChecker directoryChecker, IUserGroupHelper reGroupHelper,
        ILogger<MainWindowViewModel> logger)
    {
        _directoryChecker = directoryChecker;
        _reGroupHelper = reGroupHelper;
        _logger = logger;

        ProgressValue = 0;
        ProgressMax = 100;
    }


    public ObservableCollection<string> Logs { get; } = new();


    [RelayCommand]
    private async Task SelectFolderAsync(Window window)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку"
            };

            var result = await Task.Run(() => dialog.ShowAsync(window));

            if (!string.IsNullOrEmpty(result))
            {
                SelectedFolderPath = result;
                Logs.Add($"Выбрана папка: {SelectedFolderPath}");
            }
        }
        else
        {
            var topLevel = TopLevel.GetTopLevel(window);
            if (topLevel?.StorageProvider is { CanOpen: true } storageProvider)
            {
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Выберите папку",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    SelectedFolderPath = folders[0].Path.LocalPath;
                    Logs.Add($"Выбрана папка: {SelectedFolderPath}");
                }
            }
            else
            {
                Logs.Add("Платформа не поддерживает выбор папки.");
            }
        }
    }

    [RelayCommand]
    public async Task CheckDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath))
            return;

        Logs.Clear();
        ProgressValue = 0;
        _dirs = _files = 0;
        CountItems(SelectedFolderPath);
        Logs.Add("Проверка началась...");
        _stopwatch.Restart();
        _cts = new CancellationTokenSource();
        UpdateTimerAsync(_cts.Token);
        try
        {
            await _directoryChecker.CheckDirectoryAsync(SelectedFolderPath,
                LogToUIAsync,
                UpdateProgressAsync);
            Logs.Add("Операция прошла успешно");
        }
        catch (Exception ex)
        {
            Logs.Add($"Ошибка: {ex.Message}");
            _logger.LogError(ex, "Ошибка проверки директории");
        }
        finally
        {
            _stopwatch.Stop();
            _cts?.Cancel();
        }
    }


    private async Task CountItems(string path)
    {
        Task.Run(async () =>
        {
            int totalItems = 0;

            void CountRecursive(string currentPath)
            {
                try
                {
                    totalItems += Directory.GetFiles(currentPath).Length;
                    
                    foreach (var dir in Directory.GetDirectories(currentPath))
                    {
                        totalItems++; 
                        CountRecursive(dir);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
            }

            CountRecursive(path);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressMax = totalItems + 1;
                ProgressValue = _files + _dirs;
            });
        });
    }

    private async Task UpdateProgressAsync(int dirs, int files)
    {
        _dirs = dirs;
        _files = files;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressValue = _dirs + _files;
            LabelText = $"Проверено: папок: {_dirs}, файлов: {_files}";
        });
    }

    private async Task LogToUIAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { Logs.Add(message); });
        SelectedLogInd = Logs.Count - 1;
        _logger.LogInformation(message); 
    }

    private async Task UpdateTimerAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var elapsed = _stopwatch.Elapsed;
                LabelTimer = $"{elapsed:hh\\:mm\\:ss\\.fff}";
                await Task.Delay(100, token);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }
}