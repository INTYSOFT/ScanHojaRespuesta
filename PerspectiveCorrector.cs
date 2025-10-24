using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Linq;

namespace ContrlAcademico

{
    

    public class PerspectiveCorrector
    {
        public Bitmap Correct(Bitmap input, out Point2f[] srcCorners, out Point2f[] dstCorners)
        {
            // 1) Bitmap → Mat
            //Mat src = input.ToMat();
            Mat src = BitmapConverter.ToMat(input);

            // 2) Gris + Blur + Canny
            Mat gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            //Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);
            Mat edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);

            // 3) Contornos y polígono aproximado
            var contours = Cv2.FindContoursAsArray(edges,
                RetrievalModes.List, ContourApproximationModes.ApproxSimple)
                .OrderByDescending(c => Cv2.ContourArea(c))
                .ToList();

            Point2f[] quad = null;
            foreach (var c in contours)
            {
                var peri = Cv2.ArcLength(c, true);
                var approx = Cv2.ApproxPolyDP(c, 0.02 * peri, true);
                if (approx.Length == 4)
                {
                    quad = approx.Select(p => new Point2f(p.X, p.Y)).ToArray();
                    break;
                }
            }
            if (quad == null)
                throw new Exception("No se detectó un contorno de 4 vértices.");

            // 4) Ordenar vértices en TL, TR, BR, BL
            srcCorners = OrderCorners(quad);

            // 5) Calcular ancho/alto destino
            float widthA = Distance(srcCorners[2], srcCorners[3]);
            float widthB = Distance(srcCorners[1], srcCorners[0]);
            float maxW = Math.Max(widthA, widthB);

            float heightA = Distance(srcCorners[1], srcCorners[2]);
            float heightB = Distance(srcCorners[0], srcCorners[3]);
            float maxH = Math.Max(heightA, heightB);

            dstCorners = new[]
            {
                new Point2f(0, 0),
                new Point2f(maxW - 1, 0),
                new Point2f(maxW - 1, maxH - 1),
                new Point2f(0, maxH - 1)
            };

            // 6) Transformada de perspectiva
            Mat transform = Cv2.GetPerspectiveTransform(srcCorners, dstCorners);
            Mat warped = new Mat();
            //Cv2.WarpPerspective(src, warped, transform, new Size((int)maxW, (int)maxH));
            Cv2.WarpPerspective(src, warped, transform, new OpenCvSharp.Size((int)maxW, (int)maxH));

            // 7) Mat → Bitmap
            //return BitmapConverter.ToBitmap(warped);
            return BitmapConverter.ToBitmap(warped);
        }

        private static float Distance(Point2f a, Point2f b)
            => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        /// <summary>
        /// Ordena un array de 4 puntos como [TL, TR, BR, BL].
        /// </summary>
        private static Point2f[] OrderCorners(Point2f[] pts)
        {
            // TL = menor suma x+y, BR = mayor suma
            var sum = pts.Select(p => p.X + p.Y).ToArray();
            // TR = menor diff y−x, BL = mayor diff
            var diff = pts.Select(p => p.Y - p.X).ToArray();

            Point2f tl = pts[Array.IndexOf(sum, sum.Min())];
            Point2f br = pts[Array.IndexOf(sum, sum.Max())];
            Point2f tr = pts[Array.IndexOf(diff, diff.Min())];
            Point2f bl = pts[Array.IndexOf(diff, diff.Max())];

            return new[] { tl, tr, br, bl };
        }
    }
}
