using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace ContrlAcademico.Services
{
    public class DniReader : IDisposable
    {
        private readonly OpenCvSharp.Rect _roi;
        private readonly TesseractEngine _tess;

        
        public DniReader(DniRegionModel region, string tessdataPath)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (string.IsNullOrWhiteSpace(tessdataPath)) throw new ArgumentException("Ruta tessdata inválida.", nameof(tessdataPath));

            _roi = new OpenCvSharp.Rect(region.X, region.Y, region.W, region.H);
            _tess = new TesseractEngine(tessdataPath, "eng", EngineMode.LstmOnly);
            _tess.SetVariable("tessedit_char_whitelist", "0123456789");
            _tess.DefaultPageSegMode = PageSegMode.SingleLine;
        }

        
        public string ReadDni(Bitmap warped)
        {
            if (warped == null) throw new ArgumentNullException(nameof(warped));

            // 1) Convertir a Mat y recortar ROI  
            using var mat = BitmapConverter.ToMat(warped);
            var digiMat = new Mat(mat, _roi);

            // 2) Preprocesamiento: gris + binarización + dilate  
            Cv2.CvtColor(digiMat, digiMat, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(
                digiMat, digiMat,
                0, 255,
                ThresholdTypes.BinaryInv | ThresholdTypes.Otsu
            );
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Dilate(digiMat, digiMat, kernel);

            // 3) Convertir a Pix para Tesseract  
            using Bitmap bmp = BitmapConverter.ToBitmap(digiMat);
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                bytes = ms.ToArray();
            }
            using Pix pix = Pix.LoadFromMemory(bytes);

            // 4) OCR con Tesseract  
            using Page page = _tess.Process(pix);
            string text = page.GetText()?.Trim() ?? string.Empty;

            // 5) Filtrar solo dígitos  
            return new string(text.Where(char.IsDigit).ToArray());
        }

        /// <summary>Libera los recursos de Tesseract.</summary>  
        public void Dispose()
        {
            _tess?.Dispose();
        }
    }
    
    public class DniRegionModel
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

}
