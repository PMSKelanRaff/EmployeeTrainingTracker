
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
            label10 = new Label();
            label11 = new Label();
            label12 = new Label();
            txtProviderCertsTab = new TextBox();
            txtHrsCertsTab = new TextBox();
            txtKeyCertsTab = new ComboBox();
            label9 = new Label();
            chkAddToTrainingFolder = new CheckBox();
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
            lbEmployees = new ListBox();
            btnExportCsv = new Button();
            btnGenerateReport = new Button();
            lblDtpEnd = new Label();
            lblDtpStart = new Label();
            dtpEnd = new DateTimePicker();
            dtpStart = new DateTimePicker();
            lblReportType = new Label();
            dgvReportResults = new DataGridView();
            cmbReportType = new ComboBox();
            tabPage1 = new TabPage();
            clbEmployees = new CheckedListBox();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            txtProvider = new TextBox();
            txtHrs = new TextBox();
            txtKey = new ComboBox();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            dtpPExpiryDate = new DateTimePicker();
            dtpPlannedDate = new DateTimePicker();
            txtCertificateName = new TextBox();
            button1 = new Button();
            button2 = new Button();
            button3 = new Button();
            dgvPlannedTraining = new DataGridView();
            ((System.ComponentModel.ISupportInitialize)dgvEmployees).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvCertificates).BeginInit();
            tabControl.SuspendLayout();
            tabEmployees.SuspendLayout();
            tabCertificates.SuspendLayout();
            Reports.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvReportResults).BeginInit();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).BeginInit();
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
            tabControl.Controls.Add(tabPage1);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(875, 450);
            tabControl.TabIndex = 2;
            // 
            // tabEmployees
            // 
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
            tabCertificates.Controls.Add(label10);
            tabCertificates.Controls.Add(label11);
            tabCertificates.Controls.Add(label12);
            tabCertificates.Controls.Add(txtProviderCertsTab);
            tabCertificates.Controls.Add(txtHrsCertsTab);
            tabCertificates.Controls.Add(txtKeyCertsTab);
            tabCertificates.Controls.Add(label9);
            tabCertificates.Controls.Add(chkAddToTrainingFolder);
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
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(648, 159);
            label10.Name = "label10";
            label10.Size = new Size(57, 15);
            label10.TabIndex = 50;
            label10.Text = "Provider :";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(748, 110);
            label11.Name = "label11";
            label11.Size = new Size(45, 15);
            label11.TabIndex = 49;
            label11.Text = "Hours :";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new Point(648, 110);
            label12.Name = "label12";
            label12.Size = new Size(32, 15);
            label12.TabIndex = 48;
            label12.Text = "Key :";
            // 
            // txtProviderCertsTab
            // 
            txtProviderCertsTab.Location = new Point(648, 177);
            txtProviderCertsTab.Name = "txtProviderCertsTab";
            txtProviderCertsTab.Size = new Size(200, 23);
            txtProviderCertsTab.TabIndex = 47;
            // 
            // txtHrsCertsTab
            // 
            txtHrsCertsTab.Location = new Point(748, 128);
            txtHrsCertsTab.Name = "txtHrsCertsTab";
            txtHrsCertsTab.Size = new Size(100, 23);
            txtHrsCertsTab.TabIndex = 46;
            // 
            // txtKeyCertsTab
            // 
            txtKeyCertsTab.FormattingEnabled = true;
            txtKeyCertsTab.Items.AddRange(new object[] { "T", "R", "P" });
            txtKeyCertsTab.Location = new Point(648, 128);
            txtKeyCertsTab.Name = "txtKeyCertsTab";
            txtKeyCertsTab.Size = new Size(71, 23);
            txtKeyCertsTab.TabIndex = 45;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(648, 322);
            label9.Name = "label9";
            label9.Size = new Size(58, 15);
            label9.TabIndex = 28;
            label9.Text = "File Path :";
            // 
            // chkAddToTrainingFolder
            // 
            chkAddToTrainingFolder.AutoSize = true;
            chkAddToTrainingFolder.Checked = true;
            chkAddToTrainingFolder.CheckState = CheckState.Checked;
            chkAddToTrainingFolder.Location = new Point(648, 369);
            chkAddToTrainingFolder.Name = "chkAddToTrainingFolder";
            chkAddToTrainingFolder.Size = new Size(140, 19);
            chkAddToTrainingFolder.TabIndex = 27;
            chkAddToTrainingFolder.Text = "Add to training folder";
            chkAddToTrainingFolder.UseVisualStyleBackColor = true;
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
            txtFilePath.Location = new Point(648, 340);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 14;
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(648, 267);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 13;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(648, 213);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 12;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(648, 62);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 11;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(648, 285);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 10;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(648, 231);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 9;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(648, 80);
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
            Reports.Controls.Add(lbEmployees);
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
            // lbEmployees
            // 
            lbEmployees.FormattingEnabled = true;
            lbEmployees.ItemHeight = 15;
            lbEmployees.Location = new Point(649, 265);
            lbEmployees.Name = "lbEmployees";
            lbEmployees.SelectionMode = SelectionMode.MultiExtended;
            lbEmployees.Size = new Size(200, 94);
            lbEmployees.TabIndex = 10;
            // 
            // btnExportCsv
            // 
            btnExportCsv.Location = new Point(774, 393);
            btnExportCsv.Name = "btnExportCsv";
            btnExportCsv.Size = new Size(90, 23);
            btnExportCsv.TabIndex = 9;
            btnExportCsv.Text = "Export CSV";
            btnExportCsv.UseVisualStyleBackColor = true;
            btnExportCsv.Click += btnExportCsv_Click;
            // 
            // btnGenerateReport
            // 
            btnGenerateReport.Location = new Point(774, 365);
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
            lblDtpEnd.Location = new Point(649, 120);
            lblDtpEnd.Name = "lblDtpEnd";
            lblDtpEnd.Size = new Size(60, 15);
            lblDtpEnd.TabIndex = 7;
            lblDtpEnd.Text = "End Date :";
            // 
            // lblDtpStart
            // 
            lblDtpStart.AutoSize = true;
            lblDtpStart.Location = new Point(649, 67);
            lblDtpStart.Name = "lblDtpStart";
            lblDtpStart.Size = new Size(64, 15);
            lblDtpStart.TabIndex = 6;
            lblDtpStart.Text = "Start Date :";
            // 
            // dtpEnd
            // 
            dtpEnd.Location = new Point(649, 138);
            dtpEnd.Name = "dtpEnd";
            dtpEnd.Size = new Size(200, 23);
            dtpEnd.TabIndex = 5;
            // 
            // dtpStart
            // 
            dtpStart.Location = new Point(649, 85);
            dtpStart.Name = "dtpStart";
            dtpStart.Size = new Size(200, 23);
            dtpStart.TabIndex = 4;
            // 
            // lblReportType
            // 
            lblReportType.AutoSize = true;
            lblReportType.Location = new Point(649, 6);
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
            cmbReportType.Location = new Point(649, 24);
            cmbReportType.Name = "cmbReportType";
            cmbReportType.Size = new Size(121, 23);
            cmbReportType.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(clbEmployees);
            tabPage1.Controls.Add(label7);
            tabPage1.Controls.Add(label6);
            tabPage1.Controls.Add(label5);
            tabPage1.Controls.Add(txtProvider);
            tabPage1.Controls.Add(txtHrs);
            tabPage1.Controls.Add(txtKey);
            tabPage1.Controls.Add(label2);
            tabPage1.Controls.Add(label3);
            tabPage1.Controls.Add(label4);
            tabPage1.Controls.Add(dtpPExpiryDate);
            tabPage1.Controls.Add(dtpPlannedDate);
            tabPage1.Controls.Add(txtCertificateName);
            tabPage1.Controls.Add(button1);
            tabPage1.Controls.Add(button2);
            tabPage1.Controls.Add(button3);
            tabPage1.Controls.Add(dgvPlannedTraining);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(867, 422);
            tabPage1.TabIndex = 3;
            tabPage1.Text = "Planning";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // clbEmployees
            // 
            clbEmployees.FormattingEnabled = true;
            clbEmployees.Location = new Point(649, 293);
            clbEmployees.Name = "clbEmployees";
            clbEmployees.Size = new Size(200, 94);
            clbEmployees.TabIndex = 46;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(649, 108);
            label7.Name = "label7";
            label7.Size = new Size(57, 15);
            label7.TabIndex = 44;
            label7.Text = "Provider :";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(749, 59);
            label6.Name = "label6";
            label6.Size = new Size(45, 15);
            label6.TabIndex = 43;
            label6.Text = "Hours :";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(649, 59);
            label5.Name = "label5";
            label5.Size = new Size(32, 15);
            label5.TabIndex = 42;
            label5.Text = "Key :";
            // 
            // txtProvider
            // 
            txtProvider.Location = new Point(649, 126);
            txtProvider.Name = "txtProvider";
            txtProvider.Size = new Size(200, 23);
            txtProvider.TabIndex = 41;
            // 
            // txtHrs
            // 
            txtHrs.Location = new Point(749, 77);
            txtHrs.Name = "txtHrs";
            txtHrs.Size = new Size(100, 23);
            txtHrs.TabIndex = 40;
            // 
            // txtKey
            // 
            txtKey.FormattingEnabled = true;
            txtKey.Items.AddRange(new object[] { "T", "R", "P" });
            txtKey.Location = new Point(649, 77);
            txtKey.Name = "txtKey";
            txtKey.Size = new Size(71, 23);
            txtKey.TabIndex = 39;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(649, 212);
            label2.Name = "label2";
            label2.Size = new Size(71, 15);
            label2.TabIndex = 36;
            label2.Text = "Expiry Date :";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(649, 158);
            label3.Name = "label3";
            label3.Size = new Size(66, 15);
            label3.TabIndex = 35;
            label3.Text = "Issue Date :";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(649, 9);
            label4.Name = "label4";
            label4.Size = new Size(105, 15);
            label4.TabIndex = 34;
            label4.Text = "Certificate Name : ";
            // 
            // dtpPExpiryDate
            // 
            dtpPExpiryDate.Location = new Point(649, 230);
            dtpPExpiryDate.Name = "dtpPExpiryDate";
            dtpPExpiryDate.Size = new Size(200, 23);
            dtpPExpiryDate.TabIndex = 33;
            // 
            // dtpPlannedDate
            // 
            dtpPlannedDate.Location = new Point(649, 176);
            dtpPlannedDate.Name = "dtpPlannedDate";
            dtpPlannedDate.Size = new Size(200, 23);
            dtpPlannedDate.TabIndex = 32;
            // 
            // txtCertificateName
            // 
            txtCertificateName.Location = new Point(649, 27);
            txtCertificateName.Name = "txtCertificateName";
            txtCertificateName.Size = new Size(200, 23);
            txtCertificateName.TabIndex = 31;
            // 
            // button1
            // 
            button1.Location = new Point(649, 395);
            button1.Name = "button1";
            button1.Size = new Size(50, 23);
            button1.TabIndex = 30;
            button1.Text = "Delete";
            button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            button2.Location = new Point(761, 395);
            button2.Name = "button2";
            button2.Size = new Size(47, 23);
            button2.TabIndex = 29;
            button2.Text = "Edit";
            button2.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            button3.Location = new Point(814, 395);
            button3.Name = "button3";
            button3.Size = new Size(48, 23);
            button3.TabIndex = 28;
            button3.Text = "Add";
            button3.UseVisualStyleBackColor = true;
            // 
            // dgvPlannedTraining
            // 
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPlannedTraining.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPlannedTraining.Location = new Point(3, 6);
            dgvPlannedTraining.Name = "dgvPlannedTraining";
            dgvPlannedTraining.Size = new Size(640, 413);
            dgvPlannedTraining.TabIndex = 3;
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
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).EndInit();
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
        private ListBox lbEmployees;
        private TabPage tabPage1;
        private DataGridView dgvPlannedTraining;
        private Label label2;
        private Label label3;
        private Label label4;
        private DateTimePicker dtpPExpiryDate;
        private DateTimePicker dtpPlannedDate;
        private TextBox txtCertificateName;
        private Button button1;
        private Button button2;
        private Button button3;
        private ComboBox txtKey;
        private TextBox txtHrs;
        private TextBox txtProvider;
        private Label label5;
        private Label label6;
        private Label label7;
        private Label label9;
        private Label label10;
        private Label label11;
        private Label label12;
        private TextBox textBox2;
        private TextBox textBox3;
        private ComboBox comboBox1;
        private CheckedListBox clbEmployees;
        private ComboBox txtKeyCertsTab;
        private TextBox txtHrsCertsTab;
        private TextBox txtProviderCertsTab;
    }
}