using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace CasaMiga.Presentation;

public sealed class OperationCenterViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");
    private readonly OperationCenterStore store;
    private DateTimeOffset now;
    private bool panelOpen;
    private string timerMessage = "";
    private string noteMessage = "";
    private Guid? editingNoteId;
    private bool timerEditorOpen;
    private bool noteEditorOpen;

    public OperationCenterViewModel(string dataDirectory, Func<DateTimeOffset>? clock = null)
    {
        store = new OperationCenterStore(dataDirectory);
        now = (clock ?? (() => DateTimeOffset.Now))();
        var state = store.Load();
        foreach (var timer in state.Timers.Take(24)) Timers.Add(new OperationTimerViewModel(timer, now));
        foreach (var note in state.Notes.Take(100)) Notes.Add(new OperationNoteViewModel(note, now));
        Tick(now);
    }

    public ObservableCollection<OperationTimerViewModel> Timers { get; } = [];
    public ObservableCollection<OperationNoteViewModel> Notes { get; } = [];
    public IReadOnlyList<OperationIconOption> TimerIconOptions => OperationIconCatalog.TimerOptions;
    public IReadOnlyList<OperationIconOption> NoteIconOptions => OperationIconCatalog.NoteOptions;
    public IReadOnlyList<OperationPriorityOption> PriorityOptions => OperationIconCatalog.Priorities;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<OperationAlertEventArgs>? AlertRaised;

    public bool IsPanelOpen { get => panelOpen; private set { if (panelOpen == value) return; panelOpen = value; Changed(nameof(IsPanelOpen)); } }
    public string ClockTime => now.LocalDateTime.ToString("HH:mm", Spanish);
    public string ClockDate
    {
        get
        {
            var value = now.LocalDateTime.ToString("ddd · d MMM", Spanish).Replace(".", "", StringComparison.Ordinal);
            return char.ToUpper(value[0], Spanish) + value[1..];
        }
    }
    public int RunningCount => Timers.Count(x => x.IsRunning);
    public int ReadyTimerCount => Timers.Count(x => x.IsReady);
    public int ReadyReminderCount => Notes.Count(x => x.IsReady);
    public int UrgentReadyCount => Timers.Count(x => x.IsReady && x.IsUrgent) + Notes.Count(x => x.IsReady && x.IsUrgent);
    public int ReadyCount => ReadyTimerCount + ReadyReminderCount;
    public bool HasHeaderStatus => RunningCount > 0 || ReadyCount > 0;
    public string HeaderStatus
    {
        get
        {
            if (UrgentReadyCount > 0) return UrgentReadyCount == 1 ? "1 urgente" : $"{UrgentReadyCount} urgentes";
            if (ReadyCount > 0 && RunningCount > 0) return $"{ReadyCount} listo{(ReadyCount == 1 ? "" : "s")} · {RunningCount} activo{(RunningCount == 1 ? "" : "s")}";
            if (ReadyCount > 0) return ReadyCount == 1 ? "1 listo" : $"{ReadyCount} listos";
            var nearest = Timers.Where(x => x.IsRunning).Select(x => x.RemainingSeconds).DefaultIfEmpty().Min();
            return RunningCount == 1 ? $"1 activo · {FormatCompact(nearest)}" : $"{RunningCount} activos · {FormatCompact(nearest)}";
        }
    }
    public bool HasTimers => Timers.Count > 0;
    public bool HasNotes => Notes.Count > 0;
    public string TimerMessage { get => timerMessage; private set { timerMessage = value; Changed(nameof(TimerMessage)); Changed(nameof(HasTimerMessage)); } }
    public bool HasTimerMessage => !string.IsNullOrWhiteSpace(TimerMessage);
    public string NoteMessage { get => noteMessage; private set { noteMessage = value; Changed(nameof(NoteMessage)); Changed(nameof(HasNoteMessage)); } }
    public bool HasNoteMessage => !string.IsNullOrWhiteSpace(NoteMessage);
    public bool IsEditingNote => editingNoteId.HasValue;
    public bool IsTimerEditorOpen { get => timerEditorOpen; private set { if (timerEditorOpen == value) return; timerEditorOpen = value; Changed(nameof(IsTimerEditorOpen)); } }
    public bool IsNoteEditorOpen { get => noteEditorOpen; private set { if (noteEditorOpen == value) return; noteEditorOpen = value; Changed(nameof(IsNoteEditorOpen)); } }

    public void OpenPanel() => IsPanelOpen = true;
    public void ClosePanel() => IsPanelOpen = false;
    public void OpenTimerEditor() { TimerMessage = ""; IsTimerEditorOpen = true; }
    public void CancelTimerEditor() { TimerMessage = ""; IsTimerEditorOpen = false; }
    public void OpenNoteEditor() { editingNoteId = null; NoteMessage = ""; Changed(nameof(IsEditingNote)); IsNoteEditorOpen = true; }
    public void CancelNoteEditor() { editingNoteId = null; NoteMessage = ""; Changed(nameof(IsEditingNote)); IsNoteEditorOpen = false; }

    public void Tick(DateTimeOffset value)
    {
        var minuteChanged = now.LocalDateTime.Minute != value.LocalDateTime.Minute || now.LocalDateTime.Date != value.LocalDateTime.Date;
        var hadRunningTimer = RunningCount > 0;
        now = value;
        var stateChanged = false;
        var urgent = false;
        var alertCount = 0;
        foreach (var timer in Timers)
        {
            if (!timer.Tick(value)) continue;
            stateChanged = true; alertCount++; urgent |= timer.IsUrgent;
        }
        foreach (var note in Notes)
        {
            if (!note.Tick(value)) continue;
            stateChanged = true; alertCount++; urgent |= note.IsUrgent;
        }
        if (minuteChanged) { Changed(nameof(ClockTime)); Changed(nameof(ClockDate)); }
        if (hadRunningTimer || stateChanged) HeaderChanged();
        if (!stateChanged) return;
        Save();
        AlertRaised?.Invoke(this, new(alertCount, urgent));
    }

    public bool CreateTimer(string? name, string? hoursText, string? minutesText, OperationIconKind icon = OperationIconKind.Clock, OperationPriority priority = OperationPriority.Normal)
    {
        TimerMessage = "";
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return TimerFailure("Escribe un nombre para el temporizador.");
        if (name.Length > 80) return TimerFailure("El nombre admite hasta 80 caracteres.");
        if (!int.TryParse(hoursText, out var hours) || !int.TryParse(minutesText, out var minutes) || hours < 0 || minutes < 0 || minutes > 59)
            return TimerFailure("Escribe horas y minutos válidos; los minutos van de 0 a 59.");
        var seconds = checked((long)hours * 3600L + (long)minutes * 60L);
        if (seconds is < 60 or > 359999) return TimerFailure("El temporizador debe durar entre 1 minuto y 99 horas 59 minutos.");
        if (Timers.Count >= 24) return TimerFailure("Puedes conservar hasta 24 temporizadores.");
        Timers.Insert(0, OperationTimerViewModel.Create(name, seconds, now, icon, priority));
        IsTimerEditorOpen = false; Changed(nameof(HasTimers)); HeaderChanged(); Save();
        return true;
    }

    public void PauseTimer(OperationTimerViewModel timer) { if (!timer.Pause(now)) return; TimerMessage = ""; HeaderChanged(); Save(); }
    public void ResumeTimer(OperationTimerViewModel timer) { if (!timer.Resume(now)) return; TimerMessage = ""; HeaderChanged(); Save(); }
    public void ResetTimer(OperationTimerViewModel timer) { timer.Reset(); TimerMessage = ""; HeaderChanged(); Save(); }
    public void AttendTimer(OperationTimerViewModel timer) { timer.Attend(); TimerMessage = ""; HeaderChanged(); Save(); }
    public void DeleteTimer(OperationTimerViewModel timer) { if (!Timers.Remove(timer)) return; Changed(nameof(HasTimers)); HeaderChanged(); Save(); }

    public bool SaveNote(string? text, string? dateText, string? timeText, OperationIconKind icon = OperationIconKind.General, OperationPriority priority = OperationPriority.Normal)
    {
        NoteMessage = "";
        text = text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return NoteFailure("Escribe el texto de la nota.");
        if (text.Length > 280) return NoteFailure("La nota admite hasta 280 caracteres.");
        if (!TryReminder(dateText, timeText, out var reminder, out var validation)) return NoteFailure(validation);
        if (editingNoteId is { } id)
        {
            var current = Notes.FirstOrDefault(x => x.Id == id);
            if (current is null) return NoteFailure("La nota que estabas editando ya no existe.");
            current.Edit(text, reminder, icon, priority, now);
            editingNoteId = null; Changed(nameof(IsEditingNote));
        }
        else
        {
            if (Notes.Count >= 100) return NoteFailure("Puedes conservar hasta 100 notas.");
            Notes.Insert(0, OperationNoteViewModel.Create(text, reminder, now, icon, priority));
            Changed(nameof(HasNotes));
        }
        IsNoteEditorOpen = false; HeaderChanged(); Save(); return true;
    }

    public OperationNoteViewModel BeginEditNote(OperationNoteViewModel note) { editingNoteId = note.Id; NoteMessage = ""; IsNoteEditorOpen = true; Changed(nameof(IsEditingNote)); return note; }
    public void CancelEditNote() => CancelNoteEditor();
    public void AttendNote(OperationNoteViewModel note) { note.Attend(); HeaderChanged(); Save(); }
    public void ToggleSnoozeOptions(OperationNoteViewModel note)
    {
        foreach (var current in Notes.Where(x => !ReferenceEquals(x, note))) current.CloseSnooze();
        note.ToggleSnooze();
    }
    public bool CloseTransientEditor()
    {
        if (IsTimerEditorOpen) { CancelTimerEditor(); return true; }
        if (IsNoteEditorOpen) { CancelNoteEditor(); return true; }
        var open = Notes.FirstOrDefault(x => x.IsSnoozeOpen);
        if (open is null) return false;
        open.CloseSnooze(); return true;
    }
    public void SnoozeNote(OperationNoteViewModel note, int minutes)
    {
        if (minutes is not (5 or 10 or 15)) throw new ArgumentOutOfRangeException(nameof(minutes));
        note.Snooze(now.AddMinutes(minutes)); HeaderChanged(); Save();
    }
    public void DeleteNote(OperationNoteViewModel note)
    {
        if (!Notes.Remove(note)) return;
        if (editingNoteId == note.Id)
        {
            editingNoteId = null;
            IsNoteEditorOpen = false;
        }
        Changed(nameof(IsEditingNote)); Changed(nameof(HasNotes)); HeaderChanged(); Save();
    }

    private bool TryReminder(string? dateText, string? timeText, out DateTimeOffset? reminder, out string validation)
    {
        reminder = null; validation = "";
        if (string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(timeText)) return true;
        if (string.IsNullOrWhiteSpace(dateText) || string.IsNullOrWhiteSpace(timeText)) { validation = "Completa fecha y hora, o deja ambos campos vacíos."; return false; }
        if (!DateTime.TryParseExact($"{dateText.Trim()} {timeText.Trim()}", "dd/MM/yyyy HH:mm", Spanish, DateTimeStyles.None, out var local)) { validation = "Usa fecha dd/mm/aaaa y hora HH:mm."; return false; }
        reminder = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)); return true;
    }

    private bool TimerFailure(string message) { TimerMessage = message; return false; }
    private bool NoteFailure(string message) { NoteMessage = message; return false; }
    private void HeaderChanged()
    {
        Changed(nameof(RunningCount)); Changed(nameof(ReadyTimerCount)); Changed(nameof(ReadyReminderCount)); Changed(nameof(UrgentReadyCount));
        Changed(nameof(ReadyCount)); Changed(nameof(HasHeaderStatus)); Changed(nameof(HeaderStatus));
    }
    private void Save()
    {
        try { store.Save(new(1, Timers.Select(x => x.Export()).ToArray(), Notes.Select(x => x.Export()).ToArray())); }
        catch (IOException) { NoteMessage = "No se pudo guardar el centro operativo. Revisa el espacio y permisos del equipo."; }
        catch (UnauthorizedAccessException) { NoteMessage = "No se pudo guardar el centro operativo. Revisa los permisos del equipo."; }
    }
    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private static string FormatCompact(long seconds) => seconds >= 3600 ? TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss") : TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
}

public sealed record OperationAlertEventArgs(int Count, bool IsUrgent);
public enum OperationTimerState { Running, Paused, Ready }

public sealed class OperationTimerViewModel : INotifyPropertyChanged
{
    private long remainingSeconds;
    private DateTimeOffset? deadlineUtc;
    private OperationTimerState state;
    internal OperationTimerViewModel(OperationTimerData data, DateTimeOffset now)
    {
        Id=data.Id; Name=data.Name; OriginalSeconds=data.OriginalSeconds; remainingSeconds=data.RemainingSeconds; deadlineUtc=data.DeadlineUtc; state=data.State;
        Icon=Enum.IsDefined(data.Icon)?data.Icon:OperationIconKind.General; Priority=Enum.IsDefined(data.Priority)?data.Priority:OperationPriority.Normal;
        if (state == OperationTimerState.Running && deadlineUtc is null) state = OperationTimerState.Paused;
    }
    internal static OperationTimerViewModel Create(string name,long seconds,DateTimeOffset now,OperationIconKind icon,OperationPriority priority) => new(new(Guid.NewGuid(),name,seconds,seconds,now.ToUniversalTime().AddSeconds(seconds),OperationTimerState.Running,icon,priority),now);
    public Guid Id { get; }
    public string Name { get; }
    public long OriginalSeconds { get; }
    public OperationIconKind Icon { get; }
    public string IconLabel => OperationIconCatalog.Get(Icon).Label;
    public Geometry IconGeometry => OperationIconCatalog.Get(Icon).Geometry;
    public OperationPriority Priority { get; }
    public bool IsUrgent => Priority == OperationPriority.Urgent;
    public long RemainingSeconds => remainingSeconds;
    public bool IsRunning => state == OperationTimerState.Running;
    public bool IsPaused => state == OperationTimerState.Paused;
    public bool IsReady => state == OperationTimerState.Ready;
    public string RemainingText => IsReady ? "¡Listo!" : remainingSeconds >= 3600 ? TimeSpan.FromSeconds(remainingSeconds).ToString(@"hh\:mm\:ss") : TimeSpan.FromSeconds(remainingSeconds).ToString(@"mm\:ss");
    public string StateText => IsRunning ? $"Termina a las {deadlineUtc?.ToLocalTime():HH:mm}" : IsReady ? "Requiere atención" : "En pausa";
    public event PropertyChangedEventHandler? PropertyChanged;
    internal bool Tick(DateTimeOffset now)
    {
        if (!IsRunning || deadlineUtc is null) return false;
        var next=Math.Max(0,(long)Math.Ceiling((deadlineUtc.Value-now.ToUniversalTime()).TotalSeconds));
        if(next==remainingSeconds && next>0)return false;
        remainingSeconds=next;
        if(next==0){state=OperationTimerState.Ready;deadlineUtc=null;All();return true;}
        Changed(nameof(RemainingSeconds));Changed(nameof(RemainingText));return false;
    }
    internal bool Pause(DateTimeOffset now){if(!IsRunning)return false;Tick(now);if(IsReady)return false;state=OperationTimerState.Paused;deadlineUtc=null;All();return true;}
    internal bool Resume(DateTimeOffset now){if(!IsPaused)return false;state=OperationTimerState.Running;deadlineUtc=now.ToUniversalTime().AddSeconds(remainingSeconds);All();return true;}
    internal void Reset(){state=OperationTimerState.Paused;remainingSeconds=OriginalSeconds;deadlineUtc=null;All();}
    internal void Attend()=>Reset();
    internal OperationTimerData Export()=>new(Id,Name,OriginalSeconds,remainingSeconds,deadlineUtc,state,Icon,Priority);
    private void All(){Changed(nameof(RemainingSeconds));Changed(nameof(RemainingText));Changed(nameof(StateText));Changed(nameof(IsRunning));Changed(nameof(IsPaused));Changed(nameof(IsReady));}
    private void Changed(string name)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}

public sealed class OperationNoteViewModel : INotifyPropertyChanged
{
    private string text;
    private DateTimeOffset? reminderUtc;
    private bool attended;
    private bool ready;
    private OperationIconKind icon;
    private OperationPriority priority;
    private bool snoozeOpen;
    internal OperationNoteViewModel(OperationNoteData data, DateTimeOffset now)
    {
        Id=data.Id;text=data.Text;reminderUtc=data.ReminderUtc;attended=data.Attended;
        icon=Enum.IsDefined(data.Icon)?data.Icon:OperationIconKind.General; priority=Enum.IsDefined(data.Priority)?data.Priority:OperationPriority.Normal;
    }
    internal static OperationNoteViewModel Create(string text,DateTimeOffset? reminder,DateTimeOffset now,OperationIconKind icon,OperationPriority priority)=>new(new(Guid.NewGuid(),text,reminder?.ToUniversalTime(),false,icon,priority),now);
    public Guid Id{get;}
    public string Text=>text;
    public DateTimeOffset? ReminderUtc=>reminderUtc;
    public OperationIconKind Icon=>icon;
    public string IconLabel=>OperationIconCatalog.Get(icon).Label;
    public Geometry IconGeometry=>OperationIconCatalog.Get(icon).Geometry;
    public OperationPriority Priority=>priority;
    public bool IsUrgent=>priority==OperationPriority.Urgent;
    public bool IsReady=>ready;
    public bool IsAttended=>attended;
    public bool IsSnoozeOpen=>snoozeOpen;
    public string ReminderText=>reminderUtc is null?"Sin recordatorio":attended?$"Atendido · {reminderUtc.Value.ToLocalTime():dd/MM/yyyy · HH:mm}":ready?$"¡Recordatorio listo! · {reminderUtc.Value.ToLocalTime():dd/MM/yyyy · HH:mm}":reminderUtc.Value.ToLocalTime().ToString("dd/MM/yyyy · HH:mm");
    public event PropertyChangedEventHandler? PropertyChanged;
    internal bool Tick(DateTimeOffset now){var next=!attended&&reminderUtc is not null&&now.ToUniversalTime()>=reminderUtc.Value;if(next==ready)return false;ready=next;All();return next;}
    internal void Edit(string value,DateTimeOffset? reminder,OperationIconKind selectedIcon,OperationPriority selectedPriority,DateTimeOffset now){text=value;reminderUtc=reminder?.ToUniversalTime();icon=selectedIcon;priority=selectedPriority;attended=false;ready=false;Tick(now);All();}
    internal void Attend(){attended=true;ready=false;snoozeOpen=false;All();}
    internal void ToggleSnooze(){snoozeOpen=!snoozeOpen;Changed(nameof(IsSnoozeOpen));}
    internal void CloseSnooze(){if(!snoozeOpen)return;snoozeOpen=false;Changed(nameof(IsSnoozeOpen));}
    internal void Snooze(DateTimeOffset reminder){reminderUtc=reminder.ToUniversalTime();attended=false;ready=false;snoozeOpen=false;All();}
    internal OperationNoteData Export()=>new(Id,text,reminderUtc,attended,icon,priority);
    private void All(){Changed(nameof(Text));Changed(nameof(ReminderUtc));Changed(nameof(ReminderText));Changed(nameof(Icon));Changed(nameof(IconLabel));Changed(nameof(IconGeometry));Changed(nameof(Priority));Changed(nameof(IsUrgent));Changed(nameof(IsReady));Changed(nameof(IsAttended));Changed(nameof(IsSnoozeOpen));}
    private void Changed(string name)=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}

internal sealed class OperationCenterStore
{
    internal const string FileName="operations-hub.json";
    private readonly string path;
    private static readonly JsonSerializerOptions Json = new(){WriteIndented=true};
    internal OperationCenterStore(string dataDirectory){path=Path.Combine(dataDirectory,FileName);}
    internal OperationCenterData Load()
    {
        try
        {
            if(!File.Exists(path))return OperationCenterData.Empty;
            var data=JsonSerializer.Deserialize<OperationCenterData>(File.ReadAllText(path),Json);
            return data is {Version:1}?data:OperationCenterData.Empty;
        }
        catch(JsonException){return OperationCenterData.Empty;}
        catch(IOException){return OperationCenterData.Empty;}
        catch(UnauthorizedAccessException){return OperationCenterData.Empty;}
    }
    internal void Save(OperationCenterData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary=path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(data,Json));
        File.Move(temporary,path,true);
    }
}

internal sealed record OperationCenterData(int Version,OperationTimerData[] Timers,OperationNoteData[] Notes)
{
    internal static OperationCenterData Empty {get;}=new(1,[],[]);
}
internal sealed record OperationTimerData(Guid Id,string Name,long OriginalSeconds,long RemainingSeconds,DateTimeOffset? DeadlineUtc,OperationTimerState State,OperationIconKind Icon=OperationIconKind.General,OperationPriority Priority=OperationPriority.Normal);
internal sealed record OperationNoteData(Guid Id,string Text,DateTimeOffset? ReminderUtc,bool Attended,OperationIconKind Icon=OperationIconKind.General,OperationPriority Priority=OperationPriority.Normal);
