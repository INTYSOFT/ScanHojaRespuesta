using ContrlAcademico.Services;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Text;
using System.Threading;

namespace ContrlAcademico
{
    public partial class Form1 : Form
    {
        // Para almacenar resultados por página
        private List<string> _pageDnis;
        private List<char[]> _pageAnswers;


        private ConfigModel _config;
        private OmrProcessor _omrProcessor;


        //DNI
        private readonly ConfigModel _cfg;
        private readonly PdfRenderer _renderer;
        //private readonly PerspectiveCorrector _corrector;
        private readonly DniExtractor _dniExt;
        private readonly RotationCorrector _corrector;




        // imágene s debug (warp con rects) y binarizadas
        private List<Bitmap> _debugPages;
        private List<Bitmap> _threshPages;
        private List<char[]> _answersPages;
        private int _currentPage = 0;

        private bool _calibrated = false;
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public Form1()
        {
            InitializeComponent();
            // suscribirte al evento MouseClick
            pbWarped.MouseClick += PbWarped_MouseClick;




            this.Resize += Form1_Resize;
            AjustarPbWarped();  // Para el tamaño inicial

            LoadConfig();

            //_renderer  = new PdfRenderer(gsPath, _config.Dpi);
            _corrector = new RotationCorrector();
            _dniExt    = new DniExtractor(_config);

            _dniExt    = new DniExtractor(_config);

            //DNI
            var cfgPath = _configPath;
            _cfg       = ConfigModel.Load(cfgPath);
            _renderer  = new PdfRenderer(@"C:\Program Files\gs\gs10.05.1\bin\gsdll64.dll", _cfg.Dpi);
            _corrector = new RotationCorrector();
            _dniExt    = new DniExtractor(_cfg);



            // 3) Monta los grids
            SetupHeadGrid();
            SetupDetailGrid();

            // 4) Eventos
            dgvHead.SelectionChanged += dgvHead_SelectionChanged;

            // añade esto:
            dgvHead.CellClick        += dgvHead_CellClick;

        }

        private void dgvHead_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Evitamos cabeceras y cols “virtuales”
            if (e.RowIndex < 0) return;

            // 1) Seleccionamos la fila entera
            dgvHead.ClearSelection();
            dgvHead.Rows[e.RowIndex].Selected = true;

            // 2) Navegamos la visualización a esa página
            DisplayPage(e.RowIndex);

            // 3) Refrescamos el detalle
            FillDetail(e.RowIndex);
        }

        private void FillDetail(int pageIndex)
        {
            dgvDetalle.Rows.Clear();
            var answers = _pageAnswers[pageIndex];
            for (int q = 0; q < answers.Length; q++)
            {
                dgvDetalle.Rows.Add((q + 1).ToString(), answers[q].ToString());
            }
        }


        private void CalibrateGrid(Mat gray)
        {
            var g = _config.AnswersGrid;
            int rows = g.Rows;
            int approxX = g.StartX + g.BubbleW/2;       // centro aproximado 1ª columna
            int minR = g.BubbleH/2 - 5, maxR = g.BubbleH/2 + 5;

            // 1) Detectar círculos
            CircleSegment[] circles = Cv2.HoughCircles(
                gray,
                HoughModes.Gradient, // HoughCircles con gradiente                
                dp: 1,
                minDist: g.Dy * 0.8,
                param1: 100,
                param2: 20,
                minRadius: minR,
                maxRadius: maxR
            );

            // 2) Filtrar por X cerca de la 1ª columna
            var col1 = circles
                .Where(c => Math.Abs(c.Center.X - approxX) < g.BubbleW)
                .OrderBy(c => c.Center.Y)
                .Take(rows)
                .ToArray();

            if (col1.Length < rows)
                return; // no calibramos si faltan detecciones

            // 3) Extraer Ys y recomputar StartY y Dy
            var ys = col1.Select(c => c.Center.Y).ToArray();
            float newStartY = ys[0] - g.BubbleH/2f;
            float sumDy = 0;
            for (int i = 1; i < ys.Length; i++)
                sumDy += (ys[i] - ys[i-1]);
            float newDy = sumDy / (rows - 1);

            // 4) Aplicar valores enteros
            g.StartY = (int)Math.Round(newStartY);
            g.Dy     = (int)Math.Round(newDy);
            _calibrated = true;

            //logs.AppendText($"Calibrado: StartY={g.StartY}, Dy={g.Dy}\n");
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
        //evento resize de form1



        private void btnSelectPdf_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "PDF Files|*.pdf" };
            if (ofd.ShowDialog() == DialogResult.OK)
                txtPdfPath.Text = ofd.FileName;
        }

        private void LoadConfig()
        {
            if (!File.Exists(_configPath))
            {
                MessageBox.Show(
                    $"No se encontró el archivo de configuración en:\n{_configPath}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            _config = JsonConvert.DeserializeObject<ConfigModel>(
                File.ReadAllText(_configPath))
                ?? throw new InvalidOperationException("Error al deserializar ConfigModel.");

            _omrProcessor = new OmrProcessor(
                _config.AnswersGrid,
                _config.DniRegion,
                fillThreshold: 0.5,
                meanThreshold: 180,
                deltaMin: 30
            );
        }

        private void SetupHeadGrid()
        {
            dgvHead.Columns.Clear();
            dgvHead.Columns.Add("colPage", "Página");
            dgvHead.Columns.Add("colDNI", "DNI");
            dgvHead.Columns.Add("colName", "Nombre");
            dgvHead.Columns.Add("colFecha", "Fecha");
            dgvHead.Columns.Add("colGrado", "Grado");
            dgvHead.Columns.Add("colSeccion", "Sección");

            dgvHead.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvHead.AllowUserToAddRows    = false;
            dgvHead.AllowUserToDeleteRows = false;
        }

        private void SetupDetailGrid()
        {
            dgvDetalle.Columns.Clear();
            dgvDetalle.Columns.Add("colPregunta", "Pregunta");
            dgvDetalle.Columns.Add("colRespuesta", "Respuesta");

            dgvDetalle.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvDetalle.AllowUserToAddRows    = false;
            dgvDetalle.AllowUserToDeleteRows = false;
        }

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            // Ruta estática al archivo de configuración
            string configPath = _configPath;

            if (File.Exists(configPath))
            {

                _config = JsonConvert.DeserializeObject<ConfigModel>(File.ReadAllText(configPath));

                //_omrProcessor = new OmrProcessor(_config.AnswersGrid, fillThreshold: 0.2);
                _omrProcessor = new OmrProcessor(
                    _config.AnswersGrid,
                    _config.DniRegion,    // ← aquí
                       fillThreshold: 0.5,   // o el umbral de relleno que quieras
                    meanThreshold: 180,   // Ajusta según tus necesidades
                    deltaMin: 30          // Ajusta según tus necesidades
                );

                //logs.AppendText("Configuración cargada y OMR listo.\n");
            }
            else
            {
                MessageBox.Show($"No se encontró el archivo de configuración en:\n{configPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        private async void btnStart_Click(object sender, EventArgs e)
        {
            // 1.a) Validar PDF
            dgvHead.Rows.Clear();
            dgvDetalle.Rows.Clear();
            _pageDnis    = new List<string>();
            _pageAnswers = new List<char[]>();

            btnStart.Enabled = false;
            //logs.Clear();

            _debugPages   = new List<Bitmap>();
            _threshPages  = new List<Bitmap>();
            _answersPages = new List<char[]>();


            try
            {
                var corrector = new PerspectiveCorrector();
                var renderer = new PdfRenderer(@"C:\Program Files\gs\gs10.05.1\bin\gsdll64.dll", _config.Dpi);

                var pages = renderer.RenderPages(txtPdfPath.Text);
                progressBar.Maximum = pages.Count;

                _pageDnis     = new List<string>(pages.Count);
                _pageAnswers  = new List<char[]>(pages.Count);

                for (int i = 0; i < pages.Count; i++)
                {

                    string dni = string.Empty;

                    dni=ReadDniFromPage(pages[i]);

                    //Corrijo perspectiva
                    Bitmap warped = corrector.Correct(pages[i], out _, out _);

                    //Muestro debug de todo el warped
                    using var warpedMat = BitmapConverter.ToMat(warped);

                    //logs.AppendText($"DEBUG: warped size = {warpedMat.Width}×{warpedMat.Height}\n");

                    Mat debugMat = warpedMat.Clone();
                    pbWarped.Image?.Dispose();
                    pbWarped.Image = BitmapConverter.ToBitmap(debugMat);
                    Application.DoEvents();
                    await Task.Delay(200);

                    // 3) DEBUG: dibujar rectángulos normalizados
                    /*var g = _config.AnswersGrid;
                    for (int b = 0; b < g.BlockCount; b++)
                    {
                        // desplazamiento X del bloque b
                        int blockX = g.StartX + b * (g.Cols * g.Dx + g.BlockSpacing);

                        for (int row = 0; row < g.Rows; row++)
                        {
                            int y = g.StartY + row * g.Dy;
                            for (int col = 0; col < g.Cols; col++)
                            {
                                int x = blockX + col * g.Dx;
                                Cv2.Rectangle(
                                    debugMat,
                                    new Rect(x, y, g.BubbleW, g.BubbleH),
                                    Scalar.Red,
                                    1
                                );
                            }
                        }
                    }*/

                    //Mostrar binarizado
                    Mat gray = new Mat();
                    Cv2.CvtColor(warpedMat, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

                    if (!_calibrated) CalibrateGrid(gray);

                    /*Mat thresh = new Mat();
                    Cv2.AdaptiveThreshold(
                        gray,
                        thresh,
                        255,
                        AdaptiveThresholdTypes.GaussianC,
                        ThresholdTypes.BinaryInv,
                        11,
                        1            // puedes también experimentar con este valor
                    );

                    pbThresh.Image?.Dispose();
                    pbThresh.Image = BitmapConverter.ToBitmap(thresh);*/


                    //DNI
                    //Bitmap fullPageBmp = pages[i];
                    //string dni = ReadDni(fullPageBmp);
                    //logs.AppendText($"Página {i+1}: DNI → {dni}\n");


                    var answers = _omrProcessor.Process(warped);

                    _pageDnis.Add(dni);
                    _pageAnswers.Add(answers);

                    //dgvHead
                    dgvHead.Rows.Add(
                        (i + 1).ToString("000"), // Página 001,002...
                        dni,                     // El DNI leído
                        "",                      // Nombre (aún no lo tenemos)
                        DateTime.Now.ToShortDateString(), // Fecha actual
                        "",                      // Grado (aún no lo tenemos)
                        ""                       // Sección (aún no lo tenemos)
                    );

                    //agregar logs.AppendText pregunta y respuesta
                    //for (int j = 0; j < answers.Length; j++)
                    //{
                    //log.AppendText($"Página {i+1} DNI {dni} : Pregunta {j + 1}: Respuesta {answers[j]}\n");

                    //}

                    progressBar.Value = i + 1;
                    Application.DoEvents();
                    await Task.Delay(50);

                }

                // Al terminar todo:
                _currentPage = 0;
                DisplayPage(_currentPage);
                btnPrev.Enabled = false;
                btnNext.Enabled = pages.Count > 1;

            }
            catch (Exception ex)
            {
                //logs.AppendText($"ERROR: {ex.Message}\n");
            }
            finally
            {
                if (dgvHead.Rows.Count > 0)
                {
                    dgvHead.ClearSelection();
                    dgvHead.Rows[0].Selected = true;
                }

                btnStart.Enabled = true;
            }
        }


        private void dgvHead_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvHead.SelectedRows.Count == 0) return;
            int idx = dgvHead.SelectedRows[0].Index;

            // ▷ Guard clause: si el índice no existe, salimos sin tocar nada
            if (_pageAnswers == null|| idx < 0|| idx >= _pageAnswers.Count)
                return;


            //DisplayPage(idx);
            FillDetail(idx);

            // Refrescar pagina visual
            _currentPage = idx;

            // Rellenar detalle
            dgvDetalle.Rows.Clear();
            var answers = _pageAnswers[idx];
            for (int q = 0; q < answers.Length; q++)
            {
                dgvDetalle.Rows.Add((q+1).ToString(), answers[q].ToString());
            }

            DisplayPage(idx);
        }


        private void DisplayPage(int index)
        {
            // debug (warped con rects)
            pbWarped.Image?.Dispose();
            // Clonamos para no disolver el original de la lista
            pbWarped.Image = (Bitmap)_debugPages[index].Clone();

            // thresh binarizado
            pbThresh.Image?.Dispose();
            pbThresh.Image = (Bitmap)_threshPages[index].Clone();

            // info de página
            lblPageInfo.Text = $"{index+1} / {_debugPages.Count}";

            // Actualizar el texto del textbox de página
            tbPage.Text = (index + 1).ToString(); // Convertir a 1-based


            // Logs: limpia y muestra solo esa página
            //logs.Clear();
            //var ans = _answersPages[index];
            //for (int j = 0; j < ans.Length; j++)                logs.AppendText($"P{index+1} Q{j+1}: {ans[j]}\n");
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                DisplayPage(_currentPage);
                btnNext.Enabled = true;
                btnPrev.Enabled = _currentPage > 0;
                //Actulizar tbPage
                tbPage.Text = (_currentPage + 1).ToString(); // Convertir a 1-based



                dgvHead.ClearSelection();
                dgvHead.Rows[_currentPage].Selected = true;
                dgvHead.FirstDisplayedScrollingRowIndex = _currentPage; // Desplazar al inicio de la página actual

            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (_currentPage < _debugPages.Count - 1)
            {
                _currentPage++;
                DisplayPage(_currentPage);
                btnPrev.Enabled = true;
                btnNext.Enabled = _currentPage < _debugPages.Count - 1;
                //Actualizar tbPage
                tbPage.Text = (_currentPage + 1).ToString(); // Convertir a 1-based

                dgvHead.ClearSelection();
                dgvHead.Rows[_currentPage].Selected = true;
                dgvHead.FirstDisplayedScrollingRowIndex = _currentPage; // Desplazar al inicio de la página actual



            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            //logs.Clear();

            var pages = _renderer.RenderPages(txtPdfPath.Text);
            progressBar.Maximum = pages.Count;

            for (int i = 0; i < pages.Count; i++)
            {
                // 1) Alineamos la página completa
                var fullPage = _corrector.Correct(pages[i]);

                // 1.a) Mostramos la página entera
                pbWarped.Image?.Dispose();
                pbWarped.Image = (Bitmap)fullPage.Clone();

                // 2) Recortamos la región del DNI
                int W = fullPage.Width, H = fullPage.Height;

                var rn = _cfg.DniRegionNorm;
                var roiRect = new Rectangle(
                    (int)(rn.X*W), (int)(rn.Y*H),
                    (int)(rn.W*W), (int)(rn.H*H)
                );

                using var roiBmp = fullPage.Clone(roiRect, fullPage.PixelFormat);

                // 2.a) Mostramos la región del DNI
                pbThresh.Image?.Dispose();
                pbThresh.Image = (Bitmap)roiBmp.Clone();

                // 3) Extraemos el DNI
                string dni = _dniExt.Extract(fullPage);
                //logs.AppendText($"Página {i+1:000} → DNI: {dni}\r\n");

                // 4) Avanzamos la barra y dejamos que Windows repinte
                progressBar.Value = i + 1;
                Application.DoEvents();
                await Task.Delay(200);
            }

            MessageBox.Show("Lectura de DNI completada.", "OK",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnStart.Enabled = true;
        }


        private string ReadDniFromPage(Bitmap fullPage)
        {
            // 1) Dibujar el rectángulo de la región del DNI sobre la página
            int W = fullPage.Width, H = fullPage.Height;
            var rn = _cfg.DniRegionNorm;
            var roiRect = new Rect(
                (int)(rn.X * W), (int)(rn.Y * H),
                (int)(rn.W * W), (int)(rn.H * H)
            );

            // debugMat: página completa + rect
            using var debugMat = BitmapConverter.ToMat(fullPage).Clone();
            Cv2.Rectangle(debugMat, roiRect, Scalar.Red, 2);
            _debugPages.Add(BitmapConverter.ToBitmap(debugMat));

            // 2) Binarizar la página completa (como hacías antes)
            using var gray = new Mat();
            Cv2.CvtColor(debugMat, gray, ColorConversionCodes.BGR2GRAY);
            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(
                gray, thresh, 255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                11, 2
            );
            _threshPages.Add(BitmapConverter.ToBitmap(thresh));

            // 3) Procesar OMR de respuestas sobre la página completa
            //    (si en realidad lo querías sobre el warped, pásale fullPage)
            var answers = _omrProcessor.Process(fullPage);
            _answersPages.Add(answers);

            // 4) Extraer el DNI con tu extractor (que aplica OMR 8×10 sobre la región)
            string dni = _dniExt.Extract(fullPage);
            return dni;
        }


        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            //Envair a la página que se escriba en el textbox
            if (int.TryParse(tbPage.Text, out int pageNum) &&
                pageNum > 0 && pageNum <= _debugPages.Count)
            {
                _currentPage = pageNum - 1; // Convertir a índice 0
                DisplayPage(_currentPage);
                btnPrev.Enabled = _currentPage > 0;
                btnNext.Enabled = _currentPage < _debugPages.Count - 1;
                dgvHead.ClearSelection();
                dgvHead.Rows[_currentPage].Selected = true;
                dgvHead.FirstDisplayedScrollingRowIndex = _currentPage; // Desplazar al inicio de la página actual

            }
            else
            {
                //MessageBox.Show("Número de página inválido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            // Actualizar el texto del label de info de página
            lblPageInfo.Text = $"{_currentPage + 1} / {_debugPages.Count}";
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            AjustarPbWarped();
        }

        private void AjustarPbWarped()
        {
            // Ubicación actual de pbWarped
            int x = pbWarped.Location.X;
            int y = pbWarped.Location.Y;

            // Margen opcional (en píxeles) si no quieres que toque el borde
            int margenDerecho = 5;
            int margenInferior = 5;

            // Nuevo ancho = ancho del cliente del form - x - margenDerecho
            pbWarped.Width  = Math.Max(0, this.ClientSize.Width - x - margenDerecho);
            // Nuevo alto  = alto del cliente del form  - y - margenInferior
            pbWarped.Height = Math.Max(0, this.ClientSize.Height - y - margenInferior);
        }

        private void dgvHead_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void pbWarped_Click(object sender, EventArgs e)
        {
            pbWarped.Visible = true;

        }

        private void PbWarped_MouseClick(object sender, MouseEventArgs e)
        {
            // Si quieres las coordenadas en píxeles del PictureBox:
            int x = e.X;
            int y = e.Y;

            // Si tu PictureBox está en SizeMode = PictureBoxSizeMode.Zoom
            // y quieres convertirlas a coordenadas de la imagen real, 
            // usa este mapeo (opcional):
            if (pbWarped.Image != null && pbWarped.SizeMode == PictureBoxSizeMode.Zoom)
            {
                var img = pbWarped.Image;
                float imgRatio = (float)img.Width / img.Height;
                float boxRatio = (float)pbWarped.Width / pbWarped.Height;
                float scale;
                int offsetX = 0, offsetY = 0;

                if (imgRatio > boxRatio)
                {
                    // la imagen ocupa todo el ancho del control
                    scale = (float)pbWarped.Width / img.Width;
                    offsetY = (int)((pbWarped.Height - img.Height * scale) / 2);
                }
                else
                {
                    // la imagen ocupa todo el alto del control
                    scale = (float)pbWarped.Height / img.Height;
                    offsetX = (int)((pbWarped.Width - img.Width * scale) / 2);
                }

                // ajustar el punto al espacio ocupado por la imagen
                x = (int)((e.X - offsetX) / scale);
                y = (int)((e.Y - offsetY) / scale);
            }

            // ahora vuelcaremos el resultado en lblPageInfo
            lblPageInfo.Text = $"Click en X={x}, Y={y}";
        }

    }
}
