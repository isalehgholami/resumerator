using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using ResumeMaker.Desktop.Localization;

namespace ResumeMaker.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplyFlow();
        Localizer.Instance.PropertyChanged += OnLocalizerChanged;
    }

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Localizer.IsRtl))
            ApplyFlow();
    }

    private void ApplyFlow() =>
        FlowDirection = Localizer.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
