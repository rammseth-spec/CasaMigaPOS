using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CasaMiga.Presentation;

public partial class OperationCenterView : UserControl
{
    public OperationCenterView()
    {
        InitializeComponent();
        Loaded+=(_,_)=>EnsureDefaults();
    }
    private OperationCenterViewModel Model=>(OperationCenterViewModel)DataContext;
    private void CloseClick(object sender,RoutedEventArgs e)=>Model.ClosePanel();
    private void PresetClick(object sender,RoutedEventArgs e)
    {
        var minutes=int.Parse((string)((Button)sender).Tag,CultureInfo.InvariantCulture);
        TimerHours.Text=(minutes/60).ToString(CultureInfo.InvariantCulture);
        TimerMinutes.Text=(minutes%60).ToString(CultureInfo.InvariantCulture);
        TimerName.Focus();
    }
    private void CreateTimerClick(object sender,RoutedEventArgs e)
    {
        var icon=TimerIcon.SelectedValue is OperationIconKind selectedIcon?selectedIcon:OperationIconKind.Clock;
        var priority=TimerPriority.SelectedValue is OperationPriority selectedPriority?selectedPriority:OperationPriority.Normal;
        if(!Model.CreateTimer(TimerName.Text,TimerHours.Text,TimerMinutes.Text,icon,priority))return;
        TimerName.Clear();TimerHours.Text="0";TimerMinutes.Text="10";TimerName.Focus();
    }
    private OperationTimerViewModel Timer(object sender)=>(OperationTimerViewModel)((FrameworkElement)sender).DataContext;
    private void PauseTimerClick(object sender,RoutedEventArgs e)=>Model.PauseTimer(Timer(sender));
    private void ResumeTimerClick(object sender,RoutedEventArgs e)=>Model.ResumeTimer(Timer(sender));
    private void ResetTimerClick(object sender,RoutedEventArgs e)=>Model.ResetTimer(Timer(sender));
    private void AttendTimerClick(object sender,RoutedEventArgs e)=>Model.AttendTimer(Timer(sender));
    private void DeleteTimerClick(object sender,RoutedEventArgs e)=>Model.DeleteTimer(Timer(sender));
    private OperationNoteViewModel Note(object sender)=>(OperationNoteViewModel)((FrameworkElement)sender).DataContext;
    private void SaveNoteClick(object sender,RoutedEventArgs e)
    {
        var icon=NoteIcon.SelectedValue is OperationIconKind selectedIcon?selectedIcon:OperationIconKind.General;
        var priority=NotePriority.SelectedValue is OperationPriority selectedPriority?selectedPriority:OperationPriority.Normal;
        if(!Model.SaveNote(NoteText.Text,ReminderDate.Text,ReminderTime.Text,icon,priority))return;
        ClearNoteEditor();
    }
    private void EditNoteClick(object sender,RoutedEventArgs e)
    {
        var note=Model.BeginEditNote(Note(sender));NoteText.Text=note.Text;
        ReminderDate.Text=note.ReminderUtc?.ToLocalTime().ToString("dd/MM/yyyy")??"";
        ReminderTime.Text=note.ReminderUtc?.ToLocalTime().ToString("HH:mm")??"";
        NoteIcon.SelectedValue=note.Icon;NotePriority.SelectedValue=note.Priority;
        NoteText.Focus();NoteText.SelectAll();
    }
    private void CancelNoteClick(object sender,RoutedEventArgs e){Model.CancelEditNote();ClearNoteEditor();}
    private void AttendNoteClick(object sender,RoutedEventArgs e)=>Model.AttendNote(Note(sender));
    private void SnoozeNoteClick(object sender,RoutedEventArgs e)
    {
        var minutes=int.Parse((string)((Button)sender).Tag,CultureInfo.InvariantCulture);
        Model.SnoozeNote(Note(sender),minutes);
    }
    private void DeleteNoteClick(object sender,RoutedEventArgs e)=>Model.DeleteNote(Note(sender));
    private void ClearNoteEditor(){NoteText.Clear();ReminderDate.Clear();ReminderTime.Clear();NoteIcon.SelectedValue=OperationIconKind.General;NotePriority.SelectedValue=OperationPriority.Normal;NoteText.Focus();}
    private void EnsureDefaults()
    {
        if(TimerIcon.SelectedIndex<0)TimerIcon.SelectedValue=OperationIconKind.Oven;
        if(TimerPriority.SelectedIndex<0)TimerPriority.SelectedValue=OperationPriority.Normal;
        if(NoteIcon.SelectedIndex<0)NoteIcon.SelectedValue=OperationIconKind.General;
        if(NotePriority.SelectedIndex<0)NotePriority.SelectedValue=OperationPriority.Normal;
    }
}
