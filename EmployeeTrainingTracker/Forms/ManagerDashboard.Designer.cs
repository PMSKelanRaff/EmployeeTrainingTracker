
namespace EmployeeTrainingTracker
{
    partial class ManagerDashboard
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ManagerDashboard));
            dgvEmployees = new DataGridView();
            dgvCertificates = new DataGridView();
            tabControl = new TabControl();
            tabEmployees = new TabPage();
            lbl_FullName = new Label();
            txtFullName = new TextBox();
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
            btnUpload = new Button();
            label10 = new Label();
            label11 = new Label();
            label12 = new Label();
            txtProviderCertsTab = new TextBox();
            txtHrsCertsTab = new TextBox();
            txtKeyCertsTab = new ComboBox();
            label9 = new Label();
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
            Reports = new TabPage();
            label15 = new Label();
            clbGroups = new CheckedListBox();
            clbEmployees = new CheckedListBox();
            label8 = new Label();
            btnExportCsv = new Button();
            btnGenerateReport = new Button();
            lblDtpEnd = new Label();
            lblDtpStart = new Label();
            dtpEnd = new DateTimePicker();
            dtpStart = new DateTimePicker();
            lblReportType = new Label();
            dgvReportResults = new DataGridView();
            cmbReportType = new ComboBox();
            Planning = new TabPage();
            textStatusPlan = new ComboBox();
            btnCompleteTraining = new Button();
            label2 = new Label();
            textNotesPlan = new TextBox();
            label3 = new Label();
            clbEmployeesPlan = new CheckedListBox();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            txtProviderPlan = new TextBox();
            txtHrsPlan = new TextBox();
            txtKeyPlan = new ComboBox();
            statusTxt = new Label();
            plannedDateTxt = new Label();
            label4 = new Label();
            dtpPlannedDate = new DateTimePicker();
            txtCertificateNamePlan = new TextBox();
            btnDeleteSession = new Button();
            btnEditSession = new Button();
            btnAddSession = new Button();
            dgvPlannedTraining = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dgvEmployees).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvCertificates).BeginInit();
            tabControl.SuspendLayout();
            tabEmployees.SuspendLayout();
            tabCertificates.SuspendLayout();
            Reports.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvReportResults).BeginInit();
            Planning.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).BeginInit();
            SuspendLayout();
            // 
            // dgvEmployees
            // 
            dgvEmployees.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvEmployees.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEmployees.Location = new Point(6, 6);
            dgvEmployees.Name = "dgvEmployees";
            dgvEmployees.Size = new Size(763, 410);
            dgvEmployees.TabIndex = 0;
            dgvEmployees.SelectionChanged += dgvEmployees_SelectionChanged;
            // 
            // dgvCertificates
            // 
            dgvCertificates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvCertificates.Location = new Point(6, 6);
            dgvCertificates.Name = "dgvCertificates";
            dgvCertificates.Size = new Size(780, 408);
            dgvCertificates.TabIndex = 1;
            dgvCertificates.CellContentClick += dgvCertificates_CellContentClick;
            dgvCertificates.SelectionChanged += dgvCertificates_SelectionChanged;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabEmployees);
            tabControl.Controls.Add(tabCertificates);
            tabControl.Controls.Add(Reports);
            tabControl.Controls.Add(Planning);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1019, 483);
            tabControl.TabIndex = 2;
            // 
            // tabEmployees
            // 
            tabEmployees.BackColor = Color.Transparent;
            tabEmployees.Controls.Add(lbl_FullName);
            tabEmployees.Controls.Add(txtFullName);
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
            tabEmployees.Size = new Size(1011, 455);
            tabEmployees.TabIndex = 0;
            tabEmployees.Text = "Employees";
            // 
            // lbl_FullName
            // 
            lbl_FullName.Anchor = AnchorStyles.Right;
            lbl_FullName.AutoSize = true;
            lbl_FullName.Location = new Point(775, 11);
            lbl_FullName.Name = "lbl_FullName";
            lbl_FullName.Size = new Size(67, 15);
            lbl_FullName.TabIndex = 24;
            lbl_FullName.Text = "Full Name :";
            // 
            // txtFullName
            // 
            txtFullName.Anchor = AnchorStyles.Right;
            txtFullName.Location = new Point(775, 29);
            txtFullName.Name = "txtFullName";
            txtFullName.Size = new Size(228, 23);
            txtFullName.TabIndex = 23;
            // 
            // lbl_JobTitle
            // 
            lbl_JobTitle.Anchor = AnchorStyles.Right;
            lbl_JobTitle.AutoSize = true;
            lbl_JobTitle.Location = new Point(775, 293);
            lbl_JobTitle.Name = "lbl_JobTitle";
            lbl_JobTitle.Size = new Size(57, 15);
            lbl_JobTitle.TabIndex = 22;
            lbl_JobTitle.Text = "Job Title :";
            // 
            // txtJobTitle
            // 
            txtJobTitle.Anchor = AnchorStyles.Right;
            txtJobTitle.Location = new Point(775, 311);
            txtJobTitle.Name = "txtJobTitle";
            txtJobTitle.Size = new Size(228, 23);
            txtJobTitle.TabIndex = 5;
            // 
            // cmbDept
            // 
            cmbDept.Anchor = AnchorStyles.Right;
            cmbDept.FormattingEnabled = true;
            cmbDept.Items.AddRange(new object[] { "Administration", "Engineering", "IT", "Site" });
            cmbDept.Location = new Point(775, 242);
            cmbDept.Name = "cmbDept";
            cmbDept.Size = new Size(228, 23);
            cmbDept.TabIndex = 4;
            // 
            // lbl_Department
            // 
            lbl_Department.Anchor = AnchorStyles.Right;
            lbl_Department.AutoSize = true;
            lbl_Department.Location = new Point(775, 224);
            lbl_Department.Name = "lbl_Department";
            lbl_Department.Size = new Size(76, 15);
            lbl_Department.TabIndex = 19;
            lbl_Department.Text = "Department :";
            // 
            // btnDeleteEmployee
            // 
            btnDeleteEmployee.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnDeleteEmployee.Location = new Point(724, 422);
            btnDeleteEmployee.Name = "btnDeleteEmployee";
            btnDeleteEmployee.Size = new Size(75, 23);
            btnDeleteEmployee.TabIndex = 6;
            btnDeleteEmployee.Text = "Delete";
            btnDeleteEmployee.UseVisualStyleBackColor = true;
            btnDeleteEmployee.Click += btnDeleteEmployee_Click;
            // 
            // btnEditEmployee
            // 
            btnEditEmployee.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnEditEmployee.Location = new Point(847, 422);
            btnEditEmployee.Name = "btnEditEmployee";
            btnEditEmployee.Size = new Size(75, 23);
            btnEditEmployee.TabIndex = 7;
            btnEditEmployee.Text = "Edit";
            btnEditEmployee.UseVisualStyleBackColor = true;
            btnEditEmployee.Click += btnUpdateEmployee_Click;
            // 
            // btnAddEmployee
            // 
            btnAddEmployee.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAddEmployee.Location = new Point(928, 424);
            btnAddEmployee.Name = "btnAddEmployee";
            btnAddEmployee.Size = new Size(75, 23);
            btnAddEmployee.TabIndex = 8;
            btnAddEmployee.Text = "Add";
            btnAddEmployee.UseVisualStyleBackColor = true;
            btnAddEmployee.Click += btnAddEmployee_Click;
            // 
            // lbl_Type
            // 
            lbl_Type.Anchor = AnchorStyles.Right;
            lbl_Type.AutoSize = true;
            lbl_Type.Location = new Point(779, 180);
            lbl_Type.Name = "lbl_Type";
            lbl_Type.Size = new Size(38, 15);
            lbl_Type.TabIndex = 14;
            lbl_Type.Text = "Type :";
            // 
            // lbl_Password
            // 
            lbl_Password.Anchor = AnchorStyles.Right;
            lbl_Password.AutoSize = true;
            lbl_Password.Location = new Point(775, 99);
            lbl_Password.Name = "lbl_Password";
            lbl_Password.Size = new Size(63, 15);
            lbl_Password.TabIndex = 13;
            lbl_Password.Text = "Password :";
            // 
            // lbl_Username
            // 
            lbl_Username.Anchor = AnchorStyles.Right;
            lbl_Username.AutoSize = true;
            lbl_Username.Location = new Point(775, 55);
            lbl_Username.Name = "lbl_Username";
            lbl_Username.Size = new Size(42, 15);
            lbl_Username.TabIndex = 12;
            lbl_Username.Text = "Email :";
            // 
            // cmbRole
            // 
            cmbRole.Anchor = AnchorStyles.Right;
            cmbRole.FormattingEnabled = true;
            cmbRole.Items.AddRange(new object[] { "Employee", "Manager", "Admin" });
            cmbRole.Location = new Point(775, 198);
            cmbRole.Name = "cmbRole";
            cmbRole.Size = new Size(228, 23);
            cmbRole.TabIndex = 3;
            // 
            // txtPassword
            // 
            txtPassword.Anchor = AnchorStyles.Right;
            txtPassword.Location = new Point(775, 117);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(228, 23);
            txtPassword.TabIndex = 2;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // txtUsername
            // 
            txtUsername.Anchor = AnchorStyles.Right;
            txtUsername.Location = new Point(775, 73);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(228, 23);
            txtUsername.TabIndex = 1;
            // 
            // tabCertificates
            // 
            tabCertificates.Controls.Add(btnUpload);
            tabCertificates.Controls.Add(label10);
            tabCertificates.Controls.Add(label11);
            tabCertificates.Controls.Add(label12);
            tabCertificates.Controls.Add(txtProviderCertsTab);
            tabCertificates.Controls.Add(txtHrsCertsTab);
            tabCertificates.Controls.Add(txtKeyCertsTab);
            tabCertificates.Controls.Add(label9);
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
            tabCertificates.Controls.Add(dgvCertificates);
            tabCertificates.Location = new Point(4, 24);
            tabCertificates.Name = "tabCertificates";
            tabCertificates.Padding = new Padding(3);
            tabCertificates.Size = new Size(1011, 455);
            tabCertificates.TabIndex = 1;
            tabCertificates.Text = "Certificates";
            tabCertificates.UseVisualStyleBackColor = true;
            // 
            // btnUpload
            // 
            btnUpload.Anchor = AnchorStyles.Right;
            btnUpload.Location = new Point(939, 376);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(53, 22);
            btnUpload.TabIndex = 51;
            btnUpload.Text = "Upload";
            btnUpload.UseVisualStyleBackColor = true;
            btnUpload.Click += btnUpload_Click;
            // 
            // label10
            // 
            label10.Anchor = AnchorStyles.Right;
            label10.AutoSize = true;
            label10.Location = new Point(792, 175);
            label10.Name = "label10";
            label10.Size = new Size(57, 15);
            label10.TabIndex = 50;
            label10.Text = "Provider :";
            // 
            // label11
            // 
            label11.Anchor = AnchorStyles.Right;
            label11.AutoSize = true;
            label11.Location = new Point(892, 126);
            label11.Name = "label11";
            label11.Size = new Size(71, 15);
            label11.TabIndex = 49;
            label11.Text = "CPD Hours :";
            // 
            // label12
            // 
            label12.Anchor = AnchorStyles.Right;
            label12.AutoSize = true;
            label12.Location = new Point(792, 126);
            label12.Name = "label12";
            label12.Size = new Size(32, 15);
            label12.TabIndex = 48;
            label12.Text = "Key :";
            // 
            // txtProviderCertsTab
            // 
            txtProviderCertsTab.Anchor = AnchorStyles.Right;
            txtProviderCertsTab.Location = new Point(792, 193);
            txtProviderCertsTab.Name = "txtProviderCertsTab";
            txtProviderCertsTab.Size = new Size(200, 23);
            txtProviderCertsTab.TabIndex = 5;
            // 
            // txtHrsCertsTab
            // 
            txtHrsCertsTab.Anchor = AnchorStyles.Right;
            txtHrsCertsTab.Location = new Point(892, 144);
            txtHrsCertsTab.Name = "txtHrsCertsTab";
            txtHrsCertsTab.Size = new Size(100, 23);
            txtHrsCertsTab.TabIndex = 4;
            // 
            // txtKeyCertsTab
            // 
            txtKeyCertsTab.Anchor = AnchorStyles.Right;
            txtKeyCertsTab.FormattingEnabled = true;
            txtKeyCertsTab.Items.AddRange(new object[] { "Training", "Retraining", "Proficiency" });
            txtKeyCertsTab.Location = new Point(792, 144);
            txtKeyCertsTab.Name = "txtKeyCertsTab";
            txtKeyCertsTab.Size = new Size(71, 23);
            txtKeyCertsTab.TabIndex = 3;
            // 
            // label9
            // 
            label9.Anchor = AnchorStyles.Right;
            label9.AutoSize = true;
            label9.Location = new Point(792, 329);
            label9.Name = "label9";
            label9.Size = new Size(58, 15);
            label9.TabIndex = 28;
            label9.Text = "File Path :";
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Point, 0);
            label1.Location = new Point(825, 6);
            label1.Name = "label1";
            label1.Size = new Size(126, 17);
            label1.TabIndex = 26;
            label1.Text = "Current Employee :";
            // 
            // cmbCurrentEmployee
            // 
            cmbCurrentEmployee.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbCurrentEmployee.FormattingEnabled = true;
            cmbCurrentEmployee.Location = new Point(825, 26);
            cmbCurrentEmployee.Name = "cmbCurrentEmployee";
            cmbCurrentEmployee.Size = new Size(126, 23);
            cmbCurrentEmployee.TabIndex = 1;
            // 
            // txtFilePath
            // 
            txtFilePath.Anchor = AnchorStyles.Right;
            txtFilePath.Location = new Point(792, 347);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 8;
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.Anchor = AnchorStyles.Right;
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(792, 273);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 13;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.Anchor = AnchorStyles.Right;
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(792, 229);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 12;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_Certname
            // 
            lbl_Certname.Anchor = AnchorStyles.Right;
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(792, 78);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 11;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Anchor = AnchorStyles.Right;
            dtpExpiryDate.Location = new Point(792, 291);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.ShowCheckBox = true;
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 7;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Anchor = AnchorStyles.Right;
            dtpIssueDate.Location = new Point(792, 247);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 6;
            // 
            // txtCertName
            // 
            txtCertName.Anchor = AnchorStyles.Right;
            txtCertName.Location = new Point(792, 96);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 2;
            // 
            // btnDelete
            // 
            btnDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnDelete.Location = new Point(902, 426);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(50, 23);
            btnDelete.TabIndex = 11;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDeleteCert_Click;
            // 
            // btnEdit
            // 
            btnEdit.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnEdit.Location = new Point(958, 426);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(47, 23);
            btnEdit.TabIndex = 12;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEditCert_Click;
            // 
            // Reports
            // 
            Reports.Controls.Add(label15);
            Reports.Controls.Add(clbGroups);
            Reports.Controls.Add(clbEmployees);
            Reports.Controls.Add(label8);
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
            Reports.Size = new Size(1011, 455);
            Reports.TabIndex = 2;
            Reports.Text = "Reports";
            Reports.UseVisualStyleBackColor = true;
            // 
            // label15
            // 
            label15.Anchor = AnchorStyles.Right;
            label15.AutoSize = true;
            label15.Location = new Point(793, 282);
            label15.Name = "label15";
            label15.Size = new Size(51, 15);
            label15.TabIndex = 54;
            label15.Text = "Groups :";
            // 
            // clbGroups
            // 
            clbGroups.Anchor = AnchorStyles.Right;
            clbGroups.CheckOnClick = true;
            clbGroups.FormattingEnabled = true;
            clbGroups.Location = new Point(794, 300);
            clbGroups.Name = "clbGroups";
            clbGroups.Size = new Size(199, 94);
            clbGroups.TabIndex = 5;
            // 
            // clbEmployees
            // 
            clbEmployees.Anchor = AnchorStyles.Right;
            clbEmployees.CheckOnClick = true;
            clbEmployees.FormattingEnabled = true;
            clbEmployees.Location = new Point(793, 183);
            clbEmployees.Name = "clbEmployees";
            clbEmployees.Size = new Size(199, 94);
            clbEmployees.TabIndex = 4;
            // 
            // label8
            // 
            label8.Anchor = AnchorStyles.Right;
            label8.AutoSize = true;
            label8.Location = new Point(793, 165);
            label8.Name = "label8";
            label8.Size = new Size(70, 15);
            label8.TabIndex = 51;
            label8.Text = "Employees :";
            // 
            // btnExportCsv
            // 
            btnExportCsv.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnExportCsv.Location = new Point(824, 424);
            btnExportCsv.Name = "btnExportCsv";
            btnExportCsv.Size = new Size(90, 23);
            btnExportCsv.TabIndex = 6;
            btnExportCsv.Text = "Export CSV";
            btnExportCsv.UseVisualStyleBackColor = true;
            btnExportCsv.Click += btnExportCsv_Click;
            // 
            // btnGenerateReport
            // 
            btnGenerateReport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnGenerateReport.Location = new Point(915, 424);
            btnGenerateReport.Name = "btnGenerateReport";
            btnGenerateReport.Size = new Size(90, 23);
            btnGenerateReport.TabIndex = 7;
            btnGenerateReport.Text = "Create Report";
            btnGenerateReport.UseVisualStyleBackColor = true;
            btnGenerateReport.Click += btnGenerateReport_Click;
            // 
            // lblDtpEnd
            // 
            lblDtpEnd.Anchor = AnchorStyles.Right;
            lblDtpEnd.AutoSize = true;
            lblDtpEnd.Location = new Point(793, 110);
            lblDtpEnd.Name = "lblDtpEnd";
            lblDtpEnd.Size = new Size(60, 15);
            lblDtpEnd.TabIndex = 7;
            lblDtpEnd.Text = "End Date :";
            // 
            // lblDtpStart
            // 
            lblDtpStart.Anchor = AnchorStyles.Right;
            lblDtpStart.AutoSize = true;
            lblDtpStart.Location = new Point(793, 66);
            lblDtpStart.Name = "lblDtpStart";
            lblDtpStart.Size = new Size(64, 15);
            lblDtpStart.TabIndex = 6;
            lblDtpStart.Text = "Start Date :";
            // 
            // dtpEnd
            // 
            dtpEnd.Anchor = AnchorStyles.Right;
            dtpEnd.Location = new Point(793, 128);
            dtpEnd.Name = "dtpEnd";
            dtpEnd.Size = new Size(200, 23);
            dtpEnd.TabIndex = 3;
            // 
            // dtpStart
            // 
            dtpStart.Anchor = AnchorStyles.Right;
            dtpStart.Location = new Point(793, 84);
            dtpStart.Name = "dtpStart";
            dtpStart.Size = new Size(200, 23);
            dtpStart.TabIndex = 2;
            // 
            // lblReportType
            // 
            lblReportType.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblReportType.AutoSize = true;
            lblReportType.Location = new Point(793, 22);
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
            dgvReportResults.Size = new Size(784, 413);
            dgvReportResults.TabIndex = 2;
            // 
            // cmbReportType
            // 
            cmbReportType.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbReportType.FormattingEnabled = true;
            cmbReportType.Items.AddRange(new object[] { "Current Year (Valid)", "Custom Range (Valid)", "Out Of Date (Invalid)", "Custom Range (Invalid)" });
            cmbReportType.Location = new Point(793, 40);
            cmbReportType.Name = "cmbReportType";
            cmbReportType.Size = new Size(121, 23);
            cmbReportType.TabIndex = 1;
            cmbReportType.SelectedIndexChanged += cmbReportType_SelectedIndexChanged;
            // 
            // Planning
            // 
            Planning.Controls.Add(textStatusPlan);
            Planning.Controls.Add(btnCompleteTraining);
            Planning.Controls.Add(label2);
            Planning.Controls.Add(textNotesPlan);
            Planning.Controls.Add(label3);
            Planning.Controls.Add(clbEmployeesPlan);
            Planning.Controls.Add(label7);
            Planning.Controls.Add(label6);
            Planning.Controls.Add(label5);
            Planning.Controls.Add(txtProviderPlan);
            Planning.Controls.Add(txtHrsPlan);
            Planning.Controls.Add(txtKeyPlan);
            Planning.Controls.Add(statusTxt);
            Planning.Controls.Add(plannedDateTxt);
            Planning.Controls.Add(label4);
            Planning.Controls.Add(dtpPlannedDate);
            Planning.Controls.Add(txtCertificateNamePlan);
            Planning.Controls.Add(btnDeleteSession);
            Planning.Controls.Add(btnEditSession);
            Planning.Controls.Add(btnAddSession);
            Planning.Controls.Add(dgvPlannedTraining);
            Planning.Location = new Point(4, 24);
            Planning.Name = "Planning";
            Planning.Padding = new Padding(3);
            Planning.Size = new Size(1011, 455);
            Planning.TabIndex = 3;
            Planning.Text = "Planning";
            Planning.UseVisualStyleBackColor = true;
            // 
            // textStatusPlan
            // 
            textStatusPlan.Anchor = AnchorStyles.Right;
            textStatusPlan.FormattingEnabled = true;
            textStatusPlan.Items.AddRange(new object[] { "Planned", "Completed", "Cancelled" });
            textStatusPlan.Location = new Point(793, 219);
            textStatusPlan.Name = "textStatusPlan";
            textStatusPlan.Size = new Size(200, 23);
            textStatusPlan.TabIndex = 6;
            // 
            // btnCompleteTraining
            // 
            btnCompleteTraining.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCompleteTraining.Location = new Point(707, 426);
            btnCompleteTraining.Name = "btnCompleteTraining";
            btnCompleteTraining.Size = new Size(80, 23);
            btnCompleteTraining.TabIndex = 9;
            btnCompleteTraining.Text = "Complete";
            btnCompleteTraining.UseVisualStyleBackColor = true;
            btnCompleteTraining.Click += btnCompleteTraining_Click;
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Right;
            label2.AutoSize = true;
            label2.Location = new Point(792, 291);
            label2.Name = "label2";
            label2.Size = new Size(75, 15);
            label2.TabIndex = 50;
            label2.Text = "Participants :";
            // 
            // textNotesPlan
            // 
            textNotesPlan.Anchor = AnchorStyles.Right;
            textNotesPlan.Location = new Point(793, 263);
            textNotesPlan.Name = "textNotesPlan";
            textNotesPlan.Size = new Size(200, 23);
            textNotesPlan.TabIndex = 7;
            // 
            // label3
            // 
            label3.Anchor = AnchorStyles.Right;
            label3.AutoSize = true;
            label3.Location = new Point(793, 245);
            label3.Name = "label3";
            label3.Size = new Size(44, 15);
            label3.TabIndex = 47;
            label3.Text = "Notes :";
            // 
            // clbEmployeesPlan
            // 
            clbEmployeesPlan.Anchor = AnchorStyles.Right;
            clbEmployeesPlan.FormattingEnabled = true;
            clbEmployeesPlan.Location = new Point(793, 309);
            clbEmployeesPlan.Name = "clbEmployeesPlan";
            clbEmployeesPlan.Size = new Size(200, 94);
            clbEmployeesPlan.TabIndex = 8;
            // 
            // label7
            // 
            label7.Anchor = AnchorStyles.Right;
            label7.AutoSize = true;
            label7.Location = new Point(793, 113);
            label7.Name = "label7";
            label7.Size = new Size(57, 15);
            label7.TabIndex = 44;
            label7.Text = "Provider :";
            // 
            // label6
            // 
            label6.Anchor = AnchorStyles.Right;
            label6.AutoSize = true;
            label6.Location = new Point(893, 69);
            label6.Name = "label6";
            label6.Size = new Size(45, 15);
            label6.TabIndex = 43;
            label6.Text = "Hours :";
            // 
            // label5
            // 
            label5.Anchor = AnchorStyles.Right;
            label5.AutoSize = true;
            label5.Location = new Point(793, 69);
            label5.Name = "label5";
            label5.Size = new Size(32, 15);
            label5.TabIndex = 42;
            label5.Text = "Key :";
            // 
            // txtProviderPlan
            // 
            txtProviderPlan.Anchor = AnchorStyles.Right;
            txtProviderPlan.Location = new Point(793, 131);
            txtProviderPlan.Name = "txtProviderPlan";
            txtProviderPlan.Size = new Size(200, 23);
            txtProviderPlan.TabIndex = 4;
            // 
            // txtHrsPlan
            // 
            txtHrsPlan.Anchor = AnchorStyles.Right;
            txtHrsPlan.Location = new Point(893, 87);
            txtHrsPlan.Name = "txtHrsPlan";
            txtHrsPlan.Size = new Size(100, 23);
            txtHrsPlan.TabIndex = 3;
            // 
            // txtKeyPlan
            // 
            txtKeyPlan.Anchor = AnchorStyles.Right;
            txtKeyPlan.FormattingEnabled = true;
            txtKeyPlan.Items.AddRange(new object[] { "T", "R", "P" });
            txtKeyPlan.Location = new Point(793, 87);
            txtKeyPlan.Name = "txtKeyPlan";
            txtKeyPlan.Size = new Size(71, 23);
            txtKeyPlan.TabIndex = 2;
            // 
            // statusTxt
            // 
            statusTxt.Anchor = AnchorStyles.Right;
            statusTxt.AutoSize = true;
            statusTxt.Location = new Point(793, 201);
            statusTxt.Name = "statusTxt";
            statusTxt.Size = new Size(45, 15);
            statusTxt.TabIndex = 36;
            statusTxt.Text = "Status :";
            // 
            // plannedDateTxt
            // 
            plannedDateTxt.Anchor = AnchorStyles.Right;
            plannedDateTxt.AutoSize = true;
            plannedDateTxt.Location = new Point(793, 157);
            plannedDateTxt.Name = "plannedDateTxt";
            plannedDateTxt.Size = new Size(66, 15);
            plannedDateTxt.TabIndex = 35;
            plannedDateTxt.Text = "Issue Date :";
            // 
            // label4
            // 
            label4.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label4.AutoSize = true;
            label4.Location = new Point(793, 9);
            label4.Name = "label4";
            label4.Size = new Size(105, 15);
            label4.TabIndex = 34;
            label4.Text = "Certificate Name : ";
            // 
            // dtpPlannedDate
            // 
            dtpPlannedDate.Anchor = AnchorStyles.Right;
            dtpPlannedDate.Location = new Point(793, 175);
            dtpPlannedDate.Name = "dtpPlannedDate";
            dtpPlannedDate.Size = new Size(200, 23);
            dtpPlannedDate.TabIndex = 5;
            // 
            // txtCertificateNamePlan
            // 
            txtCertificateNamePlan.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtCertificateNamePlan.Location = new Point(793, 27);
            txtCertificateNamePlan.Name = "txtCertificateNamePlan";
            txtCertificateNamePlan.Size = new Size(200, 23);
            txtCertificateNamePlan.TabIndex = 1;
            // 
            // btnDeleteSession
            // 
            btnDeleteSession.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnDeleteSession.Location = new Point(793, 428);
            btnDeleteSession.Name = "btnDeleteSession";
            btnDeleteSession.Size = new Size(50, 23);
            btnDeleteSession.TabIndex = 10;
            btnDeleteSession.Text = "Delete";
            btnDeleteSession.UseVisualStyleBackColor = true;
            btnDeleteSession.Click += btnDeleteSession_Click;
            // 
            // btnEditSession
            // 
            btnEditSession.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnEditSession.Location = new Point(905, 428);
            btnEditSession.Name = "btnEditSession";
            btnEditSession.Size = new Size(47, 23);
            btnEditSession.TabIndex = 11;
            btnEditSession.Text = "Edit";
            btnEditSession.UseVisualStyleBackColor = true;
            btnEditSession.Click += btnEditSession_Click;
            // 
            // btnAddSession
            // 
            btnAddSession.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAddSession.Location = new Point(958, 428);
            btnAddSession.Name = "btnAddSession";
            btnAddSession.Size = new Size(48, 23);
            btnAddSession.TabIndex = 12;
            btnAddSession.Text = "Add";
            btnAddSession.UseVisualStyleBackColor = true;
            btnAddSession.Click += btnAddSession_Click;
            // 
            // dgvPlannedTraining
            // 
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPlannedTraining.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPlannedTraining.Location = new Point(3, 6);
            dgvPlannedTraining.Name = "dgvPlannedTraining";
            dgvPlannedTraining.Size = new Size(783, 397);
            dgvPlannedTraining.TabIndex = 3;
            dgvPlannedTraining.SelectionChanged += dgvPlannedTraining_SelectionChanged;
            // 
            // ManagerDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1019, 483);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "ManagerDashboard";
            Text = "Manager Dashboard";
            Load += ManagerDashboard_Load;
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
            Planning.ResumeLayout(false);
            Planning.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).EndInit();
            ResumeLayout(false);
        }


        #endregion

        private DataGridView dgvEmployees;
        private DataGridView dgvCertificates;
        private TabControl tabControl;
        private TabPage tabEmployees;
        private TabPage tabCertificates;
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
        private TabPage Planning;
        private DataGridView dgvPlannedTraining;
        private Label statusTxt;
        private Label plannedDateTxt;
        private Label label4;
        private DateTimePicker dtpPlannedDate;
        private TextBox txtCertificateNamePlan;
        private Button btnDeleteSession;
        private Button btnEditSession;
        private Button btnAddSession;
        private ComboBox txtKeyPlan;
        private TextBox txtHrsPlan;
        private TextBox txtProviderPlan;
        private Label label5;
        private Label label6;
        private Label label7;
        private Label label9;
        private Label label10;
        private Label label11;
        private Label label12;
        private TextBox textNotesPlan;
        private CheckedListBox clbEmployeesPlan;
        private ComboBox txtKeyCertsTab;
        private TextBox txtHrsCertsTab;
        private TextBox txtProviderCertsTab;
        private Label label3;
        private Label label2;
        private Button btnCompleteTraining;
        private ComboBox textStatusPlan;
        private Label label8;
        private CheckedListBox checkedListBox1;
        private CheckedListBox clbEmployees;
        private TextBox txtSearchGroups;
        private Label label15;
        private CheckedListBox clbGroups;
        private Label lbl_FullName;
        private TextBox txtFullName;
        private Button btnUpload;
    }
}