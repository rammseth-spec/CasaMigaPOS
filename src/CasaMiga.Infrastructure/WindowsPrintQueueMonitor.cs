using System.ComponentModel;
using System.Runtime.InteropServices;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

/// <summary>
/// Cola de impresión de Windows (spooler) de la impresora de tickets: lista los trabajos pendientes y cancela uno.
/// Sólo actúa sobre la cola indicada; no envía datos a la impresora ni modifica ventas.
/// </summary>
public sealed class WindowsPrintQueueMonitor : IPrintQueueMonitor
{
    private const int JobControlDelete = 5;

    public IReadOnlyList<PrintQueueJob> List(string queueName)
    {
        if(string.IsNullOrWhiteSpace(queueName))throw new ArgumentException("Cola de impresión inválida.");
        if(!OperatingSystem.IsWindows())throw new PrintTransportException("WINDOWS_REQUIRED",false);
        if(!Native.OpenPrinter(queueName,out var printer,IntPtr.Zero))throw Failure("OPEN_PRINTER");
        try
        {
            // Dos llamadas: la primera pide el tamaño; si entra un trabajo entre ambas se reintenta.
            for(var attempt=0;attempt<3;attempt++)
            {
                Native.EnumJobs(printer,0,255,1,IntPtr.Zero,0,out var needed,out _);
                if(needed==0)return [];
                var buffer=Marshal.AllocHGlobal(needed);
                try
                {
                    if(!Native.EnumJobs(printer,0,255,1,buffer,needed,out _,out var count))
                    {
                        if(Marshal.GetLastWin32Error()==122)continue;// ERROR_INSUFFICIENT_BUFFER
                        throw Failure("ENUM_JOBS");
                    }
                    var size=Marshal.SizeOf<Native.JobInfo1>();
                    var jobs=new List<PrintQueueJob>(count);
                    for(var i=0;i<count;i++)
                    {
                        var info=Marshal.PtrToStructure<Native.JobInfo1>(buffer+i*size);
                        jobs.Add(new(info.JobId,Text(info.Document)??"(sin nombre)",Status(info.Status,Text(info.StatusText)),
                            Submitted(info.Submitted),info.TotalPages,info.PagesPrinted,Text(info.UserName)));
                    }
                    return jobs;
                }
                finally{Marshal.FreeHGlobal(buffer);}
            }
            throw new PrintTransportException("ENUM_JOBS_CHANGED",false);
        }
        finally{Native.ClosePrinter(printer);}
    }

    public void Cancel(string queueName,int jobId)
    {
        if(string.IsNullOrWhiteSpace(queueName) || jobId<=0)throw new ArgumentException("Trabajo de impresión inválido.");
        if(!OperatingSystem.IsWindows())throw new PrintTransportException("WINDOWS_REQUIRED",false);
        if(!Native.OpenPrinter(queueName,out var printer,IntPtr.Zero))throw Failure("OPEN_PRINTER");
        try{if(!Native.SetJob(printer,jobId,0,IntPtr.Zero,JobControlDelete))throw Failure("CANCEL_JOB");}
        finally{Native.ClosePrinter(printer);}
    }

    private static string? Text(IntPtr value)=>value==IntPtr.Zero?null:Marshal.PtrToStringUni(value);

    private static DateTimeOffset? Submitted(Native.SystemTime t)
    {
        try{return t.Year==0?null:new DateTimeOffset(t.Year,t.Month,t.Day,t.Hour,t.Minute,t.Second,t.Milliseconds,TimeSpan.Zero);}
        catch(ArgumentOutOfRangeException){return null;}
    }

    private static string Status(int flags,string? driverText)
    {
        // Banderas JOB_STATUS_* de winspool, de la más relevante a la menos.
        var parts=new List<string>();
        if((flags&0x0002)!=0)parts.Add("Error");
        if((flags&0x0020)!=0)parts.Add("Impresora desconectada");
        if((flags&0x0040)!=0)parts.Add("Sin papel");
        if((flags&0x0400)!=0)parts.Add("Requiere atención");
        if((flags&0x0200)!=0)parts.Add("Bloqueado");
        if((flags&0x0004)!=0)parts.Add("Eliminando");
        if((flags&0x0001)!=0)parts.Add("En pausa");
        if((flags&0x0010)!=0)parts.Add("Imprimiendo");
        if((flags&0x0008)!=0)parts.Add("Recibiendo");
        if((flags&0x0080)!=0 || (flags&0x1000)!=0)parts.Add("Impreso");
        if(parts.Count==0)parts.Add(string.IsNullOrWhiteSpace(driverText)?"En espera":driverText.Trim());
        return string.Join(" · ",parts);
    }

    private static PrintTransportException Failure(string operation)
    {
        var code=Marshal.GetLastWin32Error();
        return new($"{operation}_{code}",false,new Win32Exception(code));
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SystemTime
        {
            public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JobInfo1
        {
            public int JobId;
            public IntPtr PrinterName, MachineName, UserName, Document, Datatype, StatusText;
            public int Status, Priority, Position, TotalPages, PagesPrinted;
            public SystemTime Submitted;
        }

        [DllImport("winspool.drv",EntryPoint="OpenPrinterW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenPrinter(string printerName,out IntPtr printer,IntPtr defaults);
        [DllImport("winspool.drv",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClosePrinter(IntPtr printer);
        [DllImport("winspool.drv",EntryPoint="EnumJobsW",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumJobs(IntPtr printer,int firstJob,int numberOfJobs,int level,IntPtr jobs,int bufferSize,out int needed,out int returned);
        [DllImport("winspool.drv",EntryPoint="SetJobW",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetJob(IntPtr printer,int jobId,int level,IntPtr job,int command);
    }
}
