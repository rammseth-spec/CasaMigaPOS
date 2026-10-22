using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace CasaMiga.Presentation;
public partial class BatchEntryView:UserControl
{
    public BatchEntryView()=>InitializeComponent();
    private BatchEntryViewModel? Model=>DataContext as BatchEntryViewModel;
    private async void VisibleChanged(object sender,DependencyPropertyChangedEventArgs e){if(IsVisible&&Model is {} model)await model.LoadAsync();}
    private void ProductClick(object sender,RoutedEventArgs e){if(sender is FrameworkElement{Tag:Guid id}&&Model is {} model){model.Choose(id);Dispatcher.BeginInvoke(()=>{PiecesBox.Focus();Keyboard.Focus(PiecesBox);});}}
    private async void SaveClick(object sender,RoutedEventArgs e){if(Model is {} model)await model.SaveAsync();}
    private void CancelClick(object sender,RoutedEventArgs e)=>Model?.Cancel();
    private async void PiecesKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&Model is {} model){e.Handled=true;await model.SaveAsync();}}
}
