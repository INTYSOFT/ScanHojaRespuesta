using System;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ContrlAcademico.Services
{
    public class PerspectiveCorrector
    {
        /// <summary>
        /// Corrige perspectiva sólo si detecta un cuadrilátero que toque
        /// los cuatro bordes de la imagen (±5%). Si no, devuelve la fuente.
        /// </summary>
        public Bitmap Correct(Bitmap srcBmp)
        {
            var src = BitmapConverter.ToMat(srcBmp);
            int W = src.Width, H = src.Height;

            // 1) Gris + blur
            var gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

            // 2) Canny + dilate
            var edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
            Cv2.Dilate(edges, edges, kernel);

            // 3) Aproxima polígonos de 4 vértices
            var quads = edges
                .FindContoursAsArray(RetrievalModes.External, ContourApproximationModes.ApproxSimple)
                .Select(c =>
                {
                    var peri = Cv2.ArcLength(c, true);
                    return Cv2.ApproxPolyDP(c, 0.02 * peri, true);
                })
                .Where(p => p.Length == 4)
                .ToList();

            // 4) Filtrar sólo los que tocan los 4 bordes (±5%)
            float tolX = W * 0.05f, tolY = H * 0.05f;
            OpenCvSharp.Point[] pageQuad = null;
            foreach (var quad in quads)
            {
                var xs = quad.Select(p => p.X).ToArray();
                var ys = quad.Select(p => p.Y).ToArray();
                bool left = xs.Any(x => x <  tolX);
                bool right = xs.Any(x => x >  W - tolX);
                bool top = ys.Any(y => y <  tolY);
                bool bottom = ys.Any(y => y >  H - tolY);
                if (left && right && top && bottom)
                {
                    // guardamos el de mayor área
                    if (pageQuad == null ||
                        Math.Abs(Cv2.ContourArea(quad)) >
                        Math.Abs(Cv2.ContourArea(pageQuad)))
                    {
                        pageQuad = quad;
                    }
                }
            }

            // 5) Si no hallamos la hoja completa, devolvemos la original
            if (pageQuad == null)
                return srcBmp;

            // 6) Ordenamos las esquinas y calculamos destino
            var srcPts = SortCorners(
                pageQuad.Select(p => new Point2f(p.X, p.Y)).ToArray());
            float wA = Distance(srcPts[0], srcPts[1]);
            float wB = Distance(srcPts[2], srcPts[3]);
            float maxW = Math.Max(wA, wB);
            float hA = Distance(srcPts[1], srcPts[2]);
            float hB = Distance(srcPts[3], srcPts[0]);
            float maxH = Math.Max(hA, hB);

            var dstPts = new[]
            {
                new Point2f(0,    0),
                new Point2f(maxW-1, 0),
                new Point2f(maxW-1, maxH-1),
                new Point2f(0,    maxH-1)
            };

            // 7) Warp
            var M = Cv2.GetPerspectiveTransform(srcPts, dstPts);
            var warped = new Mat();
            Cv2.WarpPerspective(src, warped, M, new OpenCvSharp.Size((int)maxW, (int)maxH));

            return BitmapConverter.ToBitmap(warped);
        }

        static Point2f[] SortCorners(Point2f[] pts)
        {
            var sum = pts.Select(p => p.X + p.Y).ToArray();
            var diff = pts.Select(p => p.Y - p.X).ToArray();
            var tl = pts[Array.IndexOf(sum, sum.Min())];
            var br = pts[Array.IndexOf(sum, sum.Max())];
            var tr = pts[Array.IndexOf(diff, diff.Min())];
            var bl = pts[Array.IndexOf(diff, diff.Max())];
            return new[] { tl, tr, br, bl };
        }

        static float Distance(Point2f a, Point2f b) =>
            (float)Math.Sqrt((a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y));
    }
}
