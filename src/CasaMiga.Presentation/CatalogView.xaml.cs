using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace CasaMiga.Presentation;
public partial class CatalogView:UserControl
{
    public CatalogView()=>InitializeComponent();
    private CatalogViewModel Model=>(CatalogViewModel)DataContext;
    private async void ProductsClick(object s,RoutedEventArgs e)=>await Model.ShowAsync(false);
    private async void CategoriesClick(object s,RoutedEventArgs e)=>await Model.ShowAsync(true);
    private void NewCategoryClick(object s,RoutedEventArgs e){Model.NewCategory();CategoryName.Focus();CategoryName.BringIntoView();}
    private void EditCategoryClick(object s,RoutedEventArgs e){Model.EditCategory();if(Model.IsCategoryEditor){CategoryName.Focus();CategoryName.BringIntoView();}}
    private void NewProductClick(object s,RoutedEventArgs e){Model.NewProduct();ProductName.Focus();ProductName.BringIntoView();}
    private void EditProductClick(object s,RoutedEventArgs e){Model.EditProduct();if(Model.IsProductEditor){ProductName.Focus();ProductName.BringIntoView();}}
    private async void SearchClick(object s,RoutedEventArgs e)=>await Model.SearchAsync();
    private async void SaveClick(object s,RoutedEventArgs e)=>await Model.SaveAsync();
    private async void CancelClick(object s,RoutedEventArgs e)=>await Model.CancelAsync();
    private async void ToggleCategoryClick(object s,RoutedEventArgs e)=>await Model.ToggleCategoryAsync();
    private async void ToggleProductClick(object s,RoutedEventArgs e)=>await Model.ToggleProductAsync();
    private async void PreviousClick(object s,RoutedEventArgs e)=>await Model.PageAsync(-1);
    private async void NextClick(object s,RoutedEventArgs e)=>await Model.PageAsync(1);
    private async void DemoClick(object s,RoutedEventArgs e)=>await Model.DemoAsync();
    private async void CatalogKeyDown(object s,KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&(Model.IsCategoryEditor||Model.IsProductEditor)){e.Handled=true;await Model.CancelAsync();}
        else if(e.Key==Key.Enter&&Keyboard.FocusedElement==SearchBox){e.Handled=true;await Model.SearchAsync();}
    }
}
