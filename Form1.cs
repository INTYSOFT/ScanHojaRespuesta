using ContrlAcademico.Services;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Runtime.InteropServices;

namespace ContrlAcademico
{
    public partial class Form1 : Form
    {
        // Para almacenar resultados por página
        private List<string> _pageDnis = new();
        private List<char[]> _pageAnswers = new();

        private ConfigModel _config = null!;
        private PdfRenderer? _renderer;
        private OmrProcessor? _omrProcessor;
        private DniExtractor? _dniExtractor;
        private readonly PerspectiveCorrector _perspectiveCorrector = new();

        private bool depurar_imagen = true;
        private readonly string _depurarRoot;
        private bool _depurarErrorNotificado;

        // imágenes debug (warp con rects) y binarizadas
        private List<Bitmap> _debugPages;
        private List<Bitmap> _threshPages;
        private List<char[]> _answersPages;
        private int _currentPage = 0;

        private bool _calibrated = false;
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        private readonly string? _authToken;
        private readonly EvaluacionProgramadaService _evaluacionService = new();
        private readonly EvaluacionRespuestaService _evaluacionRespuestaService = new();
        private List<EvaluacionProgramadaSummaryDto> _evaluacionesProgramadas = new();
        private List<EvaluacionProgramadaConsultaDto> _evaluacionParticipantes = new();
        private readonly Dictionary<string, int> _dniToScanIndex = new(StringComparer.OrdinalIgnoreCase);
        private bool _suppressPageTextChange;
        private bool _isPopulatingSecciones;

        private const string GhostscriptDefaultPath = @"C:\\Program Files\\gs\\gs10.05.1\\bin\\gsdll64.dll";

        private sealed class ScanMetadata
        {
            public ScanMetadata(int scanIndex, DateTime processedOn)
            {
                ScanIndex = scanIndex;
                ProcessedOn = processedOn;
            }

            public int ScanIndex { get; }
            public DateTime ProcessedOn { get; }
        }

        private sealed class SectionItem
        {
            public SectionItem(string display, string? value, int? seccionId)
            {
                Display = display;
                Value = value;
                SeccionId = seccionId;
            }

            public string Display { get; }
            public string? Value { get; }
            public int? SeccionId { get; }

            public override string ToString() => Display;
        }

        public Form1(string? authToken = null)
        {
            _authToken = authToken;
            _depurarRoot = GetDepurarRoot();
            InitializeComponent();
            // suscribirte al evento MouseClick
            pbWarped.MouseClick += PbWarped_MouseClick;




            this.Resize += Form1_Resize;
            AjustarPbWarped();  // Para el tamaño inicial

            LoadConfig();
            // 3) Monta los grids
            SetupHeadGrid();
            SetupDetailGrid();
            SetupMissingGrid();

            // 4) Eventos
            dgvHead.SelectionChanged += dgvHead_SelectionChanged;

            // añade esto:
            dgvHead.CellClick        += dgvHead_CellClick;
            dataGV__noencontradas.SelectionChanged += dataGV__noencontradas_SelectionChanged;
            dataGV__noencontradas.CellClick        += dataGV__noencontradas_CellClick;
            dataGV__noencontradas.CellEndEdit      += dataGV__noencontradas_CellEndEdit;

        }

        private void dgvHead_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Evitamos cabeceras y cols “virtuales”
            if (e.RowIndex < 0) return;

            // 1) Seleccionamos la fila entera
            dgvHead.ClearSelection();
            dgvHead.Rows[e.RowIndex].Selected = true;
            dataGV__noencontradas.ClearSelection();

            MostrarDetalleDesdeFila(e.RowIndex);
        }

        private void dataGV__noencontradas_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            dataGV__noencontradas.ClearSelection();
            dataGV__noencontradas.Rows[e.RowIndex].Selected = true;
            dgvHead.ClearSelection();
            MostrarDetalleDesdeRow(dataGV__noencontradas.Rows[e.RowIndex]);
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

        private bool ValidateHeadGridRows(out List<DataGridViewRow> invalidRows)
        {
            invalidRows = new List<DataGridViewRow>();

            var defaultBackColor = dgvHead.DefaultCellStyle.BackColor;
            var defaultForeColor = dgvHead.DefaultCellStyle.ForeColor;
            var defaultSelectionBackColor = dgvHead.DefaultCellStyle.SelectionBackColor;
            var defaultSelectionForeColor = dgvHead.DefaultCellStyle.SelectionForeColor;

            foreach (DataGridViewRow row in dgvHead.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                bool hasPage = HasRequiredValue(row, "colPage");
                bool hasDni = HasRequiredValue(row, "colDNI");
                bool hasName = HasRequiredValue(row, "colName");

                if (hasPage && hasDni && hasName)
                {
                    ApplyRowValidationStyle(row, defaultBackColor, defaultForeColor, defaultSelectionBackColor, defaultSelectionForeColor);
                }
                else
                {
                    ApplyRowValidationStyle(row, Color.MistyRose, Color.Black, Color.IndianRed, Color.White);
                    invalidRows.Add(row);
                }
            }

            return invalidRows.Count == 0;
        }

        private static bool HasRequiredValue(DataGridViewRow row, string columnName)
            => !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value));

        private static void ApplyRowValidationStyle(
            DataGridViewRow row,
            Color backColor,
            Color foreColor,
            Color selectionBackColor,
            Color selectionForeColor)
        {
            row.DefaultCellStyle.BackColor = backColor;
            row.DefaultCellStyle.ForeColor = foreColor;
            row.DefaultCellStyle.SelectionBackColor = selectionBackColor;
            row.DefaultCellStyle.SelectionForeColor = selectionForeColor;
        }

        private void PopulateSecciones(IEnumerable<EvaluacionProgramadaConsultaDto> participantes)
        {
            _isPopulatingSecciones = true;
            try
            {
                cmbSecciones.DataSource = null;
                cmbSecciones.Items.Clear();

                var sectionsMap = new Dictionary<string, SectionItem>(StringComparer.OrdinalIgnoreCase);

                foreach (var participante in participantes)
                {
                    var normalizedSection = NormalizeSection(participante.Seccion);
                    var displayName = string.IsNullOrWhiteSpace(participante.Seccion)
                        ? "Sin sección"
                        : participante.Seccion.Trim();

                    var key = participante.SeccionId.HasValue
                        ? $"ID:{participante.SeccionId.Value}"
                        : $"NAME:{normalizedSection ?? string.Empty}";

                    if (!sectionsMap.ContainsKey(key))
                    {
                        sectionsMap[key] = new SectionItem(displayName, normalizedSection, participante.SeccionId);
                    }
                }

                var sections = sectionsMap.Values
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
            if (selectedSection.SeccionId.HasValue)
            {
                return participante.SeccionId == selectedSection.SeccionId;
            }

            var participanteSeccion = NormalizeSection(participante.Seccion);

            if (selectedSection.Value is null)
            {
                return participanteSeccion is null;
            }

            return participanteSeccion is not null &&
                   string.Equals(participanteSeccion, selectedSection.Value, StringComparison.OrdinalIgnoreCase);
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

            dataGV__noencontradas.Rows.Clear();
            dataGV__noencontradas.ClearSelection();

            foreach (DataGridViewRow row in dgvHead.Rows)
            {
                row.Cells["colPage"].Value = string.Empty;
                row.Cells["colFecha"].Value = string.Empty;
                row.Tag = null;
            }
        }

        private string GetDepurarRoot()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return @"D:\\depurar";
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "depurar");
        }

        private void SaveDebugImage(string subfolder, string prefix, int pageIndex, Bitmap image)
        {
            try
            {
                string folder = Path.Combine(_depurarRoot, subfolder);
                Directory.CreateDirectory(folder);
                string fileName = $"{prefix}_pagina_{pageIndex + 1:000}.png";
                string destinationPath = Path.Combine(folder, fileName);
                image.Save(destinationPath, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                if (!_depurarErrorNotificado)
                {
                    _depurarErrorNotificado = true;
                    MessageBox.Show(
                        $"No se pudo guardar la imagen de depuración en '{_depurarRoot}':\n{ex.Message}",
                        "Depuración de imágenes",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            finally
            {
                image.Dispose();
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

            RemoveMissingRowByScanIndex(scanIndex);

            if (!string.IsNullOrWhiteSpace(dni))
            {
                RemoveMissingRowByDni(dni);
            }

            int rowIndex = FindRowIndexByDni(dni);
            bool foundInHead = rowIndex >= 0;

            if (foundInHead)
            {
                var row = dgvHead.Rows[rowIndex];
                row.Cells["colPage"].Value = (scanIndex + 1).ToString("000");
                row.Cells["colFecha"].Value = fechaProcesamiento.ToShortDateString();
                row.Cells["colDNI"].Value = dni;
                row.Tag = new ScanMetadata(scanIndex, fechaProcesamiento);
            }
            else
            {
                AddMissingScanEntry(dni, scanIndex, fechaProcesamiento);
            }

            if (foundInHead && !string.IsNullOrWhiteSpace(dni))
            {
                _dniToScanIndex[dni] = scanIndex;
            }
        }

        private bool TryGetScanIndexForRow(DataGridViewRow row, out int scanIndex)
        {
            var metadata = GetScanMetadata(row.Tag);
            if (metadata is not null && metadata.ScanIndex >= 0 && metadata.ScanIndex < _pageAnswers.Count)
            {
                scanIndex = metadata.ScanIndex;
                return true;
            }

            var dni = Convert.ToString(row.Cells["colDNI"].Value)?.Trim();
            if (!string.IsNullOrWhiteSpace(dni) &&
                _dniToScanIndex.TryGetValue(dni, out var mappedIndex) &&
                mappedIndex >= 0 && mappedIndex < _pageAnswers.Count)
            {
                scanIndex = mappedIndex;
                return true;
            }

            scanIndex = -1;
            return false;
        }

        private List<EvaluacionRespuestaCreateDto> BuildResponses(out List<string> warnings)
        {
            warnings = new List<string>();
            var result = new List<EvaluacionRespuestaCreateDto>();

            if (_pageAnswers.Count == 0)
            {
                warnings.Add("No hay respuestas escaneadas para registrar.");
                return result;
            }

            var now = DateTime.UtcNow;

            foreach (DataGridViewRow row in dgvHead.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var dni = Convert.ToString(row.Cells["colDNI"].Value)?.Trim();
                if (string.IsNullOrWhiteSpace(dni))
                {
                    continue;
                }

                if (!TryGetScanIndexForRow(row, out var scanIndex))
                {
                    warnings.Add($"No se encontraron respuestas escaneadas para el DNI {dni}.");
                    continue;
                }

                if (scanIndex < 0 || scanIndex >= _pageAnswers.Count)
                {
                    warnings.Add($"El índice de página asociado al DNI {dni} es inválido.");
                    continue;
                }

                var answers = _pageAnswers[scanIndex];
                if (answers is null || answers.Length == 0)
                {
                    warnings.Add($"La página asociada al DNI {dni} no contiene respuestas.");
                    continue;
                }

                var participante = _evaluacionParticipantes
                    .FirstOrDefault(p => string.Equals(p.AlumnoDni, dni, StringComparison.OrdinalIgnoreCase));

                if (participante is null)
                {
                    warnings.Add($"El DNI {dni} no pertenece a la evaluación seleccionada.");
                    continue;
                }

                for (int i = 0; i < answers.Length; i++)
                {
                    var answer = answers[i];

                    result.Add(new EvaluacionRespuestaCreateDto
                    {
                        EvaluacionId = participante.EvaluacionId,
                        EvaluacionProgramadaId = participante.EvaluacionProgramadaId,
                        SeccionId = participante.SeccionId,
                        Version = 1,
                        PreguntaOrden = i + 1,
                        Respuesta = answer == '-' ? null : answer.ToString(),
                        Fuente = "ScanHojaRespuesta",
                        Activo = true,
                        FechaRegistro = now,
                        DniAlumno = dni
                    });
                }
            }

            return result;
        }

        private async void btnRegistrarDatos_Click(object sender, EventArgs e)
        {
            if (!ValidateHeadGridRows(out var invalidRows))
            {
                if (invalidRows.Count > 0)
                {
                    dgvHead.ClearSelection();
                    invalidRows[0].Selected = true;
                    dgvHead.FirstDisplayedScrollingRowIndex = Math.Max(0, invalidRows[0].Index);
                }

                MessageBox.Show(
                    "Complete los campos Página, DNI y Nombre antes de registrar los datos.",
                    "Validación requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (cmbEvaluaciones.SelectedItem is not EvaluacionProgramadaSummaryDto)
            {
                MessageBox.Show(
                    "Seleccione una evaluación programada antes de registrar los datos.",
                    "Evaluación requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var evaluacionSeleccionada = (EvaluacionProgramadaSummaryDto)cmbEvaluaciones.SelectedItem;

            if (string.IsNullOrWhiteSpace(_authToken))
            {
                MessageBox.Show(
                    "No se encontró el token de autenticación. Inicie sesión nuevamente.",
                    "Autenticación requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_config is null || string.IsNullOrWhiteSpace(_config.ApiEndpoint))
            {
                MessageBox.Show(
                    "La configuración del servicio no es válida. Verifique el archivo config.json.",
                    "Configuración incompleta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var responses = BuildResponses(out var warnings);
            var warningMessages = warnings
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (responses.Count == 0)
            {
                var message = warningMessages.Count > 0
                    ? string.Join(Environment.NewLine, warningMessages)
                    : "No se encontraron respuestas válidas para registrar.";

                MessageBox.Show(
                    message,
                    "Sin datos para registrar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                bool existenRespuestas = await _evaluacionRespuestaService
                    .ExistenRespuestasParaEvaluacionProgramadaAsync(
                        _config.ApiEndpoint,
                        _authToken!,
                        evaluacionSeleccionada.Id)
                    .ConfigureAwait(true);

                if (existenRespuestas)
                {
                    var confirmResult = MessageBox.Show(
                        "Ya existen respuestas registradas para la evaluación seleccionada. Si continúa se eliminarán los datos existentes y se registrarán los nuevos resultados procesados. ¿Desea continuar?",
                        "Confirmar reemplazo",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);

                    if (confirmResult != DialogResult.Yes)
                    {
                        return;
                    }

                    await _evaluacionRespuestaService
                        .EliminarRespuestasParaEvaluacionProgramadaAsync(
                            _config.ApiEndpoint,
                            _authToken!,
                            evaluacionSeleccionada.Id)
                        .ConfigureAwait(true);
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    $"Error de conexión al validar o eliminar respuestas previas: {ex.Message}",
                    "Error de red",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ocurrió un error al validar o eliminar respuestas previas: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            btnRegistrarDatos.Enabled = false;

            try
            {
                await _evaluacionRespuestaService
                    .RegistrarRespuestasAsync(_config.ApiEndpoint, _authToken, responses)
                    .ConfigureAwait(true);

                var alumnosRegistrados = responses
                    .Select(r => r.DniAlumno)
                    .Where(dni => !string.IsNullOrWhiteSpace(dni))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                var builder = new StringBuilder();
                builder.AppendLine($"Se registraron las respuestas de {alumnosRegistrados} alumno(s).");

                if (warningMessages.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Advertencias:");
                    foreach (var warning in warningMessages)
                    {
                        builder.AppendLine(warning);
                    }
                }

                MessageBox.Show(
                    builder.ToString(),
                    "Registro completado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show(
                    $"Error de conexión al registrar las respuestas: {ex.Message}",
                    "Error de red",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ocurrió un error al registrar las respuestas: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnRegistrarDatos.Enabled = true;
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

            MostrarDetalleDesdeRow(dgvHead.Rows[rowIndex]);
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

            _config = ConfigModel.Load(_configPath);

            _renderer = new PdfRenderer(GhostscriptDefaultPath, _config.Dpi);
            _omrProcessor = new OmrProcessor(
                _config.AnswersGrid,
                _config.DniRegion,
                fillThreshold: 0.5,
                meanThreshold: 180,
                deltaMin: 30
            );
            _dniExtractor = new DniExtractor(_config);
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

        private void SetupMissingGrid()
        {
            dataGV__noencontradas.Columns.Clear();
            dataGV__noencontradas.Columns.Add("colMissingPage", "Página");
            dataGV__noencontradas.Columns.Add("colMissingDni", "DNI");
            dataGV__noencontradas.Columns.Add("colMissingFecha", "Fecha");

            dataGV__noencontradas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGV__noencontradas.AllowUserToAddRows    = false;
            dataGV__noencontradas.AllowUserToDeleteRows = false;
            dataGV__noencontradas.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dataGV__noencontradas.MultiSelect           = false;

            if (dataGV__noencontradas.Columns["colMissingPage"] is not null)
            {
                dataGV__noencontradas.Columns["colMissingPage"].ReadOnly = true;
            }

            if (dataGV__noencontradas.Columns["colMissingFecha"] is not null)
            {
                dataGV__noencontradas.Columns["colMissingFecha"].ReadOnly = true;
            }
        }

        private void AddMissingScanEntry(string dni, int scanIndex, DateTime fechaProcesamiento)
        {
            var rowIndex = dataGV__noencontradas.Rows.Add(
                (scanIndex + 1).ToString("000"),
                dni ?? string.Empty,
                fechaProcesamiento.ToShortDateString());

            dataGV__noencontradas.Rows[rowIndex].Tag = new ScanMetadata(scanIndex, fechaProcesamiento);
        }

        private void RemoveMissingRowByScanIndex(int scanIndex)
        {
            for (int i = dataGV__noencontradas.Rows.Count - 1; i >= 0; i--)
            {
                var metadata = GetScanMetadata(dataGV__noencontradas.Rows[i].Tag);
                if (metadata is not null && metadata.ScanIndex == scanIndex)
                {
                    dataGV__noencontradas.Rows.RemoveAt(i);
                }
            }
        }

        private void RemoveMissingRowByDni(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                return;
            }

            for (int i = dataGV__noencontradas.Rows.Count - 1; i >= 0; i--)
            {
                var value = Convert.ToString(dataGV__noencontradas.Rows[i].Cells["colMissingDni"].Value)?.Trim();
                if (string.Equals(value, dni, StringComparison.OrdinalIgnoreCase))
                {
                    dataGV__noencontradas.Rows.RemoveAt(i);
                }
            }
        }

        private static ScanMetadata? GetScanMetadata(object? tag)
            => tag switch
            {
                ScanMetadata metadata => metadata,
                int legacyIndex => new ScanMetadata(legacyIndex, DateTime.MinValue),
                _ => null
            };

        private string? GetDniFromRow(DataGridViewRow row)
        {
            var dataGridView = row.DataGridView;
            if (dataGridView is null)
            {
                return null;
            }

            if (dataGridView.Columns["colDNI"] is not null)
            {
                return Convert.ToString(row.Cells["colDNI"].Value)?.Trim();
            }

            if (dataGridView.Columns["colMissingDni"] is not null)
            {
                return Convert.ToString(row.Cells["colMissingDni"].Value)?.Trim();
            }

            return null;
        }

        private void MostrarDetalleDesdeRow(DataGridViewRow? row)
        {
            if (row is null)
            {
                return;
            }

            var metadata = GetScanMetadata(row.Tag);
            int scanIndex = metadata?.ScanIndex ?? -1;

            if (scanIndex < 0)
            {
                var dni = GetDniFromRow(row);
                if (!string.IsNullOrWhiteSpace(dni) && _dniToScanIndex.TryGetValue(dni, out var mappedIndex))
                {
                    scanIndex = mappedIndex;
                }
            }

            if (scanIndex < 0)
            {
                var dataGridView = row.DataGridView;
                if (dataGridView?.Columns["colPage"] is not null &&
                    row.Cells["colPage"].Value is string pageText &&
                    int.TryParse(pageText, out var pageIndex))
                {
                    scanIndex = pageIndex - 1;
                }
                else if (dataGridView?.Columns["colMissingPage"] is not null &&
                         row.Cells["colMissingPage"].Value is string missingPageText &&
                         int.TryParse(missingPageText, out var missingPageIndex))
                {
                    scanIndex = missingPageIndex - 1;
                }
            }

            if (scanIndex < 0)
            {
                return;
            }

            ShowScanResult(scanIndex);
        }

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            try
            {
                LoadConfig();
                MessageBox.Show(
                    "Configuración cargada correctamente.",
                    "Configuración",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al recargar la configuración: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
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
                if (_renderer is null || _omrProcessor is null || _dniExtractor is null)
                {
                    MessageBox.Show(
                        "El sistema no se inicializó correctamente. Revise la configuración.",
                        "Configuración incompleta",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                var pages = _renderer.RenderPages(txtPdfPath.Text);
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
                    using var warped = _perspectiveCorrector.Correct(pages[i], out _, out _);
                    string dni = ReadDniFromPage(warped, i);

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

                    Bitmap? answersDebugImage = null;
                    var answers = depurar_imagen
                        ? _omrProcessor.Process(warped, out answersDebugImage)
                        : _omrProcessor.Process(warped);
                    if (depurar_imagen && answersDebugImage is not null)
                    {
                        SaveDebugImage("respuestas", "respuestas", i, answersDebugImage);
                    }

                    _pageDnis.Add(dni);
                    _pageAnswers.Add(answers);
                    _answersPages.Add(answers);

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

            if (!ValidateHeadGridRows(out var invalidRows))
            {
                if (invalidRows.Count > 0)
                {
                    dgvHead.ClearSelection();
                    invalidRows[0].Selected = true;
                    dgvHead.FirstDisplayedScrollingRowIndex = Math.Max(0, invalidRows[0].Index);
                }

                MessageBox.Show(
                    "Complete los campos Página, DNI y Nombre antes de registrar los datos.",
                    "Validación requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }


        private void dgvHead_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvHead.SelectedRows.Count == 0) return;
            int idx = dgvHead.SelectedRows[0].Index;
            dataGV__noencontradas.ClearSelection();
            MostrarDetalleDesdeFila(idx);
        }

        private void dataGV__noencontradas_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGV__noencontradas.SelectedRows.Count == 0)
            {
                return;
            }

            dgvHead.ClearSelection();
            MostrarDetalleDesdeRow(dataGV__noencontradas.SelectedRows[0]);
        }

        private void dataGV__noencontradas_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (dataGV__noencontradas.Columns[e.ColumnIndex].Name != "colMissingDni")
            {
                return;
            }

            var row = dataGV__noencontradas.Rows[e.RowIndex];
            var dni = Convert.ToString(row.Cells["colMissingDni"].Value)?.Trim();

            if (string.IsNullOrWhiteSpace(dni))
            {
                return;
            }

            int headRowIndex = FindRowIndexByDni(dni);
            if (headRowIndex < 0)
            {
                MessageBox.Show(
                    "El DNI ingresado no se encuentra en la lista de alumnos cargada.",
                    "DNI no encontrado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var metadata = GetScanMetadata(row.Tag);
            if (metadata is null)
            {
                return;
            }

            UpdateRowWithScanData(dni, metadata.ScanIndex, metadata.ProcessedOn == DateTime.MinValue ? DateTime.Now : metadata.ProcessedOn);

            dataGV__noencontradas.ClearSelection();
            SelectRowByDni(dni);
            MostrarDetalleDesdeFila(headRowIndex);
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
            try
            {
                if (_renderer is null || _dniExtractor is null)
                {
                    MessageBox.Show(
                        "El lector de DNI no está configurado correctamente.",
                        "Configuración incompleta",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                var pages = _renderer.RenderPages(txtPdfPath.Text);
                progressBar.Maximum = pages.Count;

                for (int i = 0; i < pages.Count; i++)
                {
                    using var alignedPage = _perspectiveCorrector.Correct(pages[i], out _, out _);

                    pbWarped.Image?.Dispose();
                    pbWarped.Image = (Bitmap)alignedPage.Clone();

                    var roiRect = CreateNormalizedRect(_config.DniRegionNorm, alignedPage.Width, alignedPage.Height);
                    var drawingRect = new System.Drawing.Rectangle(roiRect.X, roiRect.Y, roiRect.Width, roiRect.Height);
                    using var roiBmp = alignedPage.Clone(drawingRect, alignedPage.PixelFormat);

                    pbThresh.Image?.Dispose();
                    pbThresh.Image = (Bitmap)roiBmp.Clone();

                    Bitmap? dniDebugImage = null;
                    _ = depurar_imagen
                        ? _dniExtractor.Extract(alignedPage, generateDebugImage: true, out dniDebugImage)
                        : _dniExtractor.Extract(alignedPage);
                    if (depurar_imagen && dniDebugImage is not null)
                    {
                        SaveDebugImage("dni", "dni", i, dniDebugImage);
                    }

                    progressBar.Value = i + 1;
                    Application.DoEvents();
                    await Task.Delay(200);
                }

                MessageBox.Show("Lectura de DNI completada.", "OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                btnStart.Enabled = true;
            }
        }


        private string ReadDniFromPage(Bitmap alignedPage, int pageIndex)
        {
            if (_dniExtractor is null)
            {
                throw new InvalidOperationException("El extractor de DNI no está inicializado.");
            }

            int width = alignedPage.Width;
            int height = alignedPage.Height;
            var roiRect = CreateNormalizedRect(_config.DniRegionNorm, width, height);

            using var pageMat = BitmapConverter.ToMat(alignedPage);
            using var debugMat = pageMat.Clone();
            Cv2.Rectangle(debugMat, roiRect, Scalar.Red, 2);
            _debugPages.Add(BitmapConverter.ToBitmap(debugMat));

            using var gray = new Mat();
            Cv2.CvtColor(pageMat, gray, ColorConversionCodes.BGR2GRAY);
            using var thresh = new Mat();
            Cv2.AdaptiveThreshold(
                gray,
                thresh,
                255,
                AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv,
                11,
                2);
            _threshPages.Add(BitmapConverter.ToBitmap(thresh));

            string dni;
            if (depurar_imagen)
            {
                string extracted = _dniExtractor.Extract(alignedPage, generateDebugImage: true, out var dniDebugImage);
                if (dniDebugImage is not null)
                {
                    SaveDebugImage("dni", "dni", pageIndex, dniDebugImage);
                }

                dni = extracted;
            }
            else
            {
                dni = _dniExtractor.Extract(alignedPage);
            }

            return dni;
        }

        private static Rect CreateNormalizedRect(NormRoiModel norm, int width, int height)
        {
            int x = (int)Math.Round(norm.X * width);
            int y = (int)Math.Round(norm.Y * height);
            int w = (int)Math.Round(norm.W * width);
            int h = (int)Math.Round(norm.H * height);

            var rect = new Rect(x, y, w, h);
            rect = rect.Intersect(new Rect(0, 0, width, height));

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                throw new InvalidOperationException("La región normalizada del DNI está fuera de la página.");
            }

            return rect;
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

        private void lblSeccion_Click(object sender, EventArgs e)
        {

        }
    }
}
