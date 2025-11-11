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
            lblPageInfo = new Label();
            pbDni = new PictureBox();
            tbPage = new TextBox();
            dgvHead = new DataGridView();
            dgvDetalle = new DataGridView();
            cmbEvaluaciones = new ComboBox();
            lblEvaluacion = new Label();
            lblSeccion = new Label();
            cmbSecciones = new ComboBox();
            ((System.ComponentModel.ISupportInitialize)pbWarped).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbThresh).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbDni).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvHead).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetalle).BeginInit();
            SuspendLayout();
            // 
            // btnSelectPdf
            // 
            btnSelectPdf.Location = new Point(15, 12);
            btnSelectPdf.Name = "btnSelectPdf";
            btnSelectPdf.Size = new Size(150, 34);
            btnSelectPdf.TabIndex = 0;
            btnSelectPdf.Text = "Seleccionar PDF";
            btnSelectPdf.UseVisualStyleBackColor = true;
            btnSelectPdf.Click += btnSelectPdf_Click;
            // 
            // txtPdfPath
            // 
            txtPdfPath.Location = new Point(176, 15);
            txtPdfPath.Name = "txtPdfPath";
            txtPdfPath.Size = new Size(343, 31);
            txtPdfPath.TabIndex = 1;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(525, 13);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(90, 34);
            btnStart.TabIndex = 4;
            btnStart.Text = "Iniciar";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(15, 256);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(584, 34);
            progressBar.TabIndex = 6;
            // 
            // pbWarped
            // 
            pbWarped.Location = new Point(629, 7);
            pbWarped.Name = "pbWarped";
            pbWarped.Size = new Size(311, 275);
            pbWarped.SizeMode = PictureBoxSizeMode.Zoom;
            pbWarped.TabIndex = 8;
            pbWarped.TabStop = false;
            pbWarped.Click += pbWarped_Click;
            // 
            // pbThresh
            // 
            pbThresh.Location = new Point(530, 149);
            pbThresh.Name = "pbThresh";
            pbThresh.Size = new Size(85, 75);
            pbThresh.SizeMode = PictureBoxSizeMode.Zoom;
            pbThresh.TabIndex = 9;
            pbThresh.TabStop = false;
            // 
            // btnPrev
            // 
            btnPrev.Location = new Point(10, 211);
            btnPrev.Name = "btnPrev";
            btnPrev.Size = new Size(80, 34);
            btnPrev.TabIndex = 10;
            btnPrev.Text = "Anterior";
            btnPrev.UseVisualStyleBackColor = true;
            btnPrev.Click += btnPrev_Click;
            // 
            // btnNext
            // 
            btnNext.Location = new Point(179, 214);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(105, 34);
            btnNext.TabIndex = 11;
            btnNext.Text = "Siguiente";
            btnNext.UseVisualStyleBackColor = true;
            btnNext.Click += btnNext_Click;
            // 
            // lblPageInfo
            // 
            lblPageInfo.AutoSize = true;
            lblPageInfo.Location = new Point(15, 183);
            lblPageInfo.Name = "lblPageInfo";
            lblPageInfo.Size = new Size(59, 25);
            lblPageInfo.TabIndex = 12;
            lblPageInfo.Text = "label1";
            // 
            // pbDni
            // 
            pbDni.Location = new Point(530, 59);
            pbDni.Name = "pbDni";
            pbDni.Size = new Size(85, 71);
            pbDni.SizeMode = PictureBoxSizeMode.Zoom;
            pbDni.TabIndex = 13;
            pbDni.TabStop = false;
            // 
            // tbPage
            // 
            tbPage.Location = new Point(96, 214);
            tbPage.Name = "tbPage";
            tbPage.Size = new Size(77, 31);
            tbPage.TabIndex = 15;
            tbPage.TextChanged += textBox1_TextChanged;
            // 
            // dgvHead
            // 
            dgvHead.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvHead.Location = new Point(10, 305);
            dgvHead.MultiSelect = false;
            dgvHead.Name = "dgvHead";
            dgvHead.RowHeadersWidth = 62;
            dgvHead.Size = new Size(605, 225);
            dgvHead.TabIndex = 17;
            dgvHead.CellContentClick += dgvHead_CellContentClick;
            // 
            // dgvDetalle
            // 
            dgvDetalle.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDetalle.Location = new Point(10, 536);
            dgvDetalle.Name = "dgvDetalle";
            dgvDetalle.RowHeadersWidth = 62;
            dgvDetalle.Size = new Size(605, 421);
            dgvDetalle.TabIndex = 18;
            // 
            // cmbEvaluaciones
            // 
            cmbEvaluaciones.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEvaluaciones.FormattingEnabled = true;
            cmbEvaluaciones.Location = new Point(15, 80);
            cmbEvaluaciones.Name = "cmbEvaluaciones";
            cmbEvaluaciones.Size = new Size(504, 33);
            cmbEvaluaciones.TabIndex = 19;
            cmbEvaluaciones.SelectedIndexChanged += cmbEvaluaciones_SelectedIndexChanged;
            // 
            // lblEvaluacion
            // 
            lblEvaluacion.AutoSize = true;
            lblEvaluacion.Location = new Point(15, 52);
            lblEvaluacion.Name = "lblEvaluacion";
            lblEvaluacion.Size = new Size(199, 25);
            lblEvaluacion.TabIndex = 20;
            lblEvaluacion.Text = "Evaluación programada";
            // 
            // lblSeccion
            // 
            lblSeccion.AutoSize = true;
            lblSeccion.Location = new Point(15, 123);
            lblSeccion.Name = "lblSeccion";
            lblSeccion.Size = new Size(72, 25);
            lblSeccion.TabIndex = 21;
            lblSeccion.Text = "Sección";
            // 
            // cmbSecciones
            // 
            cmbSecciones.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSecciones.FormattingEnabled = true;
            cmbSecciones.Location = new Point(15, 149);
            cmbSecciones.Name = "cmbSecciones";
            cmbSecciones.Size = new Size(269, 33);
            cmbSecciones.TabIndex = 22;
            cmbSecciones.SelectedIndexChanged += cmbSecciones_SelectedIndexChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1210, 969);
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
            Text = "Form1";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)pbWarped).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbThresh).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbDni).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvHead).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetalle).EndInit();
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
    }
}
