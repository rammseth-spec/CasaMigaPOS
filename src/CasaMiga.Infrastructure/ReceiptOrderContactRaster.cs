namespace CasaMiga.Infrastructure;

/// <summary>A compact deterministic one-bit phone-in-circle mark for the ordering contact block.</summary>
public static class ReceiptOrderContactRaster
{
    public const int Width=32,Height=32,RowBytes=4;

    public static byte[] Raster()
    {
        var raster=new byte[RowBytes*Height];
        void Set(int x,int y)
        {
            if(x is <0 or >=Width||y is <0 or >=Height)return;
            raster[y*RowBytes+x/8]|=(byte)(0x80>>(x%8));
        }

        // Two-pixel circular outline, kept local and deterministic for 1-bit thermal output.
        for(var y=0;y<Height;y++)for(var x=0;x<Width;x++)
        {
            var dx=x-15.5;var dy=y-15.5;var distance=dx*dx+dy*dy;
            if(distance is >=182 and <=225)Set(x,y);
        }

        // A simple handset silhouette inside the circle; no Unicode or font glyphs are involved.
        for(var y=8;y<=12;y++)for(var x=8;x<=11;x++)Set(x,y);
        for(var y=19;y<=23;y++)for(var x=20;x<=23;x++)Set(x,y);
        for(var step=0;step<=10;step++)
        {
            var x=10+step;var y=11+step;
            Set(x,y);Set(x+1,y);Set(x,y+1);
        }
        for(var x=10;x<=14;x++)Set(x,13);
        for(var x=18;x<=22;x++)Set(x,20);
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
