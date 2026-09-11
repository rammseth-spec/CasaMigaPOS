using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;

namespace CasaMiga.Infrastructure;

/// <summary>Decodes the pinned, approved 1-bit PNG without resampling or redrawing.</summary>
public static class OfficialReceiptLogo
{
    public const string AssetId = "Opcion07:06D4ADE3AE160967FCD21E958458C17113BEF3C4A8E03F15D63115530AE821C6";
    public const string Sha256 = "06D4ADE3AE160967FCD21E958458C17113BEF3C4A8E03F15D63115530AE821C6";
    public const int Width = 560, Height = 262, RowBytes = 70;

    public static byte[] ReadPng()
    {
        using var source = typeof(OfficialReceiptLogo).Assembly.GetManifestResourceStream("CasaMiga.Opcion07.Ticket.png")
            ?? throw new InvalidDataException("Falta el logo oficial Opcion07.");
        using var buffer = new MemoryStream(); source.CopyTo(buffer); return buffer.ToArray();
    }

    public static byte[] Raster()
    {
        var png = ReadPng();
        if (Convert.ToHexString(SHA256.HashData(png)) != Sha256
            || BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4)) != Width
            || BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4)) != Height
            || png[24] != 1 || png[25] != 0 || png[28] != 0)
            throw new InvalidDataException("Logo oficial alterado o formato inesperado.");
        using var compressed = new MemoryStream();
        for (var offset=8; offset<png.Length;)
        {
            var length=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset,4));
            if (png.AsSpan(offset+4,4).SequenceEqual("IDAT"u8)) compressed.Write(png,offset+8,length);
            offset=checked(offset+12+length);
        }
        compressed.Position=0;
        using var zlib=new ZLibStream(compressed,CompressionMode.Decompress);
        var rows=new byte[(RowBytes+1)*Height]; zlib.ReadExactly(rows);
        if(zlib.ReadByte()!=-1)throw new InvalidDataException("Raster excedido.");
        var pixels=new byte[RowBytes*Height];
        for(var y=0;y<Height;y++)
        {
            var filter=rows[y*(RowBytes+1)];
            for(var x=0;x<RowBytes;x++)
            {
                var left=x>0?pixels[y*RowBytes+x-1]:0;
                var up=y>0?pixels[(y-1)*RowBytes+x]:0;
                var diagonal=x>0&&y>0?pixels[(y-1)*RowBytes+x-1]:0;
                var predictor=filter switch {0=>0,1=>left,2=>up,3=>(left+up)/2,4=>Paeth(left,up,diagonal),_=>throw new InvalidDataException("Filtro PNG inválido.")};
                pixels[y*RowBytes+x]=unchecked((byte)(rows[y*(RowBytes+1)+1+x]+predictor));
            }
        }
        // PNG grayscale: 0 black; ESC/POS: 1 prints. Preserve MSB-first row order.
        for(var i=0;i<pixels.Length;i++)pixels[i]=(byte)~pixels[i];
        return pixels;
    }

    public static byte[] Command()
    {
        // 576-dot canvas: one white byte on each side, original 560 dots unchanged.
        using var output=new MemoryStream();
        output.Write([0x1d,0x76,0x30,0,72,0,6,1]);
        var raster=Raster();
        for(var y=0;y<Height;y++){output.WriteByte(0);output.Write(raster,y*RowBytes,RowBytes);output.WriteByte(0);}
        return output.ToArray();
    }

    private static int Paeth(int a,int b,int c)
    {
        var p=a+b-c;var pa=Math.Abs(p-a);var pb=Math.Abs(p-b);var pc=Math.Abs(p-c);
        return pa<=pb&&pa<=pc?a:pb<=pc?b:c;
    }
}
