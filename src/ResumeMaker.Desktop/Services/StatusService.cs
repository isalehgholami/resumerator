using CommunityToolkit.Mvvm.ComponentModel;

namespace ResumeMaker.Desktop.Services;

/// <summary>Status bar / toast message shared by all view-models.</summary>
public sealed partial class StatusService : ObservableObject
{
    private CancellationTokenSource? _clear;

    public TimeSpan ClearAfter { get; set; } = TimeSpan.FromSeconds(8);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string _message = "";

    [ObservableProperty]
    private bool _isError;

    public bool HasMessage => Message.Length > 0;

    public void Info(string message) => Show(message, false);

    public void Error(string message) => Show(message, true);

    public void Clear() => Show("", false);

    private void Show(string message, bool isError)
    {
        _clear?.Cancel();
        IsError = isError;
        Message = message;
        if (message.Length == 0 || ClearAfter <= TimeSpan.Zero)
            return;
        var cts = _clear = new CancellationTokenSource();
        _ = ClearLaterAsync(message, cts.Token);
    }

    private async Task ClearLaterAsync(string message, CancellationToken ct)
    {
        try
        {
            await Task.Delay(ClearAfter, ct);
            if (Message == message)
                Message = "";
        }
        catch (OperationCanceledException) { }
    }
}
