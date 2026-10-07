using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CasaMiga.Presentation;

public partial class DateEntryControl : UserControl
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(
        nameof(Text),typeof(string),typeof(DateEntryControl),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,TextChanged));
    public static readonly DependencyProperty HasErrorProperty=DependencyProperty.Register(
        nameof(HasError),typeof(bool),typeof(DateEntryControl),new PropertyMetadata(false,ErrorChanged));
    public static readonly DependencyProperty AutomationNameProperty=DependencyProperty.Register(
        nameof(AutomationName),typeof(string),typeof(DateEntryControl),new PropertyMetadata("Fecha"));

    public DateEntryControl()
    {
        InitializeComponent();
        Editor.LostKeyboardFocus+=(_,_)=>Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,() =>
        {
            if(!IsKeyboardFocusWithin&&!CalendarPopup.IsOpen)EntryCommitted?.Invoke(this,EventArgs.Empty);
        });
        UpdateVisualState();
    }

    public string Text {get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    public bool HasError {get=>(bool)GetValue(HasErrorProperty);set=>SetValue(HasErrorProperty,value);}
    public string AutomationName {get=>(string)GetValue(AutomationNameProperty);set=>SetValue(AutomationNameProperty,value);}
    public event EventHandler? EntryCommitted;

    private static void TextChanged(DependencyObject value,DependencyPropertyChangedEventArgs _)=>((DateEntryControl)value).UpdateVisualState();
    private static void ErrorChanged(DependencyObject value,DependencyPropertyChangedEventArgs _)=>((DateEntryControl)value).UpdateVisualState();
    private void UpdateVisualState()
    {
        if(!IsInitialized)return;
        DateHint.Visibility=string.IsNullOrEmpty(Text)?Visibility.Visible:Visibility.Collapsed;
        FieldBorder.BorderBrush=(System.Windows.Media.Brush)FindResource(HasError?"ErrorBrush":"BorderBrush");
        FieldBorder.BorderThickness=HasError?new Thickness(2):new Thickness(1);
    }
    private void OpenCalendar(object sender,RoutedEventArgs e)
    {
        if(DateTime.TryParseExact(Text?.Trim(),"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out var selected))
        {
            PickerCalendar.SelectedDate=selected;
            PickerCalendar.DisplayDate=selected;
        }
        else PickerCalendar.SelectedDate=null;
        CalendarPopup.IsOpen=true;
    }
    private void CalendarSelected(object? sender,SelectionChangedEventArgs e)
    {
        if(PickerCalendar.SelectedDate is not { } selected)return;
        Text=selected.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture);
        CalendarPopup.IsOpen=false;
        Editor.Focus();
        Editor.CaretIndex=Editor.Text.Length;
    }
}
