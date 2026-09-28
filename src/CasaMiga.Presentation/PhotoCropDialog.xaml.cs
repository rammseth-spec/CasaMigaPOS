using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public partial class PhotoCropDialog : Window
{
    private readonly PhotoCropViewModel model;
    private Point lastPointer;
    private bool dragging;
    private bool resizing;
    public PhotoCrop Crop=>model.Crop;
    public PhotoCropDialog(Window owner,byte[] bytes)
    {
        Owner=owner;InitializeComponent();
        using var stream=new MemoryStream(bytes,false);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        if(decoder.Frames.Count!=1)throw new ProductPhotoException("Selecciona una sola imagen JPEG o PNG.");
        var frame=decoder.Frames[0];var orientation=ReadOrientation(frame);var width=orientation>=5?frame.PixelHeight:frame.PixelWidth;var height=orientation>=5?frame.PixelWidth:frame.PixelHeight;
        frame.Freeze();PreviewImage.Source=frame;PreviewImage.LayoutTransform=OrientationTransform(orientation);model=new(width,height);DataContext=model;model.PropertyChanged+=(_,_)=>UpdateOverlay();
        DimensionsLabel.Text=$"Imagen orientada: {width} × {height} px";Loaded+=(_,_)=>UpdateOverlay();
    }
    private static int ReadOrientation(BitmapFrame frame)
    {
        if(frame.Metadata is not BitmapMetadata metadata)return 1;
        foreach(var query in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})if(metadata.ContainsQuery(query)&&metadata.GetQuery(query) is {} value)return Convert.ToInt32(value);return 1;
    }
    private static Transform OrientationTransform(int orientation)
    {
        var group=new TransformGroup();if(orientation is 2 or 5 or 7)group.Children.Add(new ScaleTransform(-1,1));if(orientation==4)group.Children.Add(new ScaleTransform(1,-1));group.Children.Add(new RotateTransform(orientation switch{3=>180,5=>90,6=>90,7=>270,8=>270,_=>0}));return group;
    }
    private double Scale=>Math.Min(PreviewHost.ActualWidth/model.Width,PreviewHost.ActualHeight/model.Height);
    private void PreviewSizeChanged(object sender,SizeChangedEventArgs e)=>UpdateOverlay();
    private void UpdateOverlay()
    {
        if(PreviewHost.ActualWidth<=0||PreviewHost.ActualHeight<=0)return;var scale=Scale;var left=(PreviewHost.ActualWidth-model.Width*scale)/2+model.X*scale;var top=(PreviewHost.ActualHeight-model.Height*scale)/2+model.Y*scale;var size=model.Edge*scale;
        CropRectangle.Width=size;CropRectangle.Height=size;Canvas.SetLeft(CropRectangle,left);Canvas.SetTop(CropRectangle,top);Canvas.SetLeft(ResizeHandle,left+size-11);Canvas.SetTop(ResizeHandle,top+size-11);
    }
    private void OverlayMouseDown(object sender,MouseButtonEventArgs e)
    {
        lastPointer=e.GetPosition(Overlay);resizing=ResizeHandle.IsMouseOver;var cropBounds=new Rect(Canvas.GetLeft(CropRectangle),Canvas.GetTop(CropRectangle),CropRectangle.Width,CropRectangle.Height);dragging=resizing||cropBounds.Contains(lastPointer);if(dragging){Overlay.CaptureMouse();e.Handled=true;}
    }
    private void OverlayMouseMove(object sender,MouseEventArgs e)
    {
        if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;var point=e.GetPosition(Overlay);var dx=(point.X-lastPointer.X)/Scale;var dy=(point.Y-lastPointer.Y)/Scale;if(resizing)model.ResizeBy(Math.Max(dx,dy));else model.MoveBy(dx,dy);lastPointer=point;e.Handled=true;
    }
    private void OverlayMouseUp(object sender,MouseButtonEventArgs e){dragging=false;resizing=false;Overlay.ReleaseMouseCapture();e.Handled=true;}
    private void AcceptClick(object sender,RoutedEventArgs e){DialogResult=true;Close();}
    private void CancelClick(object sender,RoutedEventArgs e){DialogResult=false;Close();}
}
