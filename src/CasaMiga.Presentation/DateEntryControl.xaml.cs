using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        Editor.PreviewTextInput+=EditorTextInput;
        Editor.PreviewKeyDown+=EditorKeyDown;
        DataObject.AddPastingHandler(Editor,EditorPaste);
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
    private void EditorTextInput(object sender,TextCompositionEventArgs e)
    {
        e.Handled=true;
        if(e.Text.Length==0||e.Text.Any(value=>!char.IsDigit(value)))return;
        ApplyEdit(DateTextMask.Insert(Editor.Text,Editor.SelectionStart,Editor.SelectionLength,e.Text));
    }
    private void EditorKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key is not (Key.Back or Key.Delete))return;
        e.Handled=true;
        ApplyEdit(e.Key==Key.Back
            ? DateTextMask.Backspace(Editor.Text,Editor.SelectionStart,Editor.SelectionLength)
            : DateTextMask.Delete(Editor.Text,Editor.SelectionStart,Editor.SelectionLength));
    }
    private void EditorPaste(object sender,DataObjectPastingEventArgs e)
    {
        var pasted=e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText,true)
            ? e.SourceDataObject.GetData(DataFormats.UnicodeText,true) as string
            : e.SourceDataObject.GetData(DataFormats.Text,true) as string;
        e.CancelCommand();
        if(!DateTextMask.TryNormalizePaste(pasted,out var digits))return;
        ApplyEdit(DateTextMask.Insert(Editor.Text,Editor.SelectionStart,Editor.SelectionLength,digits));
    }
    private void ApplyEdit(DateMaskEdit edit)
    {
        SetCurrentValue(TextProperty,edit.Text);
        Editor.SetCurrentValue(TextBox.TextProperty,edit.Text);
        Editor.SelectionStart=Math.Clamp(edit.Caret,0,Editor.Text.Length);
        Editor.SelectionLength=0;
    }
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
        SetCurrentValue(TextProperty,selected.ToString("dd/MM/yyyy",CultureInfo.InvariantCulture));
        CalendarPopup.IsOpen=false;
        Editor.Focus();
        Editor.CaretIndex=Editor.Text.Length;
    }
}

internal readonly record struct DateMaskEdit(string Text,int Caret);

internal static class DateTextMask
{
    internal static string Format(string digits)
    {
        var value=new string(digits.Where(char.IsDigit).Take(8).ToArray());
        if(value.Length<=2)return value;
        if(value.Length<=4)return $"{value[..2]}/{value[2..]}";
        return $"{value[..2]}/{value[2..4]}/{value[4..]}";
    }

    internal static DateMaskEdit Insert(string current,int selectionStart,int selectionLength,string inserted)
    {
        var digits=Digits(current);var start=DigitIndex(current,selectionStart);var end=DigitIndex(current,selectionStart+selectionLength);
        var room=8-(digits.Length-(end-start));var addition=new string(inserted.Where(char.IsDigit).Take(Math.Max(0,room)).ToArray());
        var result=digits[..start]+addition+digits[end..];return Result(result,start+addition.Length);
    }

    internal static DateMaskEdit Backspace(string current,int selectionStart,int selectionLength)
    {
        var digits=Digits(current);var start=DigitIndex(current,selectionStart);var end=DigitIndex(current,selectionStart+selectionLength);
        if(end>start)return Result(digits.Remove(start,end-start),start);
        if(start==0)return Result(digits,0);
        return Result(digits.Remove(start-1,1),start-1);
    }

    internal static DateMaskEdit Delete(string current,int selectionStart,int selectionLength)
    {
        var digits=Digits(current);var start=DigitIndex(current,selectionStart);var end=DigitIndex(current,selectionStart+selectionLength);
        if(end>start)return Result(digits.Remove(start,end-start),start);
        if(start>=digits.Length)return Result(digits,start);
        return Result(digits.Remove(start,1),start);
    }

    internal static bool TryNormalizePaste(string? value,out string digits)
    {
        digits="";if(string.IsNullOrEmpty(value))return false;
        if(value.All(char.IsDigit))
        {
            if(value.Length>8)return false;digits=value;return true;
        }
        var extracted=Digits(value);
        if(extracted.Length>8||Format(extracted)!=value)return false;
        digits=extracted;return true;
    }

    private static string Digits(string value)=>new(value.Where(char.IsDigit).Take(8).ToArray());
    private static int DigitIndex(string value,int characterIndex)=>value.Take(Math.Clamp(characterIndex,0,value.Length)).Count(char.IsDigit);
    private static DateMaskEdit Result(string digits,int digitCaret)
    {
        var text=Format(digits);if(digitCaret<=0)return new(text,0);
        var seen=0;
        for(var index=0;index<text.Length;index++)if(char.IsDigit(text[index])&&++seen==digitCaret)return new(text,index+1);
        return new(text,text.Length);
    }
}
