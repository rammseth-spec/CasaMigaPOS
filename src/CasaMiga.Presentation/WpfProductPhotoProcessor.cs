using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CasaMiga.Application;

namespace CasaMiga.Presentation;

// No filesystem access. The original remains untouched; crop coordinates follow EXIF normalization.
public sealed class WpfProductPhotoProcessor : IProductPhotoProcessor
{
    public ProcessedPhoto Process(byte[] original, PhotoCrop crop)
    {
        try { return Decode(original, crop); }
        catch (Exception e) when (e is not ProductPhotoException && e is ArgumentException or IOException or NotSupportedException or System.Runtime.InteropServices.COMException or OverflowException)
        { throw new ProductPhotoException("No se pudo decodificar la fotografía JPEG/PNG."); }
    }
    private static ProcessedPhoto Decode(byte[] bytes, PhotoCrop crop)
    {
        if (bytes.Length is 0 or > ProductPhotoService.MaxOriginalBytes) throw new ProductPhotoException("Máximo 25 MiB.");
        string format;
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        { format = "png"; ValidatePng(bytes); }
        else if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[^2] == 255 && bytes[^1] == 217) format = "jpeg";
        else throw new ProductPhotoException("Selecciona un JPEG o PNG válido.");
        using var stream = new MemoryStream(bytes, false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        if (decoder.Frames.Count != 1 || (format == "png" && decoder is not PngBitmapDecoder) || (format == "jpeg" && decoder is not JpegBitmapDecoder))
            throw new ProductPhotoException("Imagen animada o formato no admitido.");
        var frame = decoder.Frames[0];
        var width = frame.PixelWidth; var height = frame.PixelHeight;
        if (width <= 0 || height <= 0 || width > 12000 || height > 12000 || (long)width * height > 48000000)
            throw new ProductPhotoException("Máximo 12000 píxeles por lado y 48 megapíxeles.");
        int orientation = 1;
        if (frame.Metadata is BitmapMetadata metadata)
        {
            foreach (var query in new[] { "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" })
                if (metadata.ContainsQuery(query) && metadata.GetQuery(query) is {} value) { orientation = Convert.ToInt32(value); break; }
        }
        if (orientation is < 1 or > 8) throw new ProductPhotoException("Orientación EXIF inválida.");
        int orientedWidth = orientation >= 5 ? height : width, orientedHeight = orientation >= 5 ? width : height;
        if (crop.Edge < 256 || crop.X < 0 || crop.Y < 0 || (long)crop.X + crop.Edge > orientedWidth || (long)crop.Y + crop.Edge > orientedHeight)
            throw new ProductPhotoException("Recorte cuadrado inválido; mínimo 256 píxeles.");
        BitmapSource source = frame;
        if (frame.ColorContexts is { Count: > 0 } profiles)
            source = new ColorConvertedBitmap(frame, profiles[0], new ColorContext(PixelFormats.Bgra32), PixelFormats.Bgra32);
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(width * height * 4)];
        converted.CopyPixels(pixels, width * 4, 0); // Fully decode before any publication.
        var edge = Math.Min(512, crop.Edge);
        var output = new byte[edge * edge * 3];
        // Area resampling: bounded output, no second full-size oriented raster, no upscaling.
        for (int y = 0; y < edge; y++) for (int x = 0; x < edge; x++)
        {
            double left = x * (double)crop.Edge / edge, right = (x + 1) * (double)crop.Edge / edge;
            double top = y * (double)crop.Edge / edge, bottom = (y + 1) * (double)crop.Edge / edge;
            double blue = 0, green = 0, red = 0, total = 0;
            for (int cy = (int)top; cy < Math.Ceiling(bottom); cy++) for (int cx = (int)left; cx < Math.Ceiling(right); cx++)
            {
                int ox = crop.X + cx, oy = crop.Y + cy;
                var (sx, sy) = orientation switch
                {
                    2 => (width - 1 - ox, oy), 3 => (width - 1 - ox, height - 1 - oy),
                    4 => (ox, height - 1 - oy), 5 => (oy, ox), 6 => (oy, height - 1 - ox),
                    7 => (width - 1 - oy, height - 1 - ox), 8 => (width - 1 - oy, ox), _ => (ox, oy)
                };
                var p = (sy * width + sx) * 4;
                var weight = (Math.Min(right, cx + 1) - Math.Max(left, cx)) * (Math.Min(bottom, cy + 1) - Math.Max(top, cy));
                double alpha = pixels[p + 3] / 255.0;
                blue += (pixels[p] * alpha + 239 * (1 - alpha)) * weight;
                green += (pixels[p + 1] * alpha + 248 * (1 - alpha)) * weight;
                red += (pixels[p + 2] * alpha + 253 * (1 - alpha)) * weight;
                total += weight;
            }
            int target = (y * edge + x) * 3;
            output[target] = (byte)Math.Round(blue / total); output[target + 1] = (byte)Math.Round(green / total); output[target + 2] = (byte)Math.Round(red / total);
        }
        var bitmap = BitmapSource.Create(edge, edge, 96, 96, PixelFormats.Bgr24, null, output, edge * 3);
        bitmap.Freeze();
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap)); // Fresh frame contains no EXIF/GPS.
        using var targetStream = new MemoryStream(); encoder.Save(targetStream);
        var thumbnail = targetStream.ToArray();
        if (thumbnail.Length > 2 * 1024 * 1024) throw new ProductPhotoException("Derivado demasiado grande.");
        using var verification = new MemoryStream(thumbnail, false);
        var check = new JpegBitmapDecoder(verification, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        if (check.PixelWidth != edge || check.PixelHeight != edge) throw new ProductPhotoException("Derivado inválido.");
        return new(format, width, height, orientation, crop, thumbnail);
    }
    private static void ValidatePng(byte[] data)
    {
        int position = 8; bool header = false, ended = false;
        while (position <= data.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
            if (length > int.MaxValue || (long)position + length + 12 > data.Length) throw new ProductPhotoException("PNG truncado.");
            var type = System.Text.Encoding.ASCII.GetString(data, position + 4, 4);
            if (type == "acTL") throw new ProductPhotoException("PNG animado no admitido.");
            uint crc = uint.MaxValue;
            for (int i = position + 4; i < position + 8 + (int)length; i++)
            { crc ^= data[i]; for (int b = 0; b < 8; b++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1; }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 8 + (int)length, 4))) throw new ProductPhotoException("PNG corrupto.");
            if (!header)
            {
                if (type != "IHDR" || length != 13) throw new ProductPhotoException("PNG inválido.");
                var w = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 8, 4));
                var h = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 12, 4));
                if (w is 0 or > 12000 || h is 0 or > 12000 || (ulong)w * h > 48000000) throw new ProductPhotoException("Dimensiones PNG no admitidas.");
                header = true;
            }
            position += (int)length + 12;
            if (type == "IEND") { ended = length == 0 && position == data.Length; break; }
        }
        if (!ended) throw new ProductPhotoException("PNG incompleto.");
    }
}
