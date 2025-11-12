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

            // Calculamos los bordes exactos de cada celda usando doble precisión para
            // evitar acumulación de errores por redondeos sucesivos. De esta manera las
            // marcas se centran perfectamente en cada burbuja, sin sobrepasar su tamaño.
            var colEdges = new int[cols + 1];
            var rowEdges = new int[rows + 1];

            double colStep = (double)roiMat.Width / cols;
            double rowStep = (double)roiMat.Height / rows;
            for (int i = 0; i <= cols; i++)
            {
                colEdges[i] = (int)Math.Round(i * colStep);
            }
            for (int i = 0; i <= rows; i++)
            {
                rowEdges[i] = (int)Math.Round(i * rowStep);
            }

            // Garantizamos que el último borde coincida exactamente con el tamaño real.
            colEdges[^1] = roiMat.Width;
            rowEdges[^1] = roiMat.Height;

            // El diámetro visible de la burbuja es menor que la celda total. Usamos un
            // factor empírico basado en las plantillas de examen para encajar el
            // rectángulo en la burbuja real y evitar capturar ruido exterior.
            const double bubbleScaleX = 0.78; // relación burbuja/ancho de celda
            const double bubbleScaleY = 0.78; // relación burbuja/alto de celda

            var digits = new List<char>(cols);


            for (int c = 0; c < cols; c++)
            {
                int bestVal = 0, bestIdx = -1;
                Rect? bestRect = null;
                int cellX = colEdges[c];
                int cellX2 = colEdges[c + 1];
                int cellWidth = Math.Max(1, cellX2 - cellX);
                int bubbleW = Math.Max(1, (int)Math.Round(cellWidth * bubbleScaleX));
                int bubbleOffsetX = cellX + (cellWidth - bubbleW) / 2;

                for (int r = 0; r < rows; r++)
                {
                    int cellY = rowEdges[r];
                    int cellY2 = rowEdges[r + 1];
                    int cellHeight = Math.Max(1, cellY2 - cellY);
                    int bubbleH = Math.Max(1, (int)Math.Round(cellHeight * bubbleScaleY));
                    int bubbleOffsetY = cellY + (cellHeight - bubbleH) / 2;

                    var bubbleRect = new Rect(bubbleOffsetX, bubbleOffsetY, bubbleW, bubbleH);
                    bubbleRect = bubbleRect.Intersect(new Rect(0, 0, roiMat.Width, roiMat.Height));
                    if (bubbleRect.Width <= 0 || bubbleRect.Height <= 0)
                    {
                        continue;
                    }

                    using var sub = new Mat(roiMat, bubbleRect);
                    int cnt = Cv2.CountNonZero(sub);
                    if (cnt > bestVal)
                    {
                        bestVal = cnt;
                        bestIdx = r;
                        bestRect = bubbleRect;
                    }
                }

                const double MIN_FILL_RATIO = 0.18;
                int dynamicThreshold = bestRect.HasValue
                    ? Math.Max(1, (int)Math.Round(bestRect.Value.Width * bestRect.Value.Height * MIN_FILL_RATIO))
                    : 50;
                bool validDigit = bestIdx >= 0 && bestVal >= dynamicThreshold;
                digits.Add(validDigit ? (char)('0' + bestIdx) : '-');

                if (generateDebugImage && debugMat is not null && validDigit)
                {
                    var localRect = bestRect ?? new Rect(bubbleOffsetX, rowEdges[bestIdx], bubbleW,
                        Math.Max(1, (int)Math.Round((rowEdges[bestIdx + 1] - rowEdges[bestIdx]) * bubbleScaleY)));
                    int highlightX = rect.X + localRect.X;
                    int highlightY = rect.Y + localRect.Y;

                    var highlight = new Rect(
                        highlightX,
                        highlightY,
                        localRect.Width,
                        localRect.Height);
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
