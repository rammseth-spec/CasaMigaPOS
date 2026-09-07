using System.Windows;
namespace CasaMiga.Presentation;
public partial class DiscardDialog : Window
{
    private DiscardDialog(Window owner,string reason)
    {
        InitializeComponent();Owner=owner;
        Explanation.Text=$"Hay partidas en el carrito temporal. Para {reason} debes descartarlas. No se registrará una venta.";
        Loaded+=(_,_)=>KeepButton.Focus();
    }
    public static bool Confirm(Window owner,string reason)=>new DiscardDialog(owner,reason).ShowDialog()==true;
    private void KeepClick(object s,RoutedEventArgs e)=>DialogResult=false;
    private void DiscardClick(object s,RoutedEventArgs e)=>DialogResult=true;
}
