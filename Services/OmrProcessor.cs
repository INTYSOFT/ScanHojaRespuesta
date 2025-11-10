using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace ContrlAcademico.Services
{
    public class OmrProcessor
    {
        public double FillThreshold => _bubbleFillThreshold;

        private readonly GridModel _grid;
        private readonly RegionModel _dniRegion;

        private readonly GridModel _g;
        private readonly Mat _mask;         // máscara elíptica de la burbuja
        private readonly double _meanThreshold; // umbral sobre media de gris
        private readonly double _deltaMin;      // separación mínima entre 1º y 2º por media
        private readonly double _bubbleFillThreshold; // % mínimo de relleno para preguntas
        private readonly double _fillSeparation;      // diferencia mínima de relleno entre 1º y 2º
        private readonly double _dniFillThreshold;    // % mínimo de relleno para DNI

        public string ReadDni(Mat threshDni)
        {
            // threshDni es el recorte binarizado de la región DNI
            const int cols = 8, rows = 10;
            int cellW = threshDni.Width  / cols;
            int cellH = threshDni.Height / rows;
            double area = cellW * cellH;
            double minFill = area * _dniFillThreshold;
            var sb = new StringBuilder(8);

            for (int c = 0; c < cols; c++)
            {
                double maxCnt = 0;
                int bestRow = -1;
                for (int r = 0; r < rows; r++)
                {
                    var cellRect = new Rect(c * cellW, r * cellH, cellW, cellH);
                    using var cell = new Mat(threshDni, cellRect);
                    double cnt = Cv2.CountNonZero(cell);
                    if (cnt > maxCnt)
                    {
                        maxCnt = cnt;
                        bestRow = r;
                    }
                }
                sb.Append(bestRow < 0 || maxCnt < minFill
                          ? '-'
                          : (char)('0' + bestRow));
            }

            return sb.ToString();
        }

        public OmrProcessor(
            GridModel grid,
            RegionModel dniRegion,         // ← nuevo parámetro
            double fillThreshold = 0.5,
            double fillSeparation = 0.12,
            double meanThreshold = 180,   // intensidad media > esto → “sin marca”
            double deltaMin = 30,          // si 2º media – 1º media < deltaMin → ambigüedad
            double dniFillThreshold = 0.5)
        {
            _g = grid;
            _grid = grid;
            _meanThreshold = meanThreshold;
            _deltaMin      = deltaMin;

            _dniRegion          = dniRegion ?? throw new ArgumentNullException(nameof(dniRegion));
            _bubbleFillThreshold = fillThreshold;
            _fillSeparation      = fillSeparation;
            _dniFillThreshold    = dniFillThreshold;

            // Creamos la máscara elíptica del tamaño exacto de la burbuja:
            _mask = new Mat(_g.BubbleH, _g.BubbleW, MatType.CV_8UC1, Scalar.All(0));
            Cv2.Ellipse(
                _mask,
                new Point(_g.BubbleW / 2, _g.BubbleH / 2),
                new Size(_g.BubbleW / 2, _g.BubbleH / 2),
                angle: 0,
                startAngle: 0,
                endAngle: 360,
                color: Scalar.All(255),
                thickness: -1);
        }

        public char[] Process(Bitmap warpedBmp)
        {
            using var src = BitmapConverter.ToMat(warpedBmp);
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);

            using var binary = new Mat();
            Cv2.AdaptiveThreshold(
                gray,
                binary,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                25,
                5);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);

            int rows = _g.Rows;
            int cols = _g.Cols;
            int blocks = _g.BlockCount;
            int space = _g.BlockSpacing;

            char[] answers = new char[rows * blocks];
            int idx = 0;

            for (int b = 0; b < blocks; b++)
            {
                int baseX = _g.StartX + b * (cols * _g.Dx + space);

                for (int r = 0; r < rows; r++, idx++)
                {
                    int y = _g.StartY + r * _g.Dy;

                    var stats = new List<(int opt, double fill, double mean)>(cols);

                    for (int c = 0; c < cols; c++)
                    {
                        int x = baseX + c * _g.Dx;
                        int x2 = Math.Clamp(x, 0, gray.Width);
                        int y2 = Math.Clamp(y, 0, gray.Height);
                        int w2 = Math.Min(_g.BubbleW, gray.Width - x2);
                        int h2 = Math.Min(_g.BubbleH, gray.Height - y2);

                        if (w2 <= 0 || h2 <= 0)
                        {
                            stats.Add((c, 0, 255.0));
                            continue;
                        }

                        using var roiGray = new Mat(gray, new Rect(x2, y2, w2, h2));
                        using var roiBinary = new Mat(binary, new Rect(x2, y2, w2, h2));
                        using var maskROI = new Mat(_mask, new Rect(0, 0, w2, h2));

                        double fillRatio = Cv2.Mean(roiBinary, maskROI).Val0 / 255.0;
                        double mean = Cv2.Mean(roiGray, maskROI).Val0;

                        stats.Add((c, fillRatio, mean));
                    }

                    var orderByFill = stats
                        .OrderByDescending(t => t.fill)
                        .ToArray();

                    var bestFill = orderByFill.First();
                    var secondFill = orderByFill.Length > 1 ? orderByFill[1] : (bestFill.opt, 0.0, 255.0);

                    bool fillConfident = bestFill.fill >= _bubbleFillThreshold &&
                                         (orderByFill.Length == 1 ||
                                          bestFill.fill - secondFill.fill >= _fillSeparation);

                    if (fillConfident)
                    {
                        answers[idx] = (char)('A' + bestFill.opt);
                        continue;
                    }

                    var orderByMean = stats
                        .OrderBy(t => t.mean)
                        .ToArray();

                    var bestMean = orderByMean.First();
                    var secondMean = orderByMean.Length > 1 ? orderByMean[1] : (bestMean.opt, bestMean.fill, 255.0);

                    bool meanConfident = bestMean.mean <= _meanThreshold &&
                                         (orderByMean.Length == 1 ||
                                          secondMean.mean - bestMean.mean >= _deltaMin);

                    if (meanConfident)
                    {
                        answers[idx] = (char)('A' + bestMean.opt);
                    }
                    else if (bestFill.fill >= Math.Max(_bubbleFillThreshold * 0.6, 0.08) &&
                             (orderByFill.Length == 1 ||
                              bestFill.fill - secondFill.fill >= Math.Max(_fillSeparation * 0.6, 0.04)))
                    {
                        answers[idx] = (char)('A' + bestFill.opt);
                    }
                    else
                    {
                        answers[idx] = '-';
                    }
                }
            }

            return answers;
        }

        public char[] old_Process(Bitmap warped)
        {
            Mat src = BitmapConverter.ToMat(warped);

            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
            Mat thresh = new Mat();
            Cv2.AdaptiveThreshold(
                gray,
                thresh,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                11,
                2);

            var g = _grid;
            int rows = g.Rows;
            int cols = g.Cols;
            int blocks = g.BlockCount;
            int spacing = g.BlockSpacing;
            char[] answers = new char[rows * blocks];
            int idx = 0;

            for (int b = 0; b < blocks; b++)
            {
                int blockX = g.StartX + b * (cols * g.Dx + spacing);

                for (int r = 0; r < rows; r++, idx++)
                {
                    double maxCount = 0;
                    int bestOpt = -1;
                    int y = g.StartY + r * g.Dy;

                    for (int c = 0; c < cols; c++)
                    {
                        int x = blockX + c * g.Dx;

                        int x2 = Math.Clamp(x, 0, thresh.Width);
                        int y2 = Math.Clamp(y, 0, thresh.Height);
                        int w2 = Math.Min(g.BubbleW, thresh.Width - x2);
                        int h2 = Math.Min(g.BubbleH, thresh.Height - y2);
                        if (w2 <= 0 || h2 <= 0)
                            continue;
                        var rect = new Rect(x2, y2, w2, h2);

                        Mat roi = new Mat(thresh, rect);
                        double cnt = Cv2.CountNonZero(roi);
                        if (cnt > maxCount)
                        {
                            maxCount = cnt;
                            bestOpt = c;
                        }
                    }

                    double area = g.BubbleW * g.BubbleH;
                    answers[idx] = (bestOpt >= 0 && maxCount >= area * _bubbleFillThreshold)
                        ? (char)('A' + bestOpt)
                        : '-';
                }
            }

            return answers;
        }
    }
}
