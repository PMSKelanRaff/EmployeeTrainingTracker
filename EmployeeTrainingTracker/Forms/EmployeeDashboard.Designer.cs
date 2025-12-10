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
            tabControl = new TabControl();
            tabPage1 = new TabPage();
            btnBrowseFile = new Button();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            txtProvider = new TextBox();
            txtHrs = new TextBox();
            txtKey = new ComboBox();
            label1 = new Label();
            lbl_ExpiryDate = new Label();
            lbl_IssueDate = new Label();
            lbl_Certname = new Label();
            txtFilePath = new TextBox();
            btnEdit = new Button();
            btnAdd = new Button();
            dtpExpiryDate = new DateTimePicker();
            dtpIssueDate = new DateTimePicker();
            txtCertName = new TextBox();
            dataGridView1 = new DataGridView();
            tabPage2 = new TabPage();
            dgvPlannedTraining = new DataGridView();
            tabControl.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).BeginInit();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabPage1);
            tabControl.Controls.Add(tabPage2);
            tabControl.Location = new Point(3, -1);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1019, 473);
            tabControl.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(btnBrowseFile);
            tabPage1.Controls.Add(label7);
            tabPage1.Controls.Add(label6);
            tabPage1.Controls.Add(label5);
            tabPage1.Controls.Add(txtProvider);
            tabPage1.Controls.Add(txtHrs);
            tabPage1.Controls.Add(txtKey);
            tabPage1.Controls.Add(label1);
            tabPage1.Controls.Add(lbl_ExpiryDate);
            tabPage1.Controls.Add(lbl_IssueDate);
            tabPage1.Controls.Add(lbl_Certname);
            tabPage1.Controls.Add(txtFilePath);
            tabPage1.Controls.Add(btnEdit);
            tabPage1.Controls.Add(btnAdd);
            tabPage1.Controls.Add(dtpExpiryDate);
            tabPage1.Controls.Add(dtpIssueDate);
            tabPage1.Controls.Add(txtCertName);
            tabPage1.Controls.Add(dataGridView1);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1011, 445);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Certificates";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // btnBrowseFile
            // 
            btnBrowseFile.Location = new Point(944, 348);
            btnBrowseFile.Name = "btnBrowseFile";
            btnBrowseFile.Size = new Size(53, 22);
            btnBrowseFile.TabIndex = 8;
            btnBrowseFile.Text = "Browse";
            btnBrowseFile.UseVisualStyleBackColor = true;
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
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(897, 62);
            label6.Name = "label6";
            label6.Size = new Size(45, 15);
            label6.TabIndex = 68;
            label6.Text = "Hours :";
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
            // txtProvider
            // 
            txtProvider.Location = new Point(797, 129);
            txtProvider.Name = "txtProvider";
            txtProvider.Size = new Size(200, 23);
            txtProvider.TabIndex = 4;
            // 
            // txtHrs
            // 
            txtHrs.Location = new Point(897, 80);
            txtHrs.Name = "txtHrs";
            txtHrs.Size = new Size(100, 23);
            txtHrs.TabIndex = 3;
            // 
            // txtKey
            // 
            txtKey.FormattingEnabled = true;
            txtKey.Items.AddRange(new object[] { "T", "R", "P" });
            txtKey.Location = new Point(797, 80);
            txtKey.Name = "txtKey";
            txtKey.Size = new Size(71, 23);
            txtKey.TabIndex = 2;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(797, 301);
            label1.Name = "label1";
            label1.Size = new Size(115, 15);
            label1.TabIndex = 63;
            label1.Text = "Certificate File path :";
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
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(797, 174);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 61;
            lbl_IssueDate.Text = "Issue Date :";
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
            // txtFilePath
            // 
            txtFilePath.Location = new Point(797, 319);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 7;
            // 
            // btnEdit
            // 
            btnEdit.Location = new Point(850, 417);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(75, 23);
            btnEdit.TabIndex = 9;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(931, 417);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(75, 23);
            btnAdd.TabIndex = 10;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(797, 249);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.ShowCheckBox = true;
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 6;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(797, 192);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 5;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(797, 22);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 1;
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
            // tabPage2
            // 
            tabPage2.Controls.Add(dgvPlannedTraining);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1011, 445);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Training";
            tabPage2.UseVisualStyleBackColor = true;
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
            tabControl.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private Button btnBrowseFile;
        private Label label7;
        private Label label6;
        private Label label5;
        private TextBox txtProvider;
        private TextBox txtHrs;
        private ComboBox txtKey;
        private Label label1;
        private Label lbl_ExpiryDate;
        private Label lbl_IssueDate;
        private Label lbl_Certname;
        private TextBox txtFilePath;
        private Button btnEdit;
        private Button btnAdd;
        private DateTimePicker dtpExpiryDate;
        private DateTimePicker dtpIssueDate;
        private TextBox txtCertName;
        private DataGridView dataGridView1;
        private DataGridView dgvPlannedTraining;
    }
}