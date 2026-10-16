namespace CasaMiga.Infrastructure;

/// <summary>A small deterministic one-bit heart for the customer-facing footer.</summary>
public static class ReceiptHeartRaster
{
    public const int Width=32,Height=16,RowBytes=4;
    private static readonly string[] Rows=
    [
        "      ######    ######",
        "    ##########  ##########",
        "   ########################",
        "  ##########################",
        "  ##########################",
        "   ########################",
        "    ######################",
        "     ####################",
        "      ##################",
        "       ################",
        "        ##############",
        "          ##########",
        "           ########",
        "            ######",
        "             ####",
        "              ##"
    ];

    public static byte[] Raster()
    {
        var raster=new byte[RowBytes*Height];
        for(var y=0;y<Height;y++)
            for(var x=0;x<Math.Min(Width,Rows[y].Length);x++)
                if(Rows[y][x]=='#')raster[y*RowBytes+x/8]|=(byte)(0x80>>(x%8));
        return raster;
    }

    public static byte[] Command()
    {
        using var output=new MemoryStream();
        output.Write([0x1d,0x76,0x30,0,RowBytes,0,Height,0]);
        output.Write(Raster());
        return output.ToArray();
    }
}
