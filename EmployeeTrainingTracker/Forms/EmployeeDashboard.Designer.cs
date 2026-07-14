namespace EmployeeTrainingTracker
{
    partial class EmployeeDashboard
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EmployeeDashboard));
            tabTraining = new TabPage();
            dgvPlannedTraining = new DataGridView();
            tabCertificates = new TabPage();
            dataGridView1 = new DataGridView();
            txtCertName = new TextBox();
            dtpIssueDate = new DateTimePicker();
            dtpExpiryDate = new DateTimePicker();
            btnEdit = new Button();
            lbl_Certname = new Label();
            lbl_IssueDate = new Label();
            lbl_ExpiryDate = new Label();
            txtKey = new ComboBox();
            txtHrs = new TextBox();
            txtProvider = new TextBox();
            label5 = new Label();
            label6 = new Label();
            label7 = new Label();
            btnUpload = new Button();
            btnRequestDelete = new Button();
            tabControl = new TabControl();
            tabTraining.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).BeginInit();
            tabCertificates.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tabControl.SuspendLayout();
            SuspendLayout();
            // 
            // tabTraining
            // 
            tabTraining.Controls.Add(dgvPlannedTraining);
            tabTraining.Location = new Point(4, 24);
            tabTraining.Name = "tabTraining";
            tabTraining.Padding = new Padding(3);
            tabTraining.Size = new Size(1011, 445);
            tabTraining.TabIndex = 1;
            tabTraining.Text = "Training";
            tabTraining.UseVisualStyleBackColor = true;
            // 
            // dgvPlannedTraining
            // 
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPlannedTraining.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPlannedTraining.Location = new Point(6, 6);
            dgvPlannedTraining.Name = "dgvPlannedTraining";
            dgvPlannedTraining.Size = new Size(994, 430);
            dgvPlannedTraining.TabIndex = 4;
            // 
            // tabCertificates
            // 
            tabCertificates.Controls.Add(btnRequestDelete);
            tabCertificates.Controls.Add(btnUpload);
            tabCertificates.Controls.Add(label7);
            tabCertificates.Controls.Add(label6);
            tabCertificates.Controls.Add(label5);
            tabCertificates.Controls.Add(txtProvider);
            tabCertificates.Controls.Add(txtHrs);
            tabCertificates.Controls.Add(txtCertName);
            tabCertificates.Controls.Add(txtKey);
            tabCertificates.Controls.Add(lbl_ExpiryDate);
            tabCertificates.Controls.Add(lbl_IssueDate);
            tabCertificates.Controls.Add(lbl_Certname);
            tabCertificates.Controls.Add(btnEdit);
            tabCertificates.Controls.Add(dtpExpiryDate);
            tabCertificates.Controls.Add(dtpIssueDate);
            tabCertificates.Controls.Add(dataGridView1);
            tabCertificates.Location = new Point(4, 24);
            tabCertificates.Name = "tabCertificates";
            tabCertificates.Padding = new Padding(3);
            tabCertificates.Size = new Size(1011, 445);
            tabCertificates.TabIndex = 0;
            tabCertificates.Text = "Certificates";
            tabCertificates.UseVisualStyleBackColor = true;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(786, 407);
            dataGridView1.TabIndex = 53;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            dataGridView1.SelectionChanged += dataGridView1_SelectionChanged;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(797, 22);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 1;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(797, 192);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 5;
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(797, 249);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.ShowCheckBox = true;
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 6;
            // 
            // btnEdit
            // 
            btnEdit.Location = new Point(922, 416);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(75, 23);
            btnEdit.TabIndex = 9;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(797, 4);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 60;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(797, 174);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 61;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(797, 231);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 62;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // txtKey
            // 
            txtKey.FormattingEnabled = true;
            txtKey.Items.AddRange(new object[] { "Training", "Retraining", "Proficiency" });
            txtKey.Location = new Point(797, 80);
            txtKey.Name = "txtKey";
            txtKey.Size = new Size(71, 23);
            txtKey.TabIndex = 2;
            // 
            // txtHrs
            // 
            txtHrs.Location = new Point(897, 80);
            txtHrs.Name = "txtHrs";
            txtHrs.Size = new Size(100, 23);
            txtHrs.TabIndex = 3;
            // 
            // txtProvider
            // 
            txtProvider.Location = new Point(797, 129);
            txtProvider.Name = "txtProvider";
            txtProvider.Size = new Size(200, 23);
            txtProvider.TabIndex = 4;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(797, 62);
            label5.Name = "label5";
            label5.Size = new Size(32, 15);
            label5.TabIndex = 67;
            label5.Text = "Key :";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(897, 62);
            label6.Name = "label6";
            label6.Size = new Size(71, 15);
            label6.TabIndex = 68;
            label6.Text = "CPD Hours :";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(797, 111);
            label7.Name = "label7";
            label7.Size = new Size(57, 15);
            label7.TabIndex = 69;
            label7.Text = "Provider :";
            // 
            // btnUpload
            // 
            btnUpload.Location = new Point(922, 287);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(75, 23);
            btnUpload.TabIndex = 70;
            btnUpload.Text = "Upload";
            btnUpload.UseVisualStyleBackColor = true;
            btnUpload.Click += btnUpload_Click;
            // 
            // btnRequestDelete
            // 
            btnRequestDelete.Location = new Point(812, 416);
            btnRequestDelete.Name = "btnRequestDelete";
            btnRequestDelete.Size = new Size(104, 23);
            btnRequestDelete.TabIndex = 71;
            btnRequestDelete.Text = "Request Deletion";
            btnRequestDelete.UseVisualStyleBackColor = true;
            btnRequestDelete.Click += btnRequestDelete_Click;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabCertificates);
            tabControl.Controls.Add(tabTraining);
            tabControl.Location = new Point(3, -1);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1019, 473);
            tabControl.TabIndex = 0;
            // 
            // EmployeeDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1019, 471);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "EmployeeDashboard";
            Text = "Employee Dashboard";
            Load += EmployeeDashboard_Load;
            tabTraining.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).EndInit();
            tabCertificates.ResumeLayout(false);
            tabCertificates.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tabControl.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private Button btnBrowseFile;
        private Label label1;
        private TextBox txtFilePath;
        private TabPage tabTraining;
        private DataGridView dgvPlannedTraining;
        private TabPage tabCertificates;
        private Button btnRequestDelete;
        private Button btnUpload;
        private Label label7;
        private Label label6;
        private Label label5;
        private TextBox txtProvider;
        private TextBox txtHrs;
        private TextBox txtCertName;
        private ComboBox txtKey;
        private Label lbl_ExpiryDate;
        private Label lbl_IssueDate;
        private Label lbl_Certname;
        private Button btnEdit;
        private DateTimePicker dtpExpiryDate;
        private DateTimePicker dtpIssueDate;
        private DataGridView dataGridView1;
        private TabControl tabControl;
    }
}