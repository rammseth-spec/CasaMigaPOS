using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public partial class PhotoCropDialog : Window
{
    private readonly int width;
    private readonly int height;
    private bool initializing = true;
    public PhotoCrop Crop => new((int)Math.Round(CropX.Value),(int)Math.Round(CropY.Value),(int)Math.Round(CropEdge.Value));
    public PhotoCropDialog(Window owner,byte[] bytes)
    {
        Owner=owner;InitializeComponent();
        using var stream=new MemoryStream(bytes,false);
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        if(decoder.Frames.Count!=1)throw new ProductPhotoException("Selecciona una sola imagen JPEG o PNG.");
        var frame=decoder.Frames[0];var orientation=ReadOrientation(frame);
        width=orientation>=5?frame.PixelHeight:frame.PixelWidth;height=orientation>=5?frame.PixelWidth:frame.PixelHeight;
        if(Math.Min(width,height)<256)throw new ProductPhotoException("La foto necesita al menos 256 píxeles por lado.");
        frame.Freeze();PreviewImage.Source=frame;PreviewImage.LayoutTransform=OrientationTransform(orientation);
        var centered=ProductPhotoUiGateway.CenteredCrop(width,height);var edge=centered.Edge;CropEdge.Maximum=edge;CropEdge.Value=edge;
        CropX.Maximum=width-edge;CropY.Maximum=height-edge;CropX.Value=centered.X;CropY.Value=centered.Y;
        DimensionsLabel.Text=$"Imagen orientada: {width} × {height} px";initializing=false;UpdateOverlay();
    }
    private static int ReadOrientation(BitmapFrame frame)
    {
        if(frame.Metadata is not BitmapMetadata metadata)return 1;
        foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})
            if(metadata.ContainsQuery(query)&&metadata.GetQuery(query) is {} value)return Convert.ToInt32(value);
        return 1;
    }
    private static Transform OrientationTransform(int orientation)
    {
        var group=new TransformGroup();
        if(orientation is 2 or 5 or 7)group.Children.Add(new ScaleTransform(-1,1));
        if(orientation==4)group.Children.Add(new ScaleTransform(1,-1));
        group.Children.Add(new RotateTransform(orientation switch{3=>180,5=>90,6=>90,7=>270,8=>270,_=>0}));
        return group;
    }
    private void EdgeChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(initializing)return;CropX.Maximum=Math.Max(0,width-CropEdge.Value);CropY.Maximum=Math.Max(0,height-CropEdge.Value);
        CropX.Value=Math.Min(CropX.Value,CropX.Maximum);CropY.Value=Math.Min(CropY.Value,CropY.Maximum);UpdateOverlay();
    }
    private void CropChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(!initializing)UpdateOverlay();}
    private void PreviewSizeChanged(object sender,SizeChangedEventArgs e)=>UpdateOverlay();
    private void UpdateOverlay()
    {
        if(initializing||PreviewHost.ActualWidth<=0||PreviewHost.ActualHeight<=0)return;
        var scale=Math.Min(PreviewHost.ActualWidth/width,PreviewHost.ActualHeight/height);
        var left=(PreviewHost.ActualWidth-width*scale)/2+CropX.Value*scale;
        var top=(PreviewHost.ActualHeight-height*scale)/2+CropY.Value*scale;
        CropRectangle.Width=CropEdge.Value*scale;CropRectangle.Height=CropEdge.Value*scale;
        Canvas.SetLeft(CropRectangle,left);Canvas.SetTop(CropRectangle,top);
        CropXLabel.Text=$"X: {(int)Math.Round(CropX.Value)} px";CropYLabel.Text=$"Y: {(int)Math.Round(CropY.Value)} px";CropEdgeLabel.Text=$"Lado: {(int)Math.Round(CropEdge.Value)} px";
    }
    private void AcceptClick(object sender,RoutedEventArgs e){DialogResult=true;Close();}
    private void CancelClick(object sender,RoutedEventArgs e){DialogResult=false;Close();}
}
