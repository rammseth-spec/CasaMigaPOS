namespace CasaMiga.Infrastructure;

/// <summary>Deterministic one-bit Facebook and Instagram marks for the thermal footer.</summary>
public static class ReceiptSocialRaster
{
    public const int Width=576,Height=40,RowBytes=72;

    public static byte[] Raster()
    {
        var raster=new byte[RowBytes*Height];
        void Set(int x,int y,bool black=true)
        {
            if(x<0||x>=Width||y<0||y>=Height)return;
            var mask=(byte)(0x80>>(x%8));
            if(black)raster[y*RowBytes+x/8]|=mask;else raster[y*RowBytes+x/8]&=(byte)~mask;
        }

        // Facebook: black disk with a compact white "f" cutout.
        // Positions align the marks over the centered account labels printed on the next line.
        const int facebookX=198,centerY=20;
        for(var y=3;y<38;y++)for(var x=181;x<216;x++)
            if((x-facebookX)*(x-facebookX)+(y-centerY)*(y-centerY)<=289)Set(x,y);
        for(var y=8;y<35;y++)for(var x=198;x<204;x++)Set(x,y,false);
        for(var y=8;y<13;y++)for(var x=202;x<211;x++)Set(x,y,false);
        for(var y=17;y<22;y++)for(var x=191;x<207;x++)Set(x,y,false);

        // Instagram: rounded-square outline, lens and status dot.
        const int left=342,right=378,top=2,bottom=38;
        for(var x=left+5;x<=right-5;x++)for(var thickness=0;thickness<3;thickness++){Set(x,top+thickness);Set(x,bottom-thickness);}
        for(var y=top+5;y<=bottom-5;y++)for(var thickness=0;thickness<3;thickness++){Set(left+thickness,y);Set(right-thickness,y);}
        foreach(var (x,y) in new[]{(344,5),(345,4),(346,3),(374,3),(375,4),(376,5),(344,35),(345,36),(346,37),(374,37),(375,36),(376,35)})
            for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++)Set(x+dx,y+dy);
        const int lensX=360,lensY=20;
        for(var y=10;y<=30;y++)for(var x=350;x<=370;x++)
        {var distance=(x-lensX)*(x-lensX)+(y-lensY)*(y-lensY);if(distance is >=49 and <=81)Set(x,y);}
        for(var y=8;y<=12;y++)for(var x=368;x<=372;x++)Set(x,y);
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
