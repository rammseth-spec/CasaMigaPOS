using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CasaMiga.Infrastructure;
using CasaMiga.Presentation;

internal static class R2VisualEvidence
{
    public static int Run(string directory,string password,string output)
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var app=new System.Windows.Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/CasaMiga.Presentation;component/Resources/Theme.xaml",UriKind.Relative)});
            app.Dispatcher.BeginInvoke(async ()=>
            {
                try
                {
                    Directory.CreateDirectory(output);using var lease=new DirectoryLease(Path.GetFullPath(directory));
                    var model=new MainViewModel();await model.InitializeAsync(new(Path.GetFullPath(directory),true));
                    await model.LoginAsync("catalog-admin",password);if(!model.CanNavigate)throw new InvalidOperationException(model.Message);
                    var window=new MainWindow{DataContext=model};var content=(FrameworkElement)window.Content;
                    await model.ShowPosAsync();
                    for(var attempt=0;attempt<100&&model.Pos!.Products.Any(p=>!p.HasPhoto);attempt++)await Task.Delay(100);
                    if(model.Pos!.Products.Count!=26||model.Pos.Products.Any(p=>!p.HasPhoto))throw new InvalidOperationException("Faltan fotos en el POS.");
                    await model.Pos.AddAsync(model.Pos.Products[0].Id);
                    Save(content,1400,900,Path.Combine(output,"pos-compact.png"));
                    Save(content,1024,768,Path.Combine(output,"pos-1024.png"));
                    model.DiscardCart();await model.ShowCatalogAsync();
                    Save(content,1400,900,Path.Combine(output,"catalog-list.png"));
                    model.Catalog!.SelectedProduct=model.Catalog.Products[0];model.Catalog.EditProduct();
                    for(var attempt=0;attempt<100&&!model.Catalog.HasCurrentPhoto;attempt++)await Task.Delay(100);
                    var catalog=new CatalogView{DataContext=model.Catalog};Save(catalog,1050,1000,Path.Combine(output,"catalog-photo.png"));
                    var bytes=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(directory))!,"phase1k2c-r2-prepared-assets","01.png"));
                    Console.WriteLine("Constructing crop preview...");
                    _=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                    var crop=new PhotoCropDialog(window,bytes);Console.WriteLine("Rendering crop preview...");Save((FrameworkElement)crop.Content,850,650,Path.Combine(output,"crop-preview.png"));
                    model.Logout();Console.WriteLine("Rendered: POS 1400/1024 (menú de íconos fijo), catalog list/photo, crop preview; 26 photos loaded.");
                }
                catch(Exception error){failure=error;Console.Error.WriteLine(error);}
                finally{app.Dispatcher.InvokeShutdown();}
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        return failure is null?0:1;
    }
    private static void Save(FrameworkElement content,int width,int height,string path)
    {
        content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);content.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        var background=new DrawingVisual();using(var drawing=background.RenderOpen())drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(253,248,239)),null,new Rect(0,0,width,height));
        bitmap.Render(background);bitmap.Render(content);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
