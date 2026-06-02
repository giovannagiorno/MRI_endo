using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EndometriosisClient.Services
{
    public class SegmentationStubService
    {
        public string CreateStubResultImage(string outputPath)
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
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        24,
                        Brushes.White,
                        1.25),
                    new Point(170, 40));

                dc.DrawRectangle(Brushes.DimGray, null, new Rect(80, 100, 350, 260));

                dc.DrawEllipse(
                    null,
                    new Pen(Brushes.Red, 4),
                    new Point(280, 230),
                    70,
                    50);

                dc.DrawText(
                    new FormattedText(
                        "Demonstration segmentation area",
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        18,
                        Brushes.Red,
                        1.25),
                    new Point(110, 390));
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