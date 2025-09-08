
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
            ((System.ComponentModel.ISupportInitialize)dgvEmployees).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvCertificates).BeginInit();
            tabControl.SuspendLayout();
            tabEmployees.SuspendLayout();
            tabCertificates.SuspendLayout();
            SuspendLayout();
            // 
            // dgvEmployees
            // 
            dgvEmployees.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvEmployees.Location = new Point(6, 6);
            dgvEmployees.Name = "dgvEmployees";
            dgvEmployees.Size = new Size(544, 379);
            dgvEmployees.TabIndex = 0;
            dgvEmployees.CellContentClick += dgvCertificates_CellContentClick;
            dgvEmployees.SelectionChanged += dgvEmployees_SelectionChanged;
            // 
            // dgvCertificates
            // 
            dgvCertificates.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvCertificates.Location = new Point(6, 6);
            dgvCertificates.Name = "dgvCertificates";
            dgvCertificates.Size = new Size(556, 406);
            dgvCertificates.TabIndex = 1;
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabEmployees);
            tabControl.Controls.Add(tabCertificates);
            tabControl.Dock = DockStyle.Fill;
            tabControl.Location = new Point(0, 0);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(800, 450);
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
            tabEmployees.Size = new Size(792, 422);
            tabEmployees.TabIndex = 0;
            tabEmployees.Text = "Employees";
            tabEmployees.UseVisualStyleBackColor = true;
            // 
            // lbl_JobTitle
            // 
            lbl_JobTitle.AutoSize = true;
            lbl_JobTitle.Location = new Point(556, 277);
            lbl_JobTitle.Name = "lbl_JobTitle";
            lbl_JobTitle.Size = new Size(57, 15);
            lbl_JobTitle.TabIndex = 22;
            lbl_JobTitle.Text = "Job Title :";
            // 
            // txtJobTitle
            // 
            txtJobTitle.Location = new Point(556, 295);
            txtJobTitle.Name = "txtJobTitle";
            txtJobTitle.Size = new Size(228, 23);
            txtJobTitle.TabIndex = 21;
            // 
            // cmbDept
            // 
            cmbDept.FormattingEnabled = true;
            cmbDept.Items.AddRange(new object[] { "Administration", "Engineering", "IT", "Site" });
            cmbDept.Location = new Point(556, 250);
            cmbDept.Name = "cmbDept";
            cmbDept.Size = new Size(228, 23);
            cmbDept.TabIndex = 20;
            // 
            // lbl_Department
            // 
            lbl_Department.AutoSize = true;
            lbl_Department.Location = new Point(556, 232);
            lbl_Department.Name = "lbl_Department";
            lbl_Department.Size = new Size(76, 15);
            lbl_Department.TabIndex = 19;
            lbl_Department.Text = "Department :";
            // 
            // btnDeleteEmployee
            // 
            btnDeleteEmployee.Location = new Point(505, 391);
            btnDeleteEmployee.Name = "btnDeleteEmployee";
            btnDeleteEmployee.Size = new Size(75, 23);
            btnDeleteEmployee.TabIndex = 17;
            btnDeleteEmployee.Text = "Delete";
            btnDeleteEmployee.UseVisualStyleBackColor = true;
            btnDeleteEmployee.Click += btnDeleteEmployee_Click;
            // 
            // btnEditEmployee
            // 
            btnEditEmployee.Location = new Point(628, 391);
            btnEditEmployee.Name = "btnEditEmployee";
            btnEditEmployee.Size = new Size(75, 23);
            btnEditEmployee.TabIndex = 16;
            btnEditEmployee.Text = "Edit";
            btnEditEmployee.UseVisualStyleBackColor = true;
            btnEditEmployee.Click += btnUpdateEmployee_Click;
            // 
            // btnAddEmployee
            // 
            btnAddEmployee.Location = new Point(709, 393);
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
            lbl_Type.Location = new Point(556, 188);
            lbl_Type.Name = "lbl_Type";
            lbl_Type.Size = new Size(38, 15);
            lbl_Type.TabIndex = 14;
            lbl_Type.Text = "Type :";
            // 
            // lbl_Password
            // 
            lbl_Password.AutoSize = true;
            lbl_Password.Location = new Point(556, 124);
            lbl_Password.Name = "lbl_Password";
            lbl_Password.Size = new Size(63, 15);
            lbl_Password.TabIndex = 13;
            lbl_Password.Text = "Password :";
            // 
            // lbl_Username
            // 
            lbl_Username.AutoSize = true;
            lbl_Username.Location = new Point(556, 80);
            lbl_Username.Name = "lbl_Username";
            lbl_Username.Size = new Size(66, 15);
            lbl_Username.TabIndex = 12;
            lbl_Username.Text = "Username :";
            // 
            // cmbRole
            // 
            cmbRole.FormattingEnabled = true;
            cmbRole.Items.AddRange(new object[] { "Employee", "Admin" });
            cmbRole.Location = new Point(556, 206);
            cmbRole.Name = "cmbRole";
            cmbRole.Size = new Size(228, 23);
            cmbRole.TabIndex = 11;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(556, 142);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(228, 23);
            txtPassword.TabIndex = 10;
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(556, 98);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(228, 23);
            txtUsername.TabIndex = 9;
            // 
            // tabCertificates
            // 
            tabCertificates.Controls.Add(label1);
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
            tabCertificates.Size = new Size(792, 422);
            tabCertificates.TabIndex = 1;
            tabCertificates.Text = "Certificates";
            tabCertificates.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(568, 237);
            label1.Name = "label1";
            label1.Size = new Size(115, 15);
            label1.TabIndex = 15;
            label1.Text = "Certificate File path :";
            // 
            // txtFilePath
            // 
            txtFilePath.Location = new Point(568, 255);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 14;
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(568, 165);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 13;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(568, 111);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 12;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(568, 43);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 11;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(568, 183);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 10;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(568, 129);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 9;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(568, 61);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 8;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(568, 389);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(50, 23);
            btnDelete.TabIndex = 7;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDeleteCert_Click;
            // 
            // btnEdit
            // 
            btnEdit.Location = new Point(680, 389);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(47, 23);
            btnEdit.TabIndex = 6;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEditCert_Click;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(733, 389);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(48, 23);
            btnAdd.TabIndex = 5;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAddCert_Click;
            // 
            // AdminDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
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
        private Label label1;
        private Label lbl_Department;
        private ComboBox cmbDept;
        private Label lbl_JobTitle;
        private TextBox txtJobTitle;
    }
}