namespace ContrlAcademico
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnSelectPdf = new Button();
            txtPdfPath = new TextBox();
            btnStart = new Button();
            progressBar = new ProgressBar();
            pbWarped = new PictureBox();
            pbThresh = new PictureBox();
            btnPrev = new Button();
            btnNext = new Button();
            btnRegistrarDatos = new Button();
            lblPageInfo = new Label();
            pbDni = new PictureBox();
            tbPage = new TextBox();
            dgvHead = new DataGridView();
            dgvDetalle = new DataGridView();
            cmbEvaluaciones = new ComboBox();
            lblEvaluacion = new Label();
            lblSeccion = new Label();
            cmbSecciones = new ComboBox();
            dataGV__noencontradas = new DataGridView();
            label1 = new Label();
            ((System.ComponentModel.ISupportInitialize)pbWarped).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbThresh).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbDni).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvHead).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetalle).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGV__noencontradas).BeginInit();
            SuspendLayout();
            // 
            // btnSelectPdf
            // 
            btnSelectPdf.Location = new Point(15, 82);
            btnSelectPdf.Name = "btnSelectPdf";
            btnSelectPdf.Size = new Size(148, 34);
            btnSelectPdf.TabIndex = 0;
            btnSelectPdf.Text = "Seleccionar PDF";
            btnSelectPdf.UseVisualStyleBackColor = true;
            btnSelectPdf.Click += btnSelectPdf_Click;
            // 
            // txtPdfPath
            // 
            txtPdfPath.Location = new Point(174, 85);
            txtPdfPath.Name = "txtPdfPath";
            txtPdfPath.Size = new Size(465, 31);
            txtPdfPath.TabIndex = 1;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(645, 82);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(100, 34);
            btnStart.TabIndex = 4;
            btnStart.Text = "Iniciar";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(15, 168);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(448, 34);
            progressBar.TabIndex = 6;
            // 
            // pbWarped
            // 
            pbWarped.Location = new Point(751, 36);
            pbWarped.Name = "pbWarped";
            pbWarped.Size = new Size(311, 275);
            pbWarped.SizeMode = PictureBoxSizeMode.Zoom;
            pbWarped.TabIndex = 8;
            pbWarped.TabStop = false;
            pbWarped.Click += pbWarped_Click;
            // 
            // pbThresh
            // 
            pbThresh.Location = new Point(662, 122);
            pbThresh.Name = "pbThresh";
            pbThresh.Size = new Size(83, 47);
            pbThresh.SizeMode = PictureBoxSizeMode.Zoom;
            pbThresh.TabIndex = 9;
            pbThresh.TabStop = false;
            // 
            // btnPrev
            // 
            btnPrev.Location = new Point(15, 128);
            btnPrev.Name = "btnPrev";
            btnPrev.Size = new Size(80, 34);
            btnPrev.TabIndex = 10;
            btnPrev.Text = "Anterior";
            btnPrev.UseVisualStyleBackColor = true;
            btnPrev.Click += btnPrev_Click;
            // 
            // btnNext
            // 
            btnNext.Location = new Point(184, 131);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(105, 34);
            btnNext.TabIndex = 11;
            btnNext.Text = "Siguiente";
            btnNext.UseVisualStyleBackColor = true;
            btnNext.Click += btnNext_Click;
            // 
            // btnRegistrarDatos
            // 
            btnRegistrarDatos.FlatAppearance.MouseOverBackColor = Color.FromArgb(192, 64, 0);
            btnRegistrarDatos.Location = new Point(469, 168);
            btnRegistrarDatos.Name = "btnRegistrarDatos";
            btnRegistrarDatos.Size = new Size(276, 34);
            btnRegistrarDatos.TabIndex = 23;
            btnRegistrarDatos.Text = "Registrar Respuestas";
            btnRegistrarDatos.UseVisualStyleBackColor = true;
            btnRegistrarDatos.Click += btnRegistrarDatos_Click;
            // 
            // lblPageInfo
            // 
            lblPageInfo.AutoSize = true;
            lblPageInfo.Location = new Point(364, 131);
            lblPageInfo.Name = "lblPageInfo";
            lblPageInfo.Size = new Size(59, 25);
            lblPageInfo.TabIndex = 12;
            lblPageInfo.Text = "label1";
            lblPageInfo.Visible = false;
            // 
            // pbDni
            // 
            pbDni.Location = new Point(469, 122);
            pbDni.Name = "pbDni";
            pbDni.Size = new Size(90, 49);
            pbDni.SizeMode = PictureBoxSizeMode.Zoom;
            pbDni.TabIndex = 13;
            pbDni.TabStop = false;
            // 
            // tbPage
            // 
            tbPage.Location = new Point(101, 131);
            tbPage.Name = "tbPage";
            tbPage.Size = new Size(77, 31);
            tbPage.TabIndex = 15;
            tbPage.TextChanged += textBox1_TextChanged;
            // 
            // dgvHead
            // 
            dgvHead.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvHead.Location = new Point(15, 208);
            dgvHead.MultiSelect = false;
            dgvHead.Name = "dgvHead";
            dgvHead.RowHeadersWidth = 62;
            dgvHead.Size = new Size(448, 248);
            dgvHead.TabIndex = 17;
            dgvHead.CellContentClick += dgvHead_CellContentClick;
            // 
            // dgvDetalle
            // 
            dgvDetalle.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDetalle.Location = new Point(469, 208);
            dgvDetalle.Name = "dgvDetalle";
            dgvDetalle.RowHeadersWidth = 62;
            dgvDetalle.Size = new Size(276, 504);
            dgvDetalle.TabIndex = 18;
            // 
            // cmbEvaluaciones
            // 
            cmbEvaluaciones.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEvaluaciones.FormattingEnabled = true;
            cmbEvaluaciones.Location = new Point(15, 36);
            cmbEvaluaciones.Name = "cmbEvaluaciones";
            cmbEvaluaciones.Size = new Size(418, 33);
            cmbEvaluaciones.TabIndex = 19;
            cmbEvaluaciones.SelectedIndexChanged += cmbEvaluaciones_SelectedIndexChanged;
            // 
            // lblEvaluacion
            // 
            lblEvaluacion.AutoSize = true;
            lblEvaluacion.Location = new Point(15, 8);
            lblEvaluacion.Name = "lblEvaluacion";
            lblEvaluacion.Size = new Size(199, 25);
            lblEvaluacion.TabIndex = 20;
            lblEvaluacion.Text = "Evaluación programada";
            // 
            // lblSeccion
            // 
            lblSeccion.AutoSize = true;
            lblSeccion.Location = new Point(445, 8);
            lblSeccion.Name = "lblSeccion";
            lblSeccion.Size = new Size(72, 25);
            lblSeccion.TabIndex = 21;
            lblSeccion.Text = "Sección";
            lblSeccion.Click += lblSeccion_Click;
            // 
            // cmbSecciones
            // 
            cmbSecciones.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSecciones.FormattingEnabled = true;
            cmbSecciones.Location = new Point(439, 36);
            cmbSecciones.Name = "cmbSecciones";
            cmbSecciones.Size = new Size(200, 33);
            cmbSecciones.TabIndex = 22;
            cmbSecciones.SelectedIndexChanged += cmbSecciones_SelectedIndexChanged;
            // 
            // dataGV__noencontradas
            // 
            dataGV__noencontradas.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGV__noencontradas.Location = new Point(15, 497);
            dataGV__noencontradas.MultiSelect = false;
            dataGV__noencontradas.Name = "dataGV__noencontradas";
            dataGV__noencontradas.RowHeadersWidth = 62;
            dataGV__noencontradas.Size = new Size(448, 215);
            dataGV__noencontradas.TabIndex = 24;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(15, 469);
            label1.Name = "label1";
            label1.Size = new Size(138, 25);
            label1.TabIndex = 25;
            label1.Text = "No Encontradas";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1640, 969);
            Controls.Add(label1);
            Controls.Add(dataGV__noencontradas);
            Controls.Add(btnRegistrarDatos);
            Controls.Add(cmbSecciones);
            Controls.Add(lblSeccion);
            Controls.Add(lblEvaluacion);
            Controls.Add(cmbEvaluaciones);
            Controls.Add(dgvDetalle);
            Controls.Add(dgvHead);
            Controls.Add(tbPage);
            Controls.Add(pbDni);
            Controls.Add(lblPageInfo);
            Controls.Add(btnNext);
            Controls.Add(btnPrev);
            Controls.Add(pbThresh);
            Controls.Add(pbWarped);
            Controls.Add(progressBar);
            Controls.Add(btnStart);
            Controls.Add(txtPdfPath);
            Controls.Add(btnSelectPdf);
            Name = "Form1";
            Text = "Lectura Hoja Respustas (OMR)";
            WindowState = FormWindowState.Maximized;
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)pbWarped).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbThresh).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbDni).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvHead).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetalle).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGV__noencontradas).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnSelectPdf;
        private TextBox txtPdfPath;
        private Button btnStart;
        private ProgressBar progressBar;
        private PictureBox pbWarped;
        private PictureBox pbThresh;
        private Button btnPrev;
        private Button btnNext;
        private Label lblPageInfo;
        private PictureBox pbDni;
        private TextBox tbPage;
        private DataGridView dgvHead;
        private DataGridView dgvDetalle;
        private ComboBox cmbEvaluaciones;
        private Label lblEvaluacion;
        private Label lblSeccion;
        private ComboBox cmbSecciones;
        private Button btnRegistrarDatos;
        private DataGridView dataGV__noencontradas;
        private Label label1;
    }
}
