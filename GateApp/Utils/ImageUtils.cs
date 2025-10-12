using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace GateApp.Utils;

public static class ImageUtils
{
    public static byte[] MatToJpegBytes(Mat frame, int quality = 90)
    {
        if (frame.Empty())
        {
            return Array.Empty<byte>();
        }

        var encodeParams = new[]
        {
            (int)ImwriteFlags.JpegQuality,
            quality
        };

        Cv2.ImEncode(".jpg", frame, out var buffer, encodeParams);
        return buffer.ToArray();
    }

    public static Bitmap MatToBitmap(Mat frame)
    {
        if (frame.Empty())
        {
            throw new ArgumentException("Frame is empty", nameof(frame));
        }

        return BitmapConverter.ToBitmap(frame);
    }
}
