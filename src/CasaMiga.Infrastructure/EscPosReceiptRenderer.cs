using System.Text;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

public sealed class EscPosReceiptRenderer : IReceiptPayloadRenderer
{
    private static readonly byte[] DoubleWidthAndHeight = [0x1d, 0x21, 0x11];
    private static readonly byte[] DoubleHeight = [0x1d, 0x21, 0x01];
    private static readonly byte[] NormalSize = [0x1d, 0x21, 0x00];
    private static readonly byte[] PartialCut = [0x1d, 0x56, 0x01];

    public byte[] Render(ReceiptDocument document,ReceiptCopyKind copyKind,PrintingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(settings);
        if(settings.CutMode is not ("Disabled" or "Partial") || settings.OpenCashDrawer)
            throw new ArgumentException("Configuracion ESC/POS no permitida.");
        var documentedEncoding=(settings.EncodingName.ToLowerInvariant(),settings.CharacterTable) switch
        {("us-ascii",null)=>true,("ibm850",2)=>true,("windows-1252",16)=>true,_=>false};
        if(!documentedEncoding)
            throw new ArgumentException("La codificacion no coincide con la tabla ESC/POS documentada.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding=Encoding.GetEncoding(settings.EncodingName,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
        var lines=new TextReceiptRenderer(settings.Columns).Render(document,copyKind)
            .Replace("\n","\r\n",StringComparison.Ordinal).Split("\r\n",StringSplitOptions.None);
        var originalHeader=Wrap(document.Business.DisplayName,settings.Columns).ToArray();
        var headerIndex=copyKind==ReceiptCopyKind.Original?0:1;
        using var output=new MemoryStream();
        if(settings.CharacterTable is { } table)
        {
            if(table is <0 or >255)throw new ArgumentOutOfRangeException(nameof(settings));
            output.Write([0x1b,0x74,(byte)table]);
        }
        for(var index=0;index<lines.Length-1;index++)
        {
            if(index==headerIndex)
            {
                if(document.Business.MonochromeLogoPath == OfficialReceiptLogo.AssetId)
                {
                    if(settings.Columns!=48)throw new ArgumentException("Opcion07 requiere 48 columnas.");
                    output.Write([0x1b,0x61,0]);
                    output.Write(OfficialReceiptLogo.Command());
                    output.Write(NormalSize);
                    WriteLine(output,encoding,"");
                    index+=originalHeader.Length-1;
                    continue;
                }
                output.Write(DoubleWidthAndHeight);
                foreach(var headerLine in Wrap(document.Business.DisplayName,settings.Columns/2))
                    WriteLine(output,encoding,Center(headerLine,settings.Columns/2));
                output.Write(NormalSize);
                index+=originalHeader.Length-1;
                continue;
            }
            if(lines[index].StartsWith("TOTAL",StringComparison.Ordinal))
            {
                output.Write(DoubleHeight);WriteLine(output,encoding,lines[index]);output.Write(NormalSize);
                continue;
            }
            WriteLine(output,encoding,lines[index]);
        }
        if(settings.CutMode=="Partial")
        {
            output.Write([0x0a,0x0a,0x0a,0x0a]);
            output.Write(PartialCut);
        }
        return output.ToArray();
    }

    private static void WriteLine(Stream output,Encoding encoding,string line)
        =>output.Write(encoding.GetBytes(line+"\r\n"));

    private static string Center(string value,int width)
        =>value.Length>=width?value:new string(' ',(width-value.Length)/2)+value;

    private static IEnumerable<string> Wrap(string value,int width)
    {
        var remaining=value.Replace("\r","").Replace('\n',' ').Trim();
        if(remaining.Length==0){yield return "";yield break;}
        while(remaining.Length>width)
        {
            var split=remaining.LastIndexOf(' ',width);
            if(split<1)split=width;
            yield return remaining[..split].TrimEnd();remaining=remaining[split..].TrimStart();
        }
        yield return remaining;
    }
}
