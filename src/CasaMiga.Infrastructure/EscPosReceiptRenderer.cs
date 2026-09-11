using System.Text;
using CasaMiga.Application;

namespace CasaMiga.Infrastructure;

// Block A deliberately emits text bytes only. Device initialization, code-page
// selection, cut and drawer commands remain disabled until Qian documentation is verified.
public sealed class EscPosReceiptRenderer : IReceiptPayloadRenderer
{
    public byte[] Render(ReceiptDocument document,ReceiptCopyKind copyKind,PrintingSettings settings)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding=Encoding.GetEncoding(settings.EncodingName,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
        var text=new TextReceiptRenderer(settings.Columns).Render(document,copyKind).Replace("\n","\r\n",StringComparison.Ordinal);
        return encoding.GetBytes(text);
    }
}
