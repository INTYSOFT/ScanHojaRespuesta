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
            ((System.ComponentModel.ISupportInitialize)pbWarped).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbThresh).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbDni).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvHead).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetalle).BeginInit();
            SuspendLayout();
            // 
            // btnSelectPdf
            // 
            btnSelectPdf.Location = new Point(35, 27);
            btnSelectPdf.Margin = new Padding(7);
            btnSelectPdf.Name = "btnSelectPdf";
            btnSelectPdf.Size = new Size(345, 78);
            btnSelectPdf.TabIndex = 0;
            btnSelectPdf.Text = "Seleccionar PDF";
            btnSelectPdf.UseVisualStyleBackColor = true;
            btnSelectPdf.Click += btnSelectPdf_Click;
            // 
            // txtPdfPath
            // 
            txtPdfPath.Location = new Point(35, 148);
            txtPdfPath.Margin = new Padding(7);
            txtPdfPath.Name = "txtPdfPath";
            txtPdfPath.Size = new Size(614, 63);
            txtPdfPath.TabIndex = 1;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(447, 34);
            btnStart.Margin = new Padding(7);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(207, 78);
            btnStart.TabIndex = 4;
            btnStart.Text = "Iniciar";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(35, 479);
            progressBar.Margin = new Padding(7);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(1343, 78);
            progressBar.TabIndex = 6;
            // 
            // pbWarped
            // 
            pbWarped.Location = new Point(1447, 16);
            pbWarped.Margin = new Padding(7);
            pbWarped.Name = "pbWarped";
            pbWarped.Size = new Size(715, 626);
            pbWarped.SizeMode = PictureBoxSizeMode.Zoom;
            pbWarped.TabIndex = 8;
            pbWarped.TabStop = false;
            pbWarped.Click += pbWarped_Click;
            // 
            // pbThresh
            // 
            pbThresh.Location = new Point(1185, 34);
            pbThresh.Margin = new Padding(7);
            pbThresh.Name = "pbThresh";
            pbThresh.Size = new Size(209, 185);
            pbThresh.SizeMode = PictureBoxSizeMode.Zoom;
            pbThresh.TabIndex = 9;
            pbThresh.TabStop = false;
            // 
            // btnPrev
            // 
            btnPrev.Location = new Point(24, 356);
            btnPrev.Margin = new Padding(7);
            btnPrev.Name = "btnPrev";
            btnPrev.Size = new Size(184, 78);
            btnPrev.TabIndex = 10;
            btnPrev.Text = "Anterior";
            btnPrev.UseVisualStyleBackColor = true;
            btnPrev.Click += btnPrev_Click;
            // 
            // btnNext
            // 
            btnNext.Location = new Point(412, 363);
            btnNext.Margin = new Padding(7);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(205, 78);
            btnNext.TabIndex = 11;
            btnNext.Text = "Siguiente";
            btnNext.UseVisualStyleBackColor = true;
            btnNext.Click += btnNext_Click;
            // 
            // lblPageInfo
            // 
            lblPageInfo.AutoSize = true;
            lblPageInfo.Location = new Point(35, 280);
            lblPageInfo.Margin = new Padding(7, 0, 7, 0);
            lblPageInfo.Name = "lblPageInfo";
            lblPageInfo.Size = new Size(136, 57);
            lblPageInfo.TabIndex = 12;
            lblPageInfo.Text = "label1";
            // 
            // pbDni
            // 
            pbDni.Location = new Point(955, 34);
            pbDni.Margin = new Padding(7);
            pbDni.Name = "pbDni";
            pbDni.Size = new Size(205, 185);
            pbDni.SizeMode = PictureBoxSizeMode.Zoom;
            pbDni.TabIndex = 13;
            pbDni.TabStop = false;
            // 
            // tbPage
            // 
            tbPage.Location = new Point(221, 363);
            tbPage.Margin = new Padding(7);
            tbPage.Name = "tbPage";
            tbPage.Size = new Size(172, 63);
            tbPage.TabIndex = 15;
            tbPage.TextChanged += textBox1_TextChanged;
            // 
            // dgvHead
            // 
            dgvHead.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvHead.Location = new Point(24, 570);
            dgvHead.Margin = new Padding(7);
            dgvHead.MultiSelect = false;
            dgvHead.Name = "dgvHead";
            dgvHead.RowHeadersWidth = 62;
            dgvHead.Size = new Size(1392, 597);
            dgvHead.TabIndex = 17;
            dgvHead.CellContentClick += dgvHead_CellContentClick;
            // 
            // dgvDetalle
            // 
            dgvDetalle.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDetalle.Location = new Point(24, 1181);
            dgvDetalle.Margin = new Padding(7);
            dgvDetalle.Name = "dgvDetalle";
            dgvDetalle.RowHeadersWidth = 62;
            dgvDetalle.Size = new Size(1392, 734);
            dgvDetalle.TabIndex = 18;
            // 
            // cmbEvaluaciones
            // 
            cmbEvaluaciones.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEvaluaciones.FormattingEnabled = true;
            cmbEvaluaciones.Location = new Point(722, 356);
            cmbEvaluaciones.Margin = new Padding(7);
            cmbEvaluaciones.Name = "cmbEvaluaciones";
            cmbEvaluaciones.Size = new Size(619, 65);
            cmbEvaluaciones.TabIndex = 19;
            cmbEvaluaciones.SelectedIndexChanged += cmbEvaluaciones_SelectedIndexChanged;
            // 
            // lblEvaluacion
            // 
            lblEvaluacion.AutoSize = true;
            lblEvaluacion.Location = new Point(722, 280);
            lblEvaluacion.Margin = new Padding(7, 0, 7, 0);
            lblEvaluacion.Name = "lblEvaluacion";
            lblEvaluacion.Size = new Size(461, 57);
            lblEvaluacion.TabIndex = 20;
            lblEvaluacion.Text = "Evaluación programada";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(23F, 57F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(3275, 1847);
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
            Margin = new Padding(7);
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
    }
}
