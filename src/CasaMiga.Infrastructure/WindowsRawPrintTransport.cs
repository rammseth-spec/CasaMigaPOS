using System.ComponentModel;
using System.Runtime.InteropServices;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

public sealed class WindowsRawPrintTransport : IReceiptPrintTransport
{
    public PrintSubmissionResult Submit(PrintSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if(string.IsNullOrWhiteSpace(submission.QueueName) || submission.Payload.Length==0)
            throw new ArgumentException("Trabajo de impresión inválido.");
        if(!OperatingSystem.IsWindows())throw new PrintTransportException("WINDOWS_REQUIRED",false);
        return SubmitSerialized(submission);
    }

    private static PrintSubmissionResult SubmitSerialized(PrintSubmission submission)
    {
        if(!Native.OpenPrinter(submission.QueueName,out var printer,IntPtr.Zero))throw Failure("OPEN_PRINTER",false);
        var started=false;var page=false;var job=0;
        try
        {
            var info=new Native.DocInfo(submission.JobName,null,"RAW");
            job=Native.StartDocPrinter(printer,1,ref info);
            if(job<=0)throw Failure("START_DOCUMENT",false);
            started=true;
            if(!Native.StartPagePrinter(printer))throw Failure("START_PAGE",true);
            page=true;
            if(!Native.WritePrinter(printer,submission.Payload,submission.Payload.Length,out var written)
                || written!=submission.Payload.Length)throw Failure("WRITE_PRINTER",true);
            var pageEnded=Native.EndPagePrinter(printer);page=false;
            if(!pageEnded)throw Failure("END_PAGE",true);
            var documentEnded=Native.EndDocPrinter(printer);started=false;
            if(!documentEnded)throw Failure("END_DOCUMENT",true);
            return new(job);
        }
        finally
        {
            if(page)Native.EndPagePrinter(printer);
            if(started)Native.EndDocPrinter(printer);
            Native.ClosePrinter(printer);
        }
    }

    private static PrintTransportException Failure(string operation,bool uncertain)
    {
        var code=Marshal.GetLastWin32Error();
        return new($"{operation}_{code}",uncertain,new Win32Exception(code));
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        internal struct DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
            public DocInfo(string documentName,string? outputFile,string dataType)
            {DocumentName=documentName;OutputFile=outputFile;DataType=dataType;}
        }
        [DllImport("winspool.drv",EntryPoint="OpenPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool OpenPrinter(string name,out IntPtr printer,IntPtr defaults);
        [DllImport("winspool.drv",EntryPoint="StartDocPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]
        internal static extern int StartDocPrinter(IntPtr printer,int level,ref DocInfo info);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StartPagePrinter(IntPtr printer);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WritePrinter(IntPtr printer,byte[] bytes,int count,out int written);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EndPagePrinter(IntPtr printer);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EndDocPrinter(IntPtr printer);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClosePrinter(IntPtr printer);
    }
}

public sealed class SerializedReceiptPrintTransport(IReceiptPrintTransport inner) : IReceiptPrintTransport
{
    private readonly SemaphoreSlim gate=new(1,1);
    public PrintSubmissionResult Submit(PrintSubmission submission)
    {
        gate.Wait();
        try{return inner.Submit(submission);}
        finally{gate.Release();}
    }
}
