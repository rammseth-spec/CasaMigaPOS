using System.Security.Cryptography;
using CasaMiga.Application;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

[TestClass]
public sealed class OfficialLogoTests
{
    [TestMethod] public void OfficialAssetIsPinnedOneBitAndHasCleanBordersAndTwoBreadCuts()
    {
        var png=OfficialReceiptLogo.ReadPng();
        Assert.AreEqual(OfficialReceiptLogo.Sha256,Convert.ToHexString(SHA256.HashData(png)));
        Assert.AreEqual((byte)1,png[24]);Assert.AreEqual((byte)0,png[25]);
        var raster=OfficialReceiptLogo.Raster();
        bool Black(int x,int y)=>(raster[y*70+x/8]&(128>>(x%8)))!=0;
        for(var x=0;x<560;x++){Assert.IsFalse(Black(x,0));Assert.IsFalse(Black(x,261));}
        for(var y=0;y<262;y++){Assert.IsFalse(Black(0,y));Assert.IsFalse(Black(559,y));}
        // Both white incisions intersect row 95 inside the bread silhouette.
        var whiteRuns=0;var wasWhite=false;
        for(var x=235;x<330;x++){var white=!Black(x,95);if(white&&!wasWhite)whiteRuns++;wasWhite=white;}
        Assert.AreEqual(2,whiteRuns);
    }

    [TestMethod] public void RasterPreservesEveryPixelWithEightWhiteDotsPerSide()
    {
        var raster=OfficialReceiptLogo.Raster();var command=OfficialReceiptLogo.Command();
        CollectionAssert.AreEqual(new byte[]{29,118,48,0,72,0,6,1},command[..8]);
        Assert.AreEqual(8+72*262,command.Length);
        for(var y=0;y<262;y++)
        {
            Assert.AreEqual((byte)0,command[8+y*72]);Assert.AreEqual((byte)0,command[8+y*72+71]);
            CollectionAssert.AreEqual(raster[(y*70)..((y+1)*70)],command[(9+y*72)..(79+y*72)]);
        }
        CollectionAssert.AreEqual(command,OfficialReceiptLogo.Command());
    }

    [TestMethod] public void ControlledLogoPrintIsSingleSafeSubmissionAndCreatesNoDatabase()
    {
        var directory=Path.Combine(Path.GetTempPath(),"CasaMigaLogo-"+Guid.NewGuid().ToString("N"));
        var settings=new PrintingSettings(1,true,"FAKE",48,"ibm850","Partial",false,
            new("PRUEBA CASA MIGA",ClosingMessage:"Gracias por su compra",MonochromeLogoPath:OfficialReceiptLogo.AssetId),2);
        var transport=new Probe();var trace=Path.Combine(directory,"trace.jsonl");
        var test=new ControlledReceiptPrintTest(new Configuration(settings),new EscPosReceiptRenderer(),transport,new SystemClock(),trace);
        try
        {
            var result=test.Run();Assert.AreEqual(ReceiptPrintState.Submitted,result.State);Assert.AreEqual(1,transport.Calls);
            Assert.Contains("TOTAL",result.ReceiptText);Assert.Contains("Gracias por su compra",result.ReceiptText);
            Assert.IsFalse(result.ReceiptText.Contains("PRUEBA CASA MIGA",StringComparison.Ordinal));
            CollectionAssert.AreEqual(new byte[]{10,10,10,10,29,86,1},transport.Payload[^7..]);
            // Exclude the length-delimited raster; data bytes are not commands.
            var textBytes=transport.Payload[(6+OfficialReceiptLogo.Command().Length)..];
            Assert.IsFalse(Convert.ToHexString(textBytes).Contains("1B70",StringComparison.Ordinal));
            Assert.IsFalse(Directory.EnumerateFiles(directory,"*.db").Any());
            Assert.ThrowsExactly<InvalidOperationException>(()=>test.Run());Assert.AreEqual(1,transport.Calls);
        }
        finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    private sealed class Configuration(PrintingSettings settings):IPrintingSettingsProvider
    {public PrintingSettingsResult Read()=>new(settings,null);}
    private sealed class Probe:IReceiptPrintTransport
    {
        public int Calls;public byte[] Payload=[];
        public PrintSubmissionResult Submit(PrintSubmission submission){Calls++;Payload=submission.Payload;return new(123);}
    }
}
