using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ContrlAcademico.Services
{
    public class PdfRenderer
    {
        private readonly GhostscriptVersionInfo _gsVersion;
        private readonly int _dpi;

        public PdfRenderer(string ghostscriptDllPath, int dpi)
        {
            _gsVersion = new GhostscriptVersionInfo(ghostscriptDllPath);
            _dpi = dpi;
        }

        public List<Bitmap> RenderPages(string pdfPath)
        {
            var pages = new List<Bitmap>();
            using var rasterizer = new GhostscriptRasterizer();
            rasterizer.Open(pdfPath, _gsVersion, false);

            for (int pageNumber = 1; pageNumber <= rasterizer.PageCount; pageNumber++)
            {
                // Fix: Adjust the call to match the correct method signature  
                Bitmap img = (Bitmap)rasterizer.GetPage(_dpi, pageNumber);
                pages.Add(new Bitmap(img)); // clonar para evitar disposals  
            }

            return pages;
        }
    }
}
