using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
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
    private void ManagePhotoClick(object s,RoutedEventArgs e)
    {
        Model.EditProduct();
        if(Model.IsProductEditor)Dispatcher.BeginInvoke(()=>PhotoSection.BringIntoView());
    }
    private async void SearchClick(object s,RoutedEventArgs e)=>await Model.SearchAsync();
    private async void ClearSearchClick(object s,RoutedEventArgs e){await Model.ClearSearchAsync();SearchBox.Focus();}
    private void BeginInlineCategoryClick(object s,RoutedEventArgs e)
    {
        Model.BeginInlineCategory();
        if(Model.IsInlineCategoryEditor)Dispatcher.BeginInvoke(()=>{InlineCategoryName.Focus();InlineCategoryName.BringIntoView();});
    }
    private void CancelInlineCategoryClick(object s,RoutedEventArgs e)=>Model.CancelInlineCategory();
    private async void CreateInlineCategoryClick(object s,RoutedEventArgs e)=>await Model.CreateInlineCategoryAsync();
    private async void SaveClick(object s,RoutedEventArgs e)=>await Model.SaveAsync();
    private async void CancelClick(object s,RoutedEventArgs e)=>await Model.CancelAsync();
    private async void ToggleCategoryClick(object s,RoutedEventArgs e)=>await Model.ToggleCategoryAsync();
    private async void ToggleProductClick(object s,RoutedEventArgs e)=>await Model.ToggleProductAsync();
    private async void PreviousClick(object s,RoutedEventArgs e)=>await Model.PageAsync(-1);
    private async void NextClick(object s,RoutedEventArgs e)=>await Model.PageAsync(1);
    private async void DemoClick(object s,RoutedEventArgs e)=>await Model.DemoAsync();
    private async void CasaMigaCatalogClick(object s,RoutedEventArgs e)
    {
        if(MessageBox.Show(Window.GetWindow(this),"Se agregan los panes del catálogo de Casa Miga que falten, con su foto provisional, y se renombran los que cambiaron de nombre. No se borra ningún pan, no cambian precios y una foto real nunca se reemplaza. ¿Deseas continuar?",
            "Cargar catálogo Casa Miga",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        await Model.LoadCasaMigaCatalogAsync();
    }
    private async void ChangePhotoClick(object s,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Title="Seleccionar fotografía del producto",Filter="Fotografías JPEG o PNG|*.jpg;*.jpeg;*.png",CheckFileExists=true,Multiselect=false};
        if(picker.ShowDialog(Window.GetWindow(this))!=true)return;
        try
        {
            var bytes=await File.ReadAllBytesAsync(picker.FileName);
            var dialog=new PhotoCropDialog(Window.GetWindow(this),bytes);
            if(dialog.ShowDialog()!=true)return;
            await Model.SetPhotoAsync(Path.GetFileName(picker.FileName),bytes,dialog.Crop);
        }
        catch(CasaMiga.Application.ProductPhotoException error){AppDialog.Show(Window.GetWindow(this),error.Message,"Casa Miga · Fotografía",MessageBoxButton.OK,MessageBoxImage.Warning);}
        catch(IOException){AppDialog.Show(Window.GetWindow(this),"No se pudo leer la fotografía. Revisa el archivo y vuelve a intentar.","Casa Miga · Fotografía",MessageBoxButton.OK,MessageBoxImage.Warning);}
    }
    private async void RemovePhotoClick(object s,RoutedEventArgs e)
    {
        if(AppDialog.Show(Window.GetWindow(this),"¿Quitar la fotografía de este producto? El producto y su historial se conservarán.","Casa Miga · Quitar foto",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        await Model.RemovePhotoAsync();
    }
    private async void CatalogKeyDown(object s,KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&Model.IsInlineCategoryEditor){e.Handled=true;Model.CancelInlineCategory();}
        else if(e.Key==Key.Escape&&(Model.IsCategoryEditor||Model.IsProductEditor)){e.Handled=true;await Model.CancelAsync();}
        else if(e.Key==Key.Escape&&Keyboard.FocusedElement==SearchBox&&Model.HasSearch){e.Handled=true;await Model.ClearSearchAsync();SearchBox.Focus();}
        else if(e.Key==Key.Enter&&Keyboard.FocusedElement==SearchBox){e.Handled=true;await Model.SearchAsync();}
    }
}
