using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EndometriosisClient.Services
{
    public class MriPreviewService
    {
        public string CreatePreviewImage(string outputPath)
        {
            int width = 512;
            int height = 512;

            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));

                dc.DrawText(
                    new FormattedText(
                        "MRI PREVIEW",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        26,
                        Brushes.White,
                        1.25),
                    new Point(165, 30));

                dc.DrawRectangle(Brushes.DimGray, null, new Rect(90, 90, 330, 300));

                dc.DrawEllipse(
                    Brushes.Gray,
                    new Pen(Brushes.LightGray, 2),
                    new Point(210, 240),
                    55,
                    85);

                dc.DrawEllipse(
                    Brushes.Gray,
                    new Pen(Brushes.LightGray, 2),
                    new Point(300, 240),
                    55,
                    85);

                dc.DrawText(
                    new FormattedText(
                        "Demonstration source MRI image",
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        18,
                        Brushes.White,
                        1.25),
                    new Point(115, 420));
            }

            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using var stream = new FileStream(outputPath, FileMode.Create);
            encoder.Save(stream);

            return outputPath;
        }
    }
}