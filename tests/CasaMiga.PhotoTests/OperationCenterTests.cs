using System.ComponentModel;
using System.IO;
using CasaMiga.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.PhotoTests;

[TestClass]
public sealed class OperationCenterTests
{
    [TestMethod]
    public void MultipleTimersUseDeadlinesAndSurvivePauseResumeRestart()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        foreach(var minutes in new[]{5,10,15,30,45,60})
            Assert.IsTrue(center.CreateTimer($"Horneado {minutes}",(minutes/60).ToString(),(minutes%60).ToString()));
        Assert.AreEqual(6,center.Timers.Count);
        Assert.AreEqual(6,center.RunningCount);
        var timer=center.Timers.Last();
        center.Tick(start.AddMinutes(2));
        center.PauseTimer(timer);
        Assert.IsTrue(timer.IsPaused);
        Assert.AreEqual(180,timer.RemainingSeconds);

        var restored=new OperationCenterViewModel(fixture.Path,()=>start.AddMinutes(20));
        var paused=restored.Timers.Single(x=>x.Id==timer.Id);
        Assert.IsTrue(paused.IsPaused);
        Assert.AreEqual(180,paused.RemainingSeconds);
        restored.ResumeTimer(paused);
        restored.Tick(start.AddMinutes(23));
        Assert.IsTrue(paused.IsReady);
        Assert.AreEqual("¡Listo!",paused.RemainingText);
        restored.AttendTimer(paused);
        Assert.IsTrue(paused.IsPaused);
        Assert.AreEqual(paused.OriginalSeconds,paused.RemainingSeconds);
    }

    [TestMethod]
    public void NotesCanBeCreatedEditedTriggeredAttendedDeletedAndRestored()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        Assert.IsTrue(center.SaveNote("Revisar fermentación","",""));
        var note=center.Notes.Single();
        Assert.AreEqual("Sin recordatorio",note.ReminderText);
        center.BeginEditNote(note);
        var due=start.AddMinutes(5);
        Assert.IsTrue(center.SaveNote("Revisar fermentación final",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm")));
        center.Tick(due);
        Assert.IsTrue(note.IsReady);
        Assert.AreEqual(1,center.ReadyCount);
        center.AttendNote(note);
        Assert.IsTrue(note.IsAttended);

        var restored=new OperationCenterViewModel(fixture.Path,()=>due.AddHours(1));
        Assert.AreEqual("Revisar fermentación final",restored.Notes.Single().Text);
        Assert.IsTrue(restored.Notes.Single().IsAttended);
        restored.DeleteNote(restored.Notes.Single());
        Assert.IsFalse(restored.HasNotes);
    }

    [TestMethod]
    public void SharedTickRaisesOnlyNamedPropertiesAndDoesNotRewritePersistenceEachSecond()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        var idleProperties=new List<string?>();
        center.PropertyChanged+=(_,e)=>idleProperties.Add(e.PropertyName);
        center.Tick(start.AddSeconds(1));
        Assert.AreEqual(0,idleProperties.Count,"An idle second must not notify unchanged header or clock properties.");
        Assert.IsTrue(center.CreateTimer("Cocción","0","10"));
        var file=System.IO.Path.Combine(fixture.Path,"operations-hub.json");
        var written=File.GetLastWriteTimeUtc(file);
        var properties=new List<string?>();
        center.PropertyChanged+=(_,e)=>properties.Add(e.PropertyName);
        center.Tick(start.AddSeconds(1));
        Assert.IsFalse(properties.Any(string.IsNullOrEmpty),"The shared tick must not issue PropertyChanged(null).");
        Assert.IsFalse(properties.Contains(nameof(center.ClockTime)),"The clock text changes only when the minute changes.");
        Assert.AreEqual(written,File.GetLastWriteTimeUtc(file),"A normal second must not persist unchanged timer state.");
    }

    [TestMethod]
    public void ValidationKeepsInvalidTimerAndReminderOutOfTheStore()
    {
        using var fixture=new DirectoryFixture();
        var center=new OperationCenterViewModel(fixture.Path);
        Assert.IsFalse(center.CreateTimer("","0","10"));
        Assert.IsFalse(center.CreateTimer("Prueba","0","60"));
        Assert.IsFalse(center.SaveNote("Recordar","08/10/2026",""));
        Assert.AreEqual(0,center.Timers.Count);
        Assert.AreEqual(0,center.Notes.Count);
    }

    [TestMethod]
    public void HeaderStaysCleanThenReportsActiveAndReadyWorkWithLocalClock()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(new DateTime(2026,10,8,9,7,0,DateTimeKind.Local));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        Assert.AreEqual("09:07",center.ClockTime);
        StringAssert.Contains(center.ClockDate,"8 oct");
        Assert.IsFalse(center.HasHeaderStatus);
        Assert.IsTrue(center.CreateTimer("Primer horneado","0","1"));
        Assert.AreEqual("1 activo · 01:00",center.HeaderStatus);
        center.Tick(start.AddMinutes(1));
        Assert.AreEqual("1 listo",center.HeaderStatus);
        center.DeleteTimer(center.Timers.Single());
        Assert.IsFalse(center.HasHeaderStatus);
        Assert.IsFalse(center.HasTimers);
    }

    [TestMethod]
    public void VectorCategoriesAndPrioritiesPersistAcrossNavigationAndRestart()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        Assert.AreEqual(12,center.TimerIconOptions.Count);
        Assert.AreEqual(8,center.NoteIconOptions.Count);
        Assert.AreEqual(2,center.PriorityOptions.Count);
        Assert.IsTrue(center.TimerIconOptions.All(x=>x.Geometry is not null&&!x.Geometry.Bounds.IsEmpty));
        Assert.IsTrue(center.CreateTimer("Entregar pedido","0","5",OperationIconKind.Delivery,OperationPriority.Urgent));
        Assert.IsTrue(center.SaveNote("Comprar harina","","",OperationIconKind.Supply,OperationPriority.Normal));
        center.OpenPanel();center.ClosePanel();

        var restored=new OperationCenterViewModel(fixture.Path,()=>start.AddMinutes(1));
        Assert.AreEqual(OperationIconKind.Delivery,restored.Timers.Single().Icon);
        Assert.IsTrue(restored.Timers.Single().IsUrgent);
        Assert.AreEqual("Entrega",restored.Timers.Single().IconLabel);
        Assert.AreEqual(OperationIconKind.Supply,restored.Notes.Single().Icon);
        Assert.AreEqual("Compra / insumo",restored.Notes.Single().IconLabel);
    }

    [TestMethod]
    public void SimultaneousReadyItemsRaiseOneAlertWithoutLoopAndAttentionClearsWhenAttended()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        Assert.IsTrue(center.CreateTimer("Horno uno","0","1",OperationIconKind.Oven));
        Assert.IsTrue(center.CreateTimer("Horno dos","0","1",OperationIconKind.Oven,OperationPriority.Urgent));
        var due=start.AddMinutes(1);
        Assert.IsTrue(center.SaveNote("Salida urgente",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm"),OperationIconKind.Delivery,OperationPriority.Urgent));
        var alerts=new List<OperationAlertEventArgs>();
        center.AlertRaised+=(_,e)=>alerts.Add(e);

        center.Tick(due);
        Assert.AreEqual(1,alerts.Count,"All items completing in one shared tick produce one sound/attention request.");
        Assert.AreEqual(3,alerts[0].Count);
        Assert.IsTrue(alerts[0].IsUrgent);
        Assert.AreEqual(3,center.ReadyCount);
        Assert.AreEqual("2 urgentes",center.HeaderStatus);
        center.Tick(due.AddSeconds(1));
        Assert.AreEqual(1,alerts.Count,"Ready state does not loop alerts.");

        foreach(var timer in center.Timers.ToArray())center.AttendTimer(timer);
        center.AttendNote(center.Notes.Single());
        Assert.AreEqual(0,center.ReadyCount);
        Assert.AreEqual(0,center.UrgentReadyCount);
    }

    [TestMethod]
    public void NormalAndUrgentRemindersCanBeSnoozedForEveryApprovedInterval()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        var due=start.AddMinutes(1);
        Assert.IsTrue(center.SaveNote("Normal",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm"),OperationIconKind.Reminder,OperationPriority.Normal));
        Assert.IsTrue(center.SaveNote("Urgente",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm"),OperationIconKind.Urgent,OperationPriority.Urgent));
        center.Tick(due);
        Assert.AreEqual(2,center.ReadyReminderCount);
        Assert.AreEqual(1,center.UrgentReadyCount);

        var normal=center.Notes.Single(x=>x.Text=="Normal");
        center.SnoozeNote(normal,5);
        Assert.IsFalse(normal.IsReady);
        Assert.AreEqual(due.AddMinutes(5).ToUniversalTime(),normal.ReminderUtc);
        center.Tick(due.AddMinutes(5));
        center.SnoozeNote(normal,10);
        Assert.AreEqual(due.AddMinutes(15).ToUniversalTime(),normal.ReminderUtc);
        center.Tick(due.AddMinutes(15));
        center.SnoozeNote(normal,15);
        Assert.AreEqual(due.AddMinutes(30).ToUniversalTime(),normal.ReminderUtc);
    }

    [TestMethod]
    public void HeaderRemainsCompactWithManyMixedEvents()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,8,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        for(var i=0;i<12;i++)Assert.IsTrue(center.CreateTimer($"Operación con nombre largo {i}","0","1",OperationIconKind.Clock,i==0?OperationPriority.Urgent:OperationPriority.Normal));
        center.Tick(start.AddMinutes(1));
        Assert.IsTrue(center.HeaderStatus.Length<=20,center.HeaderStatus);
        Assert.IsFalse(center.HeaderStatus.Contains("Operación",StringComparison.Ordinal));
    }

    [TestMethod]
    public void EditorsStartCollapsedAndCloseAfterCancelCreateOrSave()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,9,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        Assert.IsFalse(center.IsTimerEditorOpen);
        Assert.IsFalse(center.IsNoteEditorOpen);

        center.OpenTimerEditor();
        Assert.IsTrue(center.IsTimerEditorOpen);
        center.CancelTimerEditor();
        Assert.IsFalse(center.IsTimerEditorOpen);
        Assert.AreEqual(0,center.Timers.Count);
        center.OpenTimerEditor();
        Assert.IsTrue(center.CreateTimer("Primer horneado","0","10"));
        Assert.IsFalse(center.IsTimerEditorOpen);

        center.OpenNoteEditor();
        Assert.IsTrue(center.IsNoteEditorOpen);
        center.CancelNoteEditor();
        Assert.IsFalse(center.IsNoteEditorOpen);
        Assert.AreEqual(0,center.Notes.Count);
        center.OpenNoteEditor();
        Assert.IsTrue(center.SaveNote("Revisar vitrina","",""));
        Assert.IsFalse(center.IsNoteEditorOpen);

        var note=center.Notes.Single();
        center.BeginEditNote(note);
        Assert.IsTrue(center.IsNoteEditorOpen);
        Assert.IsTrue(center.IsEditingNote);
        center.CancelEditNote();
        Assert.IsFalse(center.IsNoteEditorOpen);
        Assert.IsFalse(center.IsEditingNote);
        Assert.AreEqual("Revisar vitrina",note.Text);
    }

    [TestMethod]
    public void SnoozeChoicesAreTransientExclusiveAndCloseAfterSelectionOrEscape()
    {
        using var fixture=new DirectoryFixture();
        var start=new DateTimeOffset(2026,10,9,9,0,0,TimeSpan.FromHours(-6));
        var center=new OperationCenterViewModel(fixture.Path,()=>start);
        var due=start.AddMinutes(1);
        Assert.IsTrue(center.SaveNote("Primera",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm")));
        Assert.IsTrue(center.SaveNote("Segunda",due.ToString("dd/MM/yyyy"),due.ToString("HH:mm")));
        center.Tick(due);
        var first=center.Notes.Single(x=>x.Text=="Primera");
        var second=center.Notes.Single(x=>x.Text=="Segunda");

        center.ToggleSnoozeOptions(first);
        Assert.IsTrue(first.IsSnoozeOpen);
        center.ToggleSnoozeOptions(second);
        Assert.IsFalse(first.IsSnoozeOpen);
        Assert.IsTrue(second.IsSnoozeOpen);
        Assert.IsTrue(center.CloseTransientEditor());
        Assert.IsFalse(second.IsSnoozeOpen);
        Assert.IsFalse(center.CloseTransientEditor());

        center.ToggleSnoozeOptions(first);
        center.SnoozeNote(first,10);
        Assert.IsFalse(first.IsSnoozeOpen);
        Assert.IsFalse(first.IsReady);
        Assert.AreEqual(due.AddMinutes(10).ToUniversalTime(),first.ReminderUtc);
    }

    private sealed class DirectoryFixture:IDisposable
    {
        public string Path{get;}=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"CasaMigaOperations",Guid.NewGuid().ToString("N"));
        public DirectoryFixture()=>Directory.CreateDirectory(Path);
        public void Dispose()=>Directory.Delete(Path,true);
    }
}
