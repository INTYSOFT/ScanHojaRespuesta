using System;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ContrlAcademico.Services
{
    public class RotationCorrector
    {
        /// <summary>
        /// Encuentra el ángulo de inclinación de la página completa con MinAreaRect
        /// y la rota para que quede horizontal. No recorta nada más.
        /// </summary>
        public Bitmap Correct(Bitmap srcBmp)
        {
            // 1) Convertir a Mat y pasar a gris
            var src = BitmapConverter.ToMat(srcBmp);
            var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            // 2) Umbral básico para extraer todo el contenido impreso
            var bin = new Mat();
            Cv2.Threshold(gray, bin, 0, 255,
                ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            // 3) Junta los blancos para tener una sola gran mancha
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(15, 15));
            Cv2.Dilate(bin, bin, kernel);

            // 4) Todos los puntos blancos en un solo arreglo
            var pts = new List<Point2f>();
            for (int y = 0; y < bin.Rows; y++)
            {
                for (int x = 0; x < bin.Cols; x++)
                {
                    if (bin.At<byte>(y, x) == 255)
                        pts.Add(new Point2f(x, y));
                }
            }
            if (pts.Count < 10)
                return srcBmp; // no hay suficiente contenido para deskew

            // 5) MinAreaRect para sacar el rectángulo mínimo que cubre todo
            var box = Cv2.MinAreaRect(pts.ToArray());
            float angle = box.Angle;
            // Ajuste: OpenCV da ángulos en (-90,0]
            if (angle < -45) angle += 90;

            // 6) Rotar alrededor del centro
            var center = new Point2f(src.Width/2f, src.Height/2f);
            var M = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            var rotated = new Mat();
            Cv2.WarpAffine(src, rotated, M,
                new OpenCvSharp.Size(src.Width, src.Height),
                InterpolationFlags.Linear,
                BorderTypes.Constant,
                Scalar.White);

            return BitmapConverter.ToBitmap(rotated);
        }
    }
}
