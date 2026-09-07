using System.Text;
using CasaMiga.Application;
namespace CasaMiga.Infrastructure;

// Only the operation ID is durable here. No cart, customer, product or cash amount.
public sealed class CheckoutJournal(string directory) : ICheckoutJournal
{
    private readonly string path=Path.Combine(directory,"checkout.pending");
    public Guid? Read()
    {
        if(!File.Exists(path))return null;
        if(!Guid.TryParseExact(File.ReadAllText(path),"D",out var id) || id==Guid.Empty)
            throw new InvalidDataException("El marcador de cobro requiere revisión.");
        return id;
    }
    public void Save(Guid operation)
    {
        if(operation==Guid.Empty)throw new ArgumentException("Operación inválida.");
        if(Read() is { } existing)
        {
            if(existing!=operation)throw new InvalidOperationException("Existe un cobro pendiente.");
            return;
        }
        Directory.CreateDirectory(directory);
        var temporary=path+".tmp";
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough))
        {stream.Write(Encoding.UTF8.GetBytes(operation.ToString("D")));stream.Flush(true);}
        File.Move(temporary,path,false);
    }
    public void Clear(Guid operation)
    {
        if(Read() is { } existing && existing!=operation)throw new InvalidOperationException("Otro cobro requiere revisión.");
        File.Delete(path);
    }
}
