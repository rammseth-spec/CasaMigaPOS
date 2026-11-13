namespace CasaMiga.Presentation;

// La captura de lotes va en este archivo porque usa las fotos de WPF (BatchEntryViewModel → ProductPhotoVisual): CasaMiga.Tests compila
// ProductionViewModel.cs sin WPF y no enlaza este archivo; la app y CasaMiga.PhotoTests sí lo tienen.
public sealed partial class ProductionViewModel
{
    /// <summary>Captura de lotes con la cuadrícula de panes (sólo para quien puede recibir y configurar inventario).</summary>
    public BatchEntryViewModel? Lotes{get;set;} public bool HasLotes=>Lotes is {CanCapture:true};
}
