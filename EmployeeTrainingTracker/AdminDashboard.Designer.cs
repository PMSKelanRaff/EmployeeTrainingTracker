
namespace EmployeeTrainingTracker
{
    partial class AdminDashboard
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AdminDashboard));
            dgvEmployees = new DataGridView();
            dgvCertificates = new DataGridView();
            tabControl = new TabControl();
            tabEmployees = new TabPage();
            lbl_JobTitle = new Label();
            txtJobTitle = new TextBox();
            cmbDept = new ComboBox();
            lbl_Department = new Label();
            btnDeleteEmployee = new Button();
            btnEditEmployee = new Button();
            btnAddEmployee = new Button();
            lbl_Type = new Label();
            lbl_Password = new Label();
            lbl_Username = new Label();
            cmbRole = new ComboBox();
            txtPassword = new TextBox();
            txtUsername = new TextBox();
            tabCertificates = new TabPage();
            label1 = new Label();
            cmbCurrentEmployee = new ComboBox();
            txtFilePath = new TextBox();
            lbl_ExpiryDate = new Label();
            lbl_IssueDate = new Label();
            lbl_Certname = new Label();
            dtpExpiryDate = new DateTimePicker();
            dtpIssueDate = new DateTimePicker();
            txtCertName = new TextBox();
            btnDelete = new Button();
            btnEdit = new Button();
            btnAdd = new Button();
            Reports = new TabPage();
            btnExportCsv = new Button();
            btnGenerateReport = new Button();
            lblDtpEnd = new Label();
            lblDtpStart = new Label();
            dtpEnd = new DateTimePicker();
            dtpStart = new DateTimePicker();
            lblReportType = new Label();
            dgvReportResults = new DataGridView();
            cmbReportType = new ComboBox();
            chkAddToTrainingFolder = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)dgvEmployees).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvCertificates).BeginInit();
            tabControl.SuspendLayout();
            tabEmployees.SuspendLayout();
            tabCertificates.SuspendLayout();
            Reports.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvReportResults).BeginInit();
            SuspendLayout();
            // 
            // dgvEmployees
            // 
            dgvEmployees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvEmployees.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEmployees.Location = new Point(6, 6);
            dgvEmployees.Name = "dgvEmployees";
            dgvEmployees.Size = new Size(619, 379);
            dgvEmployees.TabIndex = 0;
            dgvEmployees.SelectionChanged += dgvEmployees_SelectionChanged;
            // 
            // dgvCertificates
            // 
            dgvCertificates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvCertificates.Location = new Point(6, 6);
            dgvCertificates.Name = "dgvCertificates";
            dgvCertificates.Size = new Size(636, 408);
            dgvCertificates.TabIndex = 1;
            dgvCertificates.CellContentClick += dgvCertificates_CellContentClick;
            dgvCertificates.SelectionChanged += dgvCertificates_SelectionChanged;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabEmployees);
            tabControl.Controls.Add(tabCertificates);
            tabControl.Controls.Add(Reports);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(875, 450);
            tabControl.TabIndex = 2;
            // 
            // tabEmployees
            // 
            tabEmployees.Controls.Add(chkAddToTrainingFolder);
            tabEmployees.Controls.Add(lbl_JobTitle);
            tabEmployees.Controls.Add(txtJobTitle);
            tabEmployees.Controls.Add(cmbDept);
            tabEmployees.Controls.Add(lbl_Department);
            tabEmployees.Controls.Add(btnDeleteEmployee);
            tabEmployees.Controls.Add(btnEditEmployee);
            tabEmployees.Controls.Add(btnAddEmployee);
            tabEmployees.Controls.Add(lbl_Type);
            tabEmployees.Controls.Add(lbl_Password);
            tabEmployees.Controls.Add(lbl_Username);
            tabEmployees.Controls.Add(cmbRole);
            tabEmployees.Controls.Add(txtPassword);
            tabEmployees.Controls.Add(txtUsername);
            tabEmployees.Controls.Add(dgvEmployees);
            tabEmployees.Location = new Point(4, 24);
            tabEmployees.Name = "tabEmployees";
            tabEmployees.Padding = new Padding(3);
            tabEmployees.Size = new Size(867, 422);
            tabEmployees.TabIndex = 0;
            tabEmployees.Text = "Employees";
            tabEmployees.UseVisualStyleBackColor = true;
            // 
            // lbl_JobTitle
            // 
            lbl_JobTitle.AutoSize = true;
            lbl_JobTitle.Location = new Point(631, 277);
            lbl_JobTitle.Name = "lbl_JobTitle";
            lbl_JobTitle.Size = new Size(57, 15);
            lbl_JobTitle.TabIndex = 22;
            lbl_JobTitle.Text = "Job Title :";
            // 
            // txtJobTitle
            // 
            txtJobTitle.Location = new Point(631, 295);
            txtJobTitle.Name = "txtJobTitle";
            txtJobTitle.Size = new Size(228, 23);
            txtJobTitle.TabIndex = 21;
            // 
            // cmbDept
            // 
            cmbDept.FormattingEnabled = true;
            cmbDept.Items.AddRange(new object[] { "Administration", "Engineering", "IT", "Site" });
            cmbDept.Location = new Point(631, 226);
            cmbDept.Name = "cmbDept";
            cmbDept.Size = new Size(228, 23);
            cmbDept.TabIndex = 20;
            // 
            // lbl_Department
            // 
            lbl_Department.AutoSize = true;
            lbl_Department.Location = new Point(631, 208);
            lbl_Department.Name = "lbl_Department";
            lbl_Department.Size = new Size(76, 15);
            lbl_Department.TabIndex = 19;
            lbl_Department.Text = "Department :";
            // 
            // btnDeleteEmployee
            // 
            btnDeleteEmployee.Location = new Point(580, 389);
            btnDeleteEmployee.Name = "btnDeleteEmployee";
            btnDeleteEmployee.Size = new Size(75, 23);
            btnDeleteEmployee.TabIndex = 17;
            btnDeleteEmployee.Text = "Delete";
            btnDeleteEmployee.UseVisualStyleBackColor = true;
            btnDeleteEmployee.Click += btnDeleteEmployee_Click;
            // 
            // btnEditEmployee
            // 
            btnEditEmployee.Location = new Point(703, 389);
            btnEditEmployee.Name = "btnEditEmployee";
            btnEditEmployee.Size = new Size(75, 23);
            btnEditEmployee.TabIndex = 16;
            btnEditEmployee.Text = "Edit";
            btnEditEmployee.UseVisualStyleBackColor = true;
            btnEditEmployee.Click += btnUpdateEmployee_Click;
            // 
            // btnAddEmployee
            // 
            btnAddEmployee.Location = new Point(784, 391);
            btnAddEmployee.Name = "btnAddEmployee";
            btnAddEmployee.Size = new Size(75, 23);
            btnAddEmployee.TabIndex = 15;
            btnAddEmployee.Text = "Add";
            btnAddEmployee.UseVisualStyleBackColor = true;
            btnAddEmployee.Click += btnAddEmployee_Click;
            // 
            // lbl_Type
            // 
            lbl_Type.AutoSize = true;
            lbl_Type.Location = new Point(635, 164);
            lbl_Type.Name = "lbl_Type";
            lbl_Type.Size = new Size(38, 15);
            lbl_Type.TabIndex = 14;
            lbl_Type.Text = "Type :";
            // 
            // lbl_Password
            // 
            lbl_Password.AutoSize = true;
            lbl_Password.Location = new Point(631, 83);
            lbl_Password.Name = "lbl_Password";
            lbl_Password.Size = new Size(63, 15);
            lbl_Password.TabIndex = 13;
            lbl_Password.Text = "Password :";
            // 
            // lbl_Username
            // 
            lbl_Username.AutoSize = true;
            lbl_Username.Location = new Point(631, 39);
            lbl_Username.Name = "lbl_Username";
            lbl_Username.Size = new Size(42, 15);
            lbl_Username.TabIndex = 12;
            lbl_Username.Text = "Email :";
            // 
            // cmbRole
            // 
            cmbRole.FormattingEnabled = true;
            cmbRole.Items.AddRange(new object[] { "Employee", "Admin" });
            cmbRole.Location = new Point(631, 182);
            cmbRole.Name = "cmbRole";
            cmbRole.Size = new Size(228, 23);
            cmbRole.TabIndex = 11;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(631, 101);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(228, 23);
            txtPassword.TabIndex = 10;
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(631, 57);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(228, 23);
            txtUsername.TabIndex = 9;
            // 
            // tabCertificates
            // 
            tabCertificates.Controls.Add(label1);
            tabCertificates.Controls.Add(cmbCurrentEmployee);
            tabCertificates.Controls.Add(txtFilePath);
            tabCertificates.Controls.Add(lbl_ExpiryDate);
            tabCertificates.Controls.Add(lbl_IssueDate);
            tabCertificates.Controls.Add(lbl_Certname);
            tabCertificates.Controls.Add(dtpExpiryDate);
            tabCertificates.Controls.Add(dtpIssueDate);
            tabCertificates.Controls.Add(txtCertName);
            tabCertificates.Controls.Add(btnDelete);
            tabCertificates.Controls.Add(btnEdit);
            tabCertificates.Controls.Add(btnAdd);
            tabCertificates.Controls.Add(dgvCertificates);
            tabCertificates.Location = new Point(4, 24);
            tabCertificates.Name = "tabCertificates";
            tabCertificates.Padding = new Padding(3);
            tabCertificates.Size = new Size(867, 422);
            tabCertificates.TabIndex = 1;
            tabCertificates.Text = "Certificates";
            tabCertificates.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Point, 0);
            label1.Location = new Point(681, 6);
            label1.Name = "label1";
            label1.Size = new Size(126, 17);
            label1.TabIndex = 26;
            label1.Text = "Current Employee :";
            // 
            // cmbCurrentEmployee
            // 
            cmbCurrentEmployee.FormattingEnabled = true;
            cmbCurrentEmployee.Location = new Point(681, 26);
            cmbCurrentEmployee.Name = "cmbCurrentEmployee";
            cmbCurrentEmployee.Size = new Size(126, 23);
            cmbCurrentEmployee.TabIndex = 25;
            // 
            // txtFilePath
            // 
            txtFilePath.Location = new Point(648, 307);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 14;
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(648, 207);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 13;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(648, 153);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 12;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(648, 80);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 11;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(648, 225);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 10;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(648, 171);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 9;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(648, 98);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 8;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(648, 394);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(50, 23);
            btnDelete.TabIndex = 7;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDeleteCert_Click;
            // 
            // btnEdit
            // 
            btnEdit.Location = new Point(760, 394);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(47, 23);
            btnEdit.TabIndex = 6;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEditCert_Click;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(813, 394);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(48, 23);
            btnAdd.TabIndex = 5;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAddCert_Click;
            // 
            // Reports
            // 
            Reports.Controls.Add(btnExportCsv);
            Reports.Controls.Add(btnGenerateReport);
            Reports.Controls.Add(lblDtpEnd);
            Reports.Controls.Add(lblDtpStart);
            Reports.Controls.Add(dtpEnd);
            Reports.Controls.Add(dtpStart);
            Reports.Controls.Add(lblReportType);
            Reports.Controls.Add(dgvReportResults);
            Reports.Controls.Add(cmbReportType);
            Reports.Location = new Point(4, 24);
            Reports.Name = "Reports";
            Reports.Padding = new Padding(3);
            Reports.Size = new Size(867, 422);
            Reports.TabIndex = 2;
            Reports.Text = "Reports";
            Reports.UseVisualStyleBackColor = true;
            // 
            // btnExportCsv
            // 
            btnExportCsv.Location = new Point(771, 391);
            btnExportCsv.Name = "btnExportCsv";
            btnExportCsv.Size = new Size(90, 23);
            btnExportCsv.TabIndex = 9;
            btnExportCsv.Text = "Export CSV";
            btnExportCsv.UseVisualStyleBackColor = true;
            btnExportCsv.Click += btnExportCsv_Click;
            // 
            // btnGenerateReport
            // 
            btnGenerateReport.Location = new Point(771, 362);
            btnGenerateReport.Name = "btnGenerateReport";
            btnGenerateReport.Size = new Size(90, 23);
            btnGenerateReport.TabIndex = 8;
            btnGenerateReport.Text = "Create Report";
            btnGenerateReport.UseVisualStyleBackColor = true;
            btnGenerateReport.Click += btnGenerateReport_Click;
            // 
            // lblDtpEnd
            // 
            lblDtpEnd.AutoSize = true;
            lblDtpEnd.Location = new Point(649, 160);
            lblDtpEnd.Name = "lblDtpEnd";
            lblDtpEnd.Size = new Size(60, 15);
            lblDtpEnd.TabIndex = 7;
            lblDtpEnd.Text = "End Date :";
            // 
            // lblDtpStart
            // 
            lblDtpStart.AutoSize = true;
            lblDtpStart.Location = new Point(649, 106);
            lblDtpStart.Name = "lblDtpStart";
            lblDtpStart.Size = new Size(64, 15);
            lblDtpStart.TabIndex = 6;
            lblDtpStart.Text = "Start Date :";
            // 
            // dtpEnd
            // 
            dtpEnd.Location = new Point(649, 178);
            dtpEnd.Name = "dtpEnd";
            dtpEnd.Size = new Size(200, 23);
            dtpEnd.TabIndex = 5;
            // 
            // dtpStart
            // 
            dtpStart.Location = new Point(649, 124);
            dtpStart.Name = "dtpStart";
            dtpStart.Size = new Size(200, 23);
            dtpStart.TabIndex = 4;
            // 
            // lblReportType
            // 
            lblReportType.AutoSize = true;
            lblReportType.Location = new Point(649, 30);
            lblReportType.Name = "lblReportType";
            lblReportType.Size = new Size(76, 15);
            lblReportType.TabIndex = 3;
            lblReportType.Text = "Report Type :";
            // 
            // dgvReportResults
            // 
            dgvReportResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvReportResults.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvReportResults.Location = new Point(3, 6);
            dgvReportResults.Name = "dgvReportResults";
            dgvReportResults.Size = new Size(640, 413);
            dgvReportResults.TabIndex = 2;
            // 
            // cmbReportType
            // 
            cmbReportType.FormattingEnabled = true;
            cmbReportType.Items.AddRange(new object[] { "Current Year", "Out Of Date", "Custom Range" });
            cmbReportType.Location = new Point(649, 48);
            cmbReportType.Name = "cmbReportType";
            cmbReportType.Size = new Size(121, 23);
            cmbReportType.TabIndex = 0;
            // 
            // chkAddToTrainingFolder
            // 
            chkAddToTrainingFolder.AutoSize = true;
            chkAddToTrainingFolder.Checked = true;
            chkAddToTrainingFolder.CheckState = CheckState.Checked;
            chkAddToTrainingFolder.Location = new Point(631, 324);
            chkAddToTrainingFolder.Name = "chkAddToTrainingFolder";
            chkAddToTrainingFolder.Size = new Size(140, 19);
            chkAddToTrainingFolder.TabIndex = 23;
            chkAddToTrainingFolder.Text = "Add to training folder";
            chkAddToTrainingFolder.UseVisualStyleBackColor = true;
            // 
            // AdminDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(875, 450);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "AdminDashboard";
            Text = "Admin Dashboard";
            ((System.ComponentModel.ISupportInitialize)dgvEmployees).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvCertificates).EndInit();
            tabControl.ResumeLayout(false);
            tabEmployees.ResumeLayout(false);
            tabEmployees.PerformLayout();
            tabCertificates.ResumeLayout(false);
            tabCertificates.PerformLayout();
            Reports.ResumeLayout(false);
            Reports.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvReportResults).EndInit();
            ResumeLayout(false);
        }


        #endregion

        private DataGridView dgvEmployees;
        private DataGridView dgvCertificates;
        private TabControl tabControl;
        private TabPage tabEmployees;
        private TabPage tabCertificates;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private ComboBox cmbRole;
        private TextBox txtPassword;
        private TextBox txtUsername;
        private Label lbl_Username;
        private Label lbl_Password;
        private Label lbl_Type;
        private Button btnDeleteEmployee;
        private Button btnEditEmployee;
        private Button btnAddEmployee;
        private DateTimePicker dtpExpiryDate;
        private DateTimePicker dtpIssueDate;
        private TextBox txtCertName;
        private Label lbl_Certname;
        private Label lbl_IssueDate;
        private Label lbl_ExpiryDate;
        private TextBox txtFilePath;
        private Label lbl_Department;
        private ComboBox cmbDept;
        private Label lbl_JobTitle;
        private TextBox txtJobTitle;
        private TabPage Reports;
        private Label label1;
        private ComboBox cmbCurrentEmployee;
        private ComboBox cmbReportType;
        private DataGridView dgvReportResults;
        private Label lblReportType;
        private DateTimePicker dtpEnd;
        private DateTimePicker dtpStart;
        private Label lblDtpStart;
        private Label lblDtpEnd;
        private Button btnGenerateReport;
        private Button btnExportCsv;
        private CheckBox chkAddToTrainingFolder;
    }
}