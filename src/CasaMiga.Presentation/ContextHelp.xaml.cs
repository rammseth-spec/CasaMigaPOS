using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CasaMiga.Presentation;

// Infrastructure only: operational help text is assigned by each future subphase.
public partial class ContextHelp : UserControl
{
    public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(
        nameof(HelpText), typeof(string), typeof(ContextHelp), new PropertyMetadata(""));
    public string HelpText { get => (string)GetValue(HelpTextProperty); set => SetValue(HelpTextProperty, value); }
    public ContextHelp() => InitializeComponent();
    private void HelpKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || !HelpPanel.IsExpanded) return;
        HelpPanel.IsExpanded = false;
        args.Handled = true;
    }
}
