using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ContrlAcademico;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ContrlAcademico.Services
{
    public class DniExtractor
    {
        readonly ConfigModel _cfg;
        public DniExtractor(ConfigModel cfg) => _cfg = cfg;

        public string Extract(Bitmap fullPageBmp)
            => Extract(fullPageBmp, generateDebugImage: false, out _);

        public string Extract(Bitmap fullPageBmp, bool generateDebugImage, out Bitmap? debugImage)
        {
            debugImage = null;

            // 1) Convertir a Mat ya alineada
            using var matPage = BitmapConverter.ToMat(fullPageBmp);

            int W = matPage.Width,
                H = matPage.Height;

            // 2) Calcular rect usando NormRoiModel
            var rn = _cfg.DniRegionNorm;
            int x = (int)Math.Round(rn.X * W);
            int y = (int)Math.Round(rn.Y * H);
            int w = (int)Math.Round(rn.W * W);
            int h = (int)Math.Round(rn.H * H);

            var rect = new Rect(x, y, w, h);
            rect = rect.Intersect(new Rect(0, 0, W, H));
            if (rect.Width <= 0 || rect.Height <= 0)
                throw new InvalidOperationException($"Region DNI fuera de página: {rect}");

            Mat? debugMat = null;
            if (generateDebugImage)
            {
                debugMat = matPage.Clone();
                Cv2.Rectangle(debugMat, rect, Scalar.LimeGreen, 2);
            }

            // 3) Extraer ROI y binarizar
            using var roiMat = new Mat(matPage, rect);
            Cv2.CvtColor(roiMat, roiMat, ColorConversionCodes.BGR2GRAY);
            Cv2.AdaptiveThreshold(
                roiMat, roiMat, 255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv, 11, 2);

            // 4) OMR 8×10
            const int cols = 8, rows = 10;

            // calculamos usando pasos decimales para asegurar celdas uniformes
            var colEdges = new int[cols + 1];
            var rowEdges = new int[rows + 1];
            colEdges[0] = 0;
            rowEdges[0] = 0;
            double stepW = roiMat.Width  / (double)cols;
            double stepH = roiMat.Height / (double)rows;
            for (int c = 1; c <= cols; c++)
                colEdges[c] = c == cols ? roiMat.Width : (int)Math.Round(stepW * c);
            for (int r = 1; r <= rows; r++)
                rowEdges[r] = r == rows ? roiMat.Height : (int)Math.Round(stepH * r);

            static Rect ShrinkRect(Rect source, double marginX, double marginY)
            {
                int shrinkX = (int)Math.Round(source.Width * marginX);
                int shrinkY = (int)Math.Round(source.Height * marginY);
                int newW = Math.Max(1, source.Width - 2 * shrinkX);
                int newH = Math.Max(1, source.Height - 2 * shrinkY);
                return new Rect(
                    source.X + shrinkX,
                    source.Y + shrinkY,
                    newW,
                    newH);
            }

            var digits = new List<char>(cols);

            const double marginRatio = 0.18; // deja la parte central de la burbuja
            const int THRESHOLD = 50;

            for (int c = 0; c < cols; c++)
            {
                int bestVal = 0, bestIdx = -1;
                for (int r = 0; r < rows; r++)
                {
                    var baseCell = new Rect(
                        colEdges[c],
                        rowEdges[r],
                        Math.Max(1, colEdges[c + 1] - colEdges[c]),
                        Math.Max(1, rowEdges[r + 1] - rowEdges[r]));

                    var cell = ShrinkRect(baseCell, marginRatio, marginRatio);
                    using var sub = new Mat(roiMat, cell);

                    using var mask = Mat.Zeros(sub.Size(), MatType.CV_8UC1);
                    var center = new Point(mask.Width / 2, mask.Height / 2);
                    int radius = (int)Math.Round(Math.Min(mask.Width, mask.Height) * 0.45);
                    Cv2.Circle(mask, center, Math.Max(1, radius), Scalar.White, -1);

                    using var masked = new Mat();
                    Cv2.BitwiseAnd(sub, mask, masked);
                    int cnt = Cv2.CountNonZero(masked);

                    if (cnt > bestVal)
                    {
                        bestVal = cnt;
                        bestIdx = r;
                    }
                }

                bool validDigit = bestIdx >= 0 && bestVal >= THRESHOLD;
                digits.Add(validDigit ? (char)('0' + bestIdx) : '-');

                if (generateDebugImage && debugMat is not null && validDigit)
                {
                    var baseCell = new Rect(
                        colEdges[c],
                        rowEdges[bestIdx],
                        Math.Max(1, colEdges[c + 1] - colEdges[c]),
                        Math.Max(1, rowEdges[bestIdx + 1] - rowEdges[bestIdx]));
                    var highlight = ShrinkRect(baseCell, marginRatio, marginRatio);
                    highlight = new Rect(
                        rect.X + highlight.X,
                        rect.Y + highlight.Y,
                        highlight.Width,
                        highlight.Height);
                    highlight = highlight.Intersect(new Rect(0, 0, W, H));
                    if (highlight.Width > 0 && highlight.Height > 0)
                    {
                        Cv2.Rectangle(debugMat, highlight, Scalar.LimeGreen, 2);
                    }
                }
            }

            if (generateDebugImage && debugMat is not null)
            {
                debugImage = BitmapConverter.ToBitmap(debugMat);
                debugMat.Dispose();
            }

            return string.Join("", digits);
        }

    }
}
