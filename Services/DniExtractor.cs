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
        { 
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

            // 3) Extraer ROI y binarizar
            using var roiMat = new Mat(matPage, rect);
            Cv2.CvtColor(roiMat, roiMat, ColorConversionCodes.BGR2GRAY);
            Cv2.AdaptiveThreshold(
                roiMat, roiMat, 255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv, 11, 2);

            // 4) OMR 8×10
            const int cols = 8, rows = 10;
            int cellW = roiMat.Width  / cols,
                cellH = roiMat.Height / rows;

            var digits = new List<char>(cols);


            for (int c = 0; c < cols; c++)
            {
                int bestVal = 0, bestIdx = -1;
                for (int r = 0; r < rows; r++)
                {
                    var cell = new Rect(c*cellW, r*cellH, cellW, cellH);
                    using var sub = new Mat(roiMat, cell);
                    int cnt = Cv2.CountNonZero(sub);
                    if (cnt > bestVal)
                    {
                        bestVal = cnt;
                        bestIdx = r;
                    }
                }
                // Si fuese necesario ajusta este umbral:
                const int THRESHOLD = 50;
                if (bestIdx < 0 || bestVal < THRESHOLD)
                    digits.Add('-');
                else
                    digits.Add((char)('0' + bestIdx));

                //digits.Add(bestIdx < 0 || bestVal < 50 ? '-' : (char)('0' + bestIdx));
            }

            return string.Join("", digits);
        }

    }
}
