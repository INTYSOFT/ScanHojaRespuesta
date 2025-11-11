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
            using var src = BitmapConverter.ToMat(input);
            int width = src.Width;
            int height = src.Height;

            using var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

            using var edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            Cv2.Dilate(edges, edges, kernel);

            var contours = Cv2.FindContoursAsArray(edges,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            OpenCvSharp.Point[]? bestQuad = null;
            double bestArea = 0;

            float tolX = Math.Max(5f, width * 0.05f);
            float tolY = Math.Max(5f, height * 0.05f);

            foreach (var contour in contours)
            {
                var perimeter = Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, 0.02 * perimeter, true);
                if (approx.Length != 4)
                {
                    continue;
                }

                var xs = approx.Select(p => p.X).ToArray();
                var ys = approx.Select(p => p.Y).ToArray();

                bool touchesLeft = xs.Any(x => x <= tolX);
                bool touchesRight = xs.Any(x => x >= width - tolX);
                bool touchesTop = ys.Any(y => y <= tolY);
                bool touchesBottom = ys.Any(y => y >= height - tolY);

                if (!(touchesLeft && touchesRight && touchesTop && touchesBottom))
                {
                    continue;
                }

                double area = Math.Abs(Cv2.ContourArea(approx));
                if (area > bestArea)
                {
                    bestArea = area;
                    bestQuad = approx;
                }
            }

            if (bestQuad == null)
            {
                foreach (var contour in contours.OrderByDescending(c => Math.Abs(Cv2.ContourArea(c))))
                {
                    var perimeter = Cv2.ArcLength(contour, true);
                    var approx = Cv2.ApproxPolyDP(contour, 0.02 * perimeter, true);
                    if (approx.Length != 4)
                    {
                        continue;
                    }

                    double area = Math.Abs(Cv2.ContourArea(approx));
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestQuad = approx;
                    }
                }
            }

            if (bestQuad == null)
            {
                throw new Exception("No se detectó un contorno de 4 vértices.");
            }

            var quad = bestQuad.Select(p => new Point2f(p.X, p.Y)).ToArray();
            srcCorners = OrderCorners(quad);

            float widthA = Distance(srcCorners[1], srcCorners[0]);
            float widthB = Distance(srcCorners[2], srcCorners[3]);
            float maxW = Math.Max(widthA, widthB);

            float heightA = Distance(srcCorners[1], srcCorners[2]);
            float heightB = Distance(srcCorners[0], srcCorners[3]);
            float maxH = Math.Max(heightA, heightB);

            int destW = Math.Max(1, (int)Math.Round(maxW));
            int destH = Math.Max(1, (int)Math.Round(maxH));

            dstCorners = new[]
            {
                new Point2f(0, 0),
                new Point2f(destW - 1, 0),
                new Point2f(destW - 1, destH - 1),
                new Point2f(0, destH - 1)
            };

            using var transform = Cv2.GetPerspectiveTransform(srcCorners, dstCorners);
            using var warped = new Mat();
            Cv2.WarpPerspective(src, warped, transform, new OpenCvSharp.Size(destW, destH));

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
