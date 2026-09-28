using System.ComponentModel;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

public sealed class PhotoCropViewModel : INotifyPropertyChanged
{
    private double x;
    private double y;
    private double edge;
    public PhotoCropViewModel(int width,int height)
    {
        if(Math.Min(width,height)<256)throw new ProductPhotoException("La foto necesita al menos 256 píxeles por lado.");
        Width=width;Height=height;edge=Math.Min(width,height);x=(width-edge)/2d;y=(height-edge)/2d;
    }
    public int Width { get; }
    public int Height { get; }
    public double MinimumEdge=>256;
    public double MaximumEdge=>Math.Min(Width,Height);
    public double MaximumX=>Width-Edge;
    public double MaximumY=>Height-Edge;
    public double X { get=>x; set {x=Math.Clamp(value,0,MaximumX);Changed();} }
    public double Y { get=>y; set {y=Math.Clamp(value,0,MaximumY);Changed();} }
    public double Edge { get=>edge; set {edge=Math.Clamp(value,MinimumEdge,MaximumEdge);x=Math.Min(x,MaximumX);y=Math.Min(y,MaximumY);Changed();} }
    public PhotoCrop Crop=>new((int)Math.Round(X),(int)Math.Round(Y),(int)Math.Round(Edge));
    public void MoveBy(double horizontal,double vertical){X+=horizontal;Y+=vertical;}
    public void ResizeBy(double amount)=>Edge+=amount;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
}
