using ContrlAcademico.Services;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Net.Http;
using System.Linq;

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

        private readonly string? _authToken;
        private readonly EvaluacionProgramadaService _evaluacionService = new();
        private List<EvaluacionProgramadaSummaryDto> _evaluacionesProgramadas = new();
        private List<EvaluacionProgramadaConsultaDto> _evaluacionParticipantes = new();
        private readonly Dictionary<string, int> _dniToScanIndex = new(StringComparer.OrdinalIgnoreCase);
        private bool _suppressPageTextChange;
        private bool _isPopulatingSecciones;

        private sealed class SectionItem
        {
            public SectionItem(string display, string? value)
            {
                Display = display;
                Value = value;
            }

            public string Display { get; }
            public string? Value { get; }

            public override string ToString() => Display;
        }

        public Form1(string? authToken = null)
        {
            _authToken = authToken;
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

            MostrarDetalleDesdeFila(e.RowIndex);
        }

        private void FillDetailFromScanIndex(int scanIndex)
        {
            dgvDetalle.Rows.Clear();
            if (scanIndex < 0 || scanIndex >= _pageAnswers.Count)
            {
                return;
            }

            var answers = _pageAnswers[scanIndex];
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

        private async void Form1_Load(object sender, EventArgs e)
        {
            await LoadEvaluacionesProgramadasAsync();
        }

        private async Task LoadEvaluacionesProgramadasAsync()
        {
            try
            {
                cmbEvaluaciones.Enabled = false;
                ResetSecciones();

                if (string.IsNullOrWhiteSpace(_authToken))
                {
                    MessageBox.Show(
                        "No se encontró el token de autenticación. Inicie sesión nuevamente.",
                        "Autenticación requerida",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var evaluaciones = await _evaluacionService
                    .ObtenerPorEstadoAsync(_config.ApiEndpoint, 2, _authToken)
                    .ConfigureAwait(true);

                _evaluacionesProgramadas = evaluaciones.ToList();

                if (_evaluacionesProgramadas.Count == 0)
                {
                    cmbEvaluaciones.DataSource = null;
                    lblEvaluacion.Text = "No existen evaluaciones programadas";
                    _evaluacionParticipantes.Clear();
                    dgvHead.Rows.Clear();
                    ResetSecciones();
                    return;
                }

                cmbEvaluaciones.DataSource = _evaluacionesProgramadas;
                cmbEvaluaciones.DisplayMember = nameof(EvaluacionProgramadaSummaryDto.DisplayText);
                cmbEvaluaciones.ValueMember = nameof(EvaluacionProgramadaSummaryDto.Id);
                cmbEvaluaciones.Enabled = true;
                lblEvaluacion.Text = "Evaluación programada";
            }
            catch (HttpRequestException ex)
            {
                ResetSecciones();
                MessageBox.Show(
                    $"Error de conexión al cargar las evaluaciones programadas: {ex.Message}",
                    "Error de red",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                ResetSecciones();
                MessageBox.Show(
                    $"Error al cargar las evaluaciones programadas: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task LoadEvaluacionDetalleAsync(int evaluacionProgramadaId)
        {
            try
            {
                ResetSecciones();
                var detalle = await _evaluacionService
                    .ObtenerDetalleAsync(_config.ApiEndpoint, evaluacionProgramadaId, _authToken ?? string.Empty)
                    .ConfigureAwait(true);

                _evaluacionParticipantes = detalle.ToList();
                ClearScanResults();
                PopulateSecciones(_evaluacionParticipantes);
            }
            catch (HttpRequestException ex)
            {
                ResetSecciones();
                MessageBox.Show(
                    $"Error de conexión al consultar la evaluación seleccionada: {ex.Message}",
                    "Error de red",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                ResetSecciones();
                MessageBox.Show(
                    $"Error al consultar la evaluación seleccionada: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void PopulateHeadGrid(IEnumerable<EvaluacionProgramadaConsultaDto> participantes)
        {
            dgvHead.Rows.Clear();

            foreach (var participante in participantes)
            {
                var nombreCompleto = string.Join(" ", new[]
                {
                    participante.AlumnoApellidos?.Trim() ?? string.Empty,
                    participante.AlumnoNombres?.Trim() ?? string.Empty
                }).Trim();

                int index = dgvHead.Rows.Add(
                    string.Empty,
                    participante.AlumnoDni ?? string.Empty,
                    nombreCompleto,
                    string.Empty,
                    participante.Ciclo ?? string.Empty,
                    participante.Seccion ?? string.Empty);

                dgvHead.Rows[index].Tag = null;
            }

            dgvHead.ClearSelection();
        }

        private void PopulateSecciones(IEnumerable<EvaluacionProgramadaConsultaDto> participantes)
        {
            _isPopulatingSecciones = true;
            try
            {
                cmbSecciones.DataSource = null;
                cmbSecciones.Items.Clear();

                var sections = participantes
                    .Select(p => NormalizeSection(p.Seccion))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(section => new SectionItem(section ?? "Sin sección", section))
                    .OrderBy(item => item.Display, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (sections.Count == 0)
                {
                    cmbSecciones.Enabled = false;
                    cmbSecciones.Items.Add("Sin secciones disponibles");
                    cmbSecciones.SelectedIndex = 0;
                    dgvHead.Rows.Clear();
                    return;
                }

                cmbSecciones.Enabled = true;
                cmbSecciones.DataSource = sections;
                cmbSecciones.DisplayMember = nameof(SectionItem.Display);
                cmbSecciones.ValueMember = nameof(SectionItem.Value);
            }
            finally
            {
                _isPopulatingSecciones = false;
            }

            ApplySectionFilter();
        }

        private void ResetSecciones()
        {
            _isPopulatingSecciones = true;
            try
            {
                cmbSecciones.DataSource = null;
                cmbSecciones.Items.Clear();
                cmbSecciones.Enabled = false;
            }
            finally
            {
                _isPopulatingSecciones = false;
            }
        }

        private void ApplySectionFilter()
        {
            if (_isPopulatingSecciones)
            {
                return;
            }

            if (cmbSecciones.SelectedItem is not SectionItem selectedSection)
            {
                dgvHead.Rows.Clear();
                return;
            }

            var filtered = _evaluacionParticipantes
                .Where(participante => SectionMatches(participante, selectedSection))
                .ToList();

            PopulateHeadGrid(filtered);
        }

        private static string? NormalizeSection(string? seccion)
            => string.IsNullOrWhiteSpace(seccion) ? null : seccion.Trim();

        private static bool SectionMatches(EvaluacionProgramadaConsultaDto participante, SectionItem selectedSection)
        {
            var participanteSeccion = NormalizeSection(participante.Seccion);

            if (selectedSection.Value is null)
            {
                return participanteSeccion is null;
            }

            return string.Equals(participanteSeccion, selectedSection.Value, StringComparison.OrdinalIgnoreCase);
        }

        private void ClearScanResults()
        {
            _pageDnis = new List<string>();
            _pageAnswers = new List<char[]>();
            _debugPages = new List<Bitmap>();
            _threshPages = new List<Bitmap>();
            _answersPages = new List<char[]>();
            _dniToScanIndex.Clear();
            _currentPage = 0;

            dgvDetalle.Rows.Clear();
            lblPageInfo.Text = string.Empty;
            pbWarped.Image?.Dispose();
            pbWarped.Image = null;
            pbThresh.Image?.Dispose();
            pbThresh.Image = null;
            btnPrev.Enabled = false;
            btnNext.Enabled = false;
            progressBar.Value = 0;
            progressBar.Maximum = 0;

            foreach (DataGridViewRow row in dgvHead.Rows)
            {
                row.Cells["colPage"].Value = string.Empty;
                row.Cells["colFecha"].Value = string.Empty;
                row.Tag = null;
            }
        }

        private int FindRowIndexByDni(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                return -1;
            }

            for (int i = 0; i < dgvHead.Rows.Count; i++)
            {
                if (dgvHead.Rows[i].Cells["colDNI"].Value is string cellValue &&
                    string.Equals(cellValue.Trim(), dni.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private async void cmbEvaluaciones_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbEvaluaciones.SelectedItem is not EvaluacionProgramadaSummaryDto evaluacion)
            {
                _evaluacionParticipantes.Clear();
                dgvHead.Rows.Clear();
                ResetSecciones();
                return;
            }

            await LoadEvaluacionDetalleAsync(evaluacion.Id);
        }

        private void cmbSecciones_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplySectionFilter();
        }

        private void UpdateRowWithScanData(string dni, int scanIndex, DateTime fechaProcesamiento)
        {
            dni ??= string.Empty;
            dni = dni.Trim();

            int rowIndex = FindRowIndexByDni(dni);

            if (rowIndex >= 0)
            {
                var row = dgvHead.Rows[rowIndex];
                row.Cells["colPage"].Value = (scanIndex + 1).ToString("000");
                row.Cells["colFecha"].Value = fechaProcesamiento.ToShortDateString();
                row.Cells["colDNI"].Value = dni;
                row.Tag = scanIndex;
            }
            else
            {
                rowIndex = dgvHead.Rows.Add(
                    (scanIndex + 1).ToString("000"),
                    dni,
                    string.Empty,
                    fechaProcesamiento.ToShortDateString(),
                    string.Empty,
                    string.Empty);

                dgvHead.Rows[rowIndex].Tag = scanIndex;
            }

            if (!string.IsNullOrWhiteSpace(dni))
            {
                _dniToScanIndex[dni] = scanIndex;
            }
        }

        private void SelectRowByDni(string dni)
        {
            int rowIndex = FindRowIndexByDni(dni);
            if (rowIndex < 0)
            {
                return;
            }

            dgvHead.ClearSelection();
            dgvHead.Rows[rowIndex].Selected = true;
            dgvHead.FirstDisplayedScrollingRowIndex = Math.Max(0, rowIndex);
        }

        private void ShowScanResult(int scanIndex)
        {
            if (scanIndex < 0 || scanIndex >= _pageAnswers.Count)
            {
                return;
            }

            FillDetailFromScanIndex(scanIndex);
            DisplayPage(scanIndex);
            _currentPage = scanIndex;

            btnPrev.Enabled = _currentPage > 0;
            btnNext.Enabled = _currentPage < _pageAnswers.Count - 1;
        }

        private void MostrarDetalleDesdeFila(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvHead.Rows.Count)
            {
                return;
            }

            var row = dgvHead.Rows[rowIndex];

            int scanIndex = -1;
            if (row.Tag is int tagIndex)
            {
                scanIndex = tagIndex;
            }
            else if (row.Cells["colDNI"].Value is string dni && _dniToScanIndex.TryGetValue(dni, out var mappedIndex))
            {
                scanIndex = mappedIndex;
            }

            if (scanIndex >= 0)
            {
                ShowScanResult(scanIndex);
            }
            else
            {
                dgvDetalle.Rows.Clear();
                lblPageInfo.Text = "Sin resultados de escaneo";
            }
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
            if (cmbEvaluaciones.SelectedItem is not EvaluacionProgramadaSummaryDto)
            {
                MessageBox.Show(
                    "Seleccione una evaluación programada antes de iniciar el escaneo.",
                    "Evaluación requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPdfPath.Text) || !File.Exists(txtPdfPath.Text))
            {
                MessageBox.Show(
                    "Seleccione un archivo PDF válido antes de iniciar.",
                    "Archivo requerido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ClearScanResults();
            btnStart.Enabled = false;

            try
            {
                var corrector = new PerspectiveCorrector();
                var renderer = new PdfRenderer(@"C:\Program Files\gs\gs10.05.1\bin\gsdll64.dll", _config.Dpi);

                var pages = renderer.RenderPages(txtPdfPath.Text);
                if (pages.Count == 0)
                {
                    MessageBox.Show(
                        "El documento seleccionado no contiene páginas.",
                        "Documento vacío",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                progressBar.Value = 0;
                progressBar.Maximum = pages.Count;

                _pageDnis = new List<string>(pages.Count);
                _pageAnswers = new List<char[]>(pages.Count);

                for (int i = 0; i < pages.Count; i++)
                {
                    string dni = ReadDniFromPage(pages[i]);

                    Bitmap warped = corrector.Correct(pages[i], out _, out _);

                    using var warpedMat = BitmapConverter.ToMat(warped);

                    Mat debugMat = warpedMat.Clone();
                    pbWarped.Image?.Dispose();
                    pbWarped.Image = BitmapConverter.ToBitmap(debugMat);
                    Application.DoEvents();
                    await Task.Delay(200);

                    using Mat gray = new Mat();
                    Cv2.CvtColor(warpedMat, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

                    if (!_calibrated)
                    {
                        CalibrateGrid(gray);
                    }

                    var answers = _omrProcessor.Process(warped);
                    warped.Dispose();

                    _pageDnis.Add(dni);
                    _pageAnswers.Add(answers);

                    UpdateRowWithScanData(dni, i, DateTime.Now);

                    progressBar.Value = i + 1;
                    Application.DoEvents();
                    await Task.Delay(50);
                }

                if (_pageDnis.Count > 0)
                {
                    ShowScanResult(0);
                    SelectRowByDni(_pageDnis[0]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al procesar el documento: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnStart.Enabled = true;
            }
        }


        private void dgvHead_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvHead.SelectedRows.Count == 0) return;
            int idx = dgvHead.SelectedRows[0].Index;
            MostrarDetalleDesdeFila(idx);
        }


        private void DisplayPage(int index)
        {
            if (_debugPages == null || _threshPages == null)
                return;

            if (index < 0 || index >= _debugPages.Count || index >= _threshPages.Count)
                return;

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
            _suppressPageTextChange = true;
            tbPage.Text = (index + 1).ToString(); // Convertir a 1-based
            _suppressPageTextChange = false;


            // Logs: limpia y muestra solo esa página
            //logs.Clear();
            //var ans = _answersPages[index];
            //for (int j = 0; j < ans.Length; j++)                logs.AppendText($"P{index+1} Q{j+1}: {ans[j]}\n");
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {
            if (_pageAnswers.Count == 0 || _currentPage <= 0)
            {
                return;
            }

            _currentPage--;
            ShowScanResult(_currentPage);

            tbPage.Text = (_currentPage + 1).ToString();

            if (_currentPage >= 0 && _currentPage < _pageDnis.Count)
            {
                SelectRowByDni(_pageDnis[_currentPage]);
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (_pageAnswers.Count == 0 || _currentPage >= _pageAnswers.Count - 1)
            {
                return;
            }

            _currentPage++;
            ShowScanResult(_currentPage);

            tbPage.Text = (_currentPage + 1).ToString();

            if (_currentPage >= 0 && _currentPage < _pageDnis.Count)
            {
                SelectRowByDni(_pageDnis[_currentPage]);
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
            if (_suppressPageTextChange)
            {
                return;
            }

            //Envair a la página que se escriba en el textbox
            if (int.TryParse(tbPage.Text, out int pageNum) &&
                pageNum > 0 && pageNum <= _pageAnswers.Count)
            {
                _currentPage = pageNum - 1; // Convertir a índice 0
                ShowScanResult(_currentPage);

                if (_currentPage >= 0 && _currentPage < _pageDnis.Count)
                {
                    SelectRowByDni(_pageDnis[_currentPage]);
                }
            }

            if (_pageAnswers.Count > 0)
            {
                lblPageInfo.Text = $"{_currentPage + 1} / {_pageAnswers.Count}";
            }
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
