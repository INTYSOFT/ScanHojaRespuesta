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

            // Antes de dividir la región en celdas buscamos la zona que contiene realmente
            // la parrilla de burbujas. Las plantillas suelen dejar márgenes arriba y a los
            // lados para textos, lo que desplaza las marcas si se reparte el ancho total
            // de la ROI. Calculamos el rectángulo "útil" analizando la densidad de tinta
            // por filas y columnas.
            var bubbleBounds = DetectBubbleBounds(roiMat);
            using var bubbleMat = new Mat(roiMat, bubbleBounds);

            // 4) OMR 8×10
            const int cols = 8, rows = 10;

            // Calculamos los bordes exactos de cada celda usando doble precisión para
            // evitar acumulación de errores por redondeos sucesivos. De esta manera las
            // marcas se centran perfectamente en cada burbuja, sin sobrepasar su tamaño.
            var colEdges = BuildEdges(bubbleMat.Width, cols);
            var rowEdges = BuildEdges(bubbleMat.Height, rows);

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

                    var bubbleRectLocal = new Rect(bubbleOffsetX, bubbleOffsetY, bubbleW, bubbleH);
                    bubbleRectLocal = bubbleRectLocal.Intersect(new Rect(0, 0, bubbleMat.Width, bubbleMat.Height));
                    if (bubbleRectLocal.Width <= 0 || bubbleRectLocal.Height <= 0)
                    {
                        continue;
                    }

                    using var sub = new Mat(bubbleMat, bubbleRectLocal);
                    int cnt = Cv2.CountNonZero(sub);
                    if (cnt > bestVal)
                    {
                        bestVal = cnt;
                        bestIdx = r;
                        bestRect = bubbleRectLocal;
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
                    var localRect = bestRect ?? new Rect(
                        bubbleOffsetX,
                        rowEdges[Math.Max(0, bestIdx)],
                        bubbleW,
                        Math.Max(1, (int)Math.Round((rowEdges[Math.Min(rows, bestIdx + 1)] - rowEdges[Math.Max(0, bestIdx)]) * bubbleScaleY)));

                    var highlight = new Rect(
                        rect.X + bubbleBounds.X + localRect.X,
                        rect.Y + bubbleBounds.Y + localRect.Y,
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

        private static int[] BuildEdges(int length, int segments)
        {
            var edges = new int[segments + 1];
            double step = (double)length / segments;
            for (int i = 0; i <= segments; i++)
            {
                edges[i] = (int)Math.Round(i * step);
            }

            // Garantizamos que el último borde coincida exactamente con la dimensión real
            edges[^1] = length;
            return edges;
        }

        private static Rect DetectBubbleBounds(Mat roiMat)
        {
            if (roiMat.Empty())
            {
                return new Rect(0, 0, roiMat.Width, roiMat.Height);
            }

            int[] rowProjection = new int[roiMat.Rows];
            for (int r = 0; r < roiMat.Rows; r++)
            {
                using var row = roiMat.SubMat(r, r + 1, 0, roiMat.Cols);
                rowProjection[r] = Cv2.CountNonZero(row);
            }

            int[] colProjection = new int[roiMat.Cols];
            for (int c = 0; c < roiMat.Cols; c++)
            {
                using var col = roiMat.SubMat(0, roiMat.Rows, c, c + 1);
                colProjection[c] = Cv2.CountNonZero(col);
            }

            int rowThreshold = Math.Max(1, (int)Math.Round(roiMat.Cols * 0.04));
            int colThreshold = Math.Max(1, (int)Math.Round(roiMat.Rows * 0.04));

            int top = 0;
            while (top < rowProjection.Length && rowProjection[top] < rowThreshold)
            {
                top++;
            }

            int bottom = rowProjection.Length - 1;
            while (bottom > top && rowProjection[bottom] < rowThreshold)
            {
                bottom--;
            }

            int left = 0;
            while (left < colProjection.Length && colProjection[left] < colThreshold)
            {
                left++;
            }

            int right = colProjection.Length - 1;
            while (right > left && colProjection[right] < colThreshold)
            {
                right--;
            }

            int width = Math.Max(1, right - left + 1);
            int height = Math.Max(1, bottom - top + 1);

            var bounds = new Rect(left, top, width, height);

            // Si los proyectores no encuentran bordes válidos devolvemos la ROI completa.
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = new Rect(0, 0, roiMat.Width, roiMat.Height);
            }

            return bounds;
        }

    }
}
