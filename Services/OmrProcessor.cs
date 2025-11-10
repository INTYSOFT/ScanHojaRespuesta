using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Text;



namespace ContrlAcademico.Services
{


    public class OmrProcessor
    {
        public double FillThreshold => _fillThreshold;

        private readonly GridModel _grid;
        private readonly RegionModel _dniRegion;
        private readonly double _fillThreshold; // % de área para considerar marcado
        private readonly double _fillSeparation; // diferencia mínima entre ratios de relleno
        

        readonly GridModel _g;
        readonly Mat _mask;         // máscara elíptica de la burbuja
        readonly double _meanThreshold; // umbral sobre media de gris
        readonly double _deltaMin;      // separación mínima entre 1º y 2º     

        public string ReadDni(Mat threshDni)
        {
            // threshDni es el recorte binarizado de la región DNI
            const int cols = 8, rows = 10;
            int cellW = threshDni.Width  / cols;
            int cellH = threshDni.Height / rows;
            double area = cellW * cellH;
            double minFill = area * _fillThreshold;
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

            double meanThreshold = 180,   // intensidad media > esto → “sin marca”
            double deltaMin = 30,    // si 2º media – 1º media < deltaMin → ambigüedad
            double fillSeparation = 0.08)
        {
            _g = grid;
            _meanThreshold = meanThreshold;
            _deltaMin      = deltaMin;

            _dniRegion     = dniRegion ?? throw new ArgumentNullException(nameof(dniRegion));
            _fillThreshold = fillThreshold;
            _fillSeparation = fillSeparation;


            // Creamos la máscara elíptica del tamaño exacto de la burbuja:
            _mask = new Mat(_g.BubbleH, _g.BubbleW, MatType.CV_8UC1, Scalar.All(0));
            Cv2.Ellipse(
                _mask,
                //new Point(_g.BubbleW/2, _g.BubbleH/2),
                new OpenCvSharp.Point(_g.BubbleW/2, _g.BubbleH/2),
                new OpenCvSharp.Size(_g.BubbleW/2, _g.BubbleH/2),
                angle: 0, startAngle: 0, endAngle: 360,
                color: Scalar.All(255),
                thickness: -1
            );
        }


        public char[] Process(Bitmap warpedBmp)
        {
            // 1) Convertir a Mat y llevar a gris+blur
            using var src = BitmapConverter.ToMat(warpedBmp);
            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(
                gray,
                thresh,
                maxValue: 255,
                adaptiveMethod: AdaptiveThresholdTypes.GaussianC,
                thresholdType: ThresholdTypes.BinaryInv,
                blockSize: 15,
                c: 3);

            int rows = _g.Rows;
            int cols = _g.Cols;
            int blocks = _g.BlockCount;
            int space = _g.BlockSpacing;

            if (rows <= 0 || cols <= 0 || blocks <= 0)
            {
                return Array.Empty<char>();
            }

            char[] answers = new char[rows * blocks];
            int idx = 0;

            // 2) Recorremos cada bloque de preguntas
            for (int b = 0; b < blocks; b++)
            {
                int baseX = _g.StartX + b * (cols * _g.Dx + space);

                for (int r = 0; r < rows; r++, idx++)
                {
                    int y = _g.StartY + r * _g.Dy;

                    var scores = new BubbleScore[cols];

                    for (int c = 0; c < cols; c++)
                    {
                        int x = baseX + c * _g.Dx;
                        int x2 = Math.Clamp(x, 0, gray.Width);
                        int y2 = Math.Clamp(y, 0, gray.Height);
                        int w2 = Math.Min(_g.BubbleW, gray.Width - x2);
                        int h2 = Math.Min(_g.BubbleH, gray.Height - y2);

                        if (w2 <= 0 || h2 <= 0)
                        {
                            scores[c] = new BubbleScore(c, 255.0, 0.0);
                            continue;
                        }

                        using var roiGray = new Mat(gray, new Rect(x2, y2, w2, h2));
                        using var roiThresh = new Mat(thresh, new Rect(x2, y2, w2, h2));
                        using var maskROI = new Mat(_mask, new Rect(0, 0, w2, h2));

                        Scalar mean = Cv2.Mean(roiGray, maskROI);
                        double fillRatio = Cv2.Mean(roiThresh, maskROI).Val0 / 255.0;

                        scores[c] = new BubbleScore(c, mean.Val0, fillRatio);
                    }

                    Array.Sort(scores, BubbleScoreComparer.Instance);

                    var best = scores[0];
                    var second = cols > 1 ? scores[1] : default;

                    bool hasFilled = best.FillRatio >= _fillThreshold || best.Mean <= _meanThreshold;
                    bool separated = cols == 1 ||
                                     (best.FillRatio - second.FillRatio >= _fillSeparation ||
                                      second.Mean - best.Mean >= _deltaMin);

                    answers[idx] = hasFilled && separated
                        ? (char)('A' + best.Option)
                        : '-';
                }
            }

            return answers;
        }

        private readonly struct BubbleScore
        {
            public BubbleScore(int option, double mean, double fillRatio)
            {
                Option = option;
                Mean = mean;
                FillRatio = fillRatio;
            }

            public int Option { get; }
            public double Mean { get; }
            public double FillRatio { get; }
        }

        private sealed class BubbleScoreComparer : IComparer<BubbleScore>
        {
            public static BubbleScoreComparer Instance { get; } = new BubbleScoreComparer();

            private BubbleScoreComparer()
            {
            }

            public int Compare(BubbleScore x, BubbleScore y)
            {
                int fillComparison = y.FillRatio.CompareTo(x.FillRatio);
                if (fillComparison != 0)
                {
                    return fillComparison;
                }

                return x.Mean.CompareTo(y.Mean);
            }
        }

        public char[] old_Process(Bitmap warped)
        {
            // 1) Bitmap → Mat
            Mat src = BitmapConverter.ToMat(warped);

            // 2) Preprocesado: gris → blur → adaptive threshold
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);
            Mat thresh = new Mat();
            Cv2.AdaptiveThreshold(
                gray,
                thresh,
                255,                             // maxValue
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                11,                              // blockSize
                2                                // este es el “C”
            );

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

                        // ——— CLAMP de rectángulo ———
                        int x2 = Math.Clamp(x, 0, thresh.Width);
                        int y2 = Math.Clamp(y, 0, thresh.Height);
                        int w2 = Math.Min(g.BubbleW, thresh.Width  - x2);
                        int h2 = Math.Min(g.BubbleH, thresh.Height - y2);
                        if (w2 <= 0 || h2 <= 0)
                            continue;
                        var rect = new Rect(x2, y2, w2, h2);
                        // ——————————————————————

                        Mat roi = new Mat(thresh, rect);
                        double cnt = Cv2.CountNonZero(roi);
                        if (cnt > maxCount)
                        {
                            maxCount = cnt;
                            bestOpt = c;
                        }
                    }

                    double area = g.BubbleW * g.BubbleH;
                    answers[idx] = (bestOpt >= 0 && maxCount >= area * _fillThreshold)
                        ? (char)('A' + bestOpt)
                        : '-';
                }
            }

            return answers;
        }

    }
}
