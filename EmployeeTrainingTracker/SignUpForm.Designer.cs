namespace EmployeeTrainingTracker
{
    partial class SignUpForm
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
            btnCreate = new Button();
            txtUsername = new TextBox();
            Lbl_Username = new Label();
            Lbl_Password = new Label();
            Txt_Title = new Label();
            txtPassword = new TextBox();
            Lbl_FullName = new Label();
            Lbl_JobTitle = new Label();
            Lbl_Department = new Label();
            txtDepartment = new TextBox();
            txtFullName = new TextBox();
            txtJobTitle = new TextBox();
            chkLinkWindows = new CheckBox();
            SuspendLayout();
            // 
            // btnCreate
            // 
            btnCreate.Location = new Point(143, 301);
            btnCreate.Name = "btnCreate";
            btnCreate.Size = new Size(102, 23);
            btnCreate.TabIndex = 0;
            btnCreate.Text = "Create Account";
            btnCreate.UseVisualStyleBackColor = true;
            btnCreate.Click += btnCreate_Click;
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(84, 54);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(131, 23);
            txtUsername.TabIndex = 1;
            // 
            // Lbl_Username
            // 
            Lbl_Username.AutoSize = true;
            Lbl_Username.Location = new Point(9, 57);
            Lbl_Username.Name = "Lbl_Username";
            Lbl_Username.Size = new Size(42, 15);
            Lbl_Username.TabIndex = 3;
            Lbl_Username.Text = "Email :";
            // 
            // Lbl_Password
            // 
            Lbl_Password.AutoSize = true;
            Lbl_Password.Location = new Point(9, 100);
            Lbl_Password.Name = "Lbl_Password";
            Lbl_Password.Size = new Size(63, 15);
            Lbl_Password.TabIndex = 4;
            Lbl_Password.Text = "Password :";
            // 
            // Txt_Title
            // 
            Txt_Title.AutoSize = true;
            Txt_Title.Font = new Font("Segoe UI Symbol", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            Txt_Title.Location = new Point(84, 18);
            Txt_Title.Name = "Txt_Title";
            Txt_Title.Size = new Size(72, 21);
            Txt_Title.TabIndex = 5;
            Txt_Title.Text = "Sign Up";
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(84, 97);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(131, 23);
            txtPassword.TabIndex = 6;
            // 
            // Lbl_FullName
            // 
            Lbl_FullName.AutoSize = true;
            Lbl_FullName.Location = new Point(9, 143);
            Lbl_FullName.Name = "Lbl_FullName";
            Lbl_FullName.Size = new Size(67, 15);
            Lbl_FullName.TabIndex = 7;
            Lbl_FullName.Text = "Full Name :";
            // 
            // Lbl_JobTitle
            // 
            Lbl_JobTitle.AutoSize = true;
            Lbl_JobTitle.Location = new Point(9, 230);
            Lbl_JobTitle.Name = "Lbl_JobTitle";
            Lbl_JobTitle.Size = new Size(57, 15);
            Lbl_JobTitle.TabIndex = 8;
            Lbl_JobTitle.Text = "Job Title :";
            // 
            // Lbl_Department
            // 
            Lbl_Department.AutoSize = true;
            Lbl_Department.Location = new Point(9, 187);
            Lbl_Department.Name = "Lbl_Department";
            Lbl_Department.Size = new Size(76, 15);
            Lbl_Department.TabIndex = 9;
            Lbl_Department.Text = "Department :";
            // 
            // txtDepartment
            // 
            txtDepartment.Location = new Point(84, 184);
            txtDepartment.Name = "txtDepartment";
            txtDepartment.Size = new Size(131, 23);
            txtDepartment.TabIndex = 10;
            // 
            // txtFullName
            // 
            txtFullName.Location = new Point(84, 140);
            txtFullName.Name = "txtFullName";
            txtFullName.Size = new Size(131, 23);
            txtFullName.TabIndex = 11;
            // 
            // txtJobTitle
            // 
            txtJobTitle.Location = new Point(84, 227);
            txtJobTitle.Name = "txtJobTitle";
            txtJobTitle.Size = new Size(131, 23);
            txtJobTitle.TabIndex = 12;
            // 
            // chkLinkWindows
            // 
            chkLinkWindows.AutoSize = true;
            chkLinkWindows.Location = new Point(12, 266);
            chkLinkWindows.Name = "chkLinkWindows";
            chkLinkWindows.Size = new Size(188, 19);
            chkLinkWindows.TabIndex = 13;
            chkLinkWindows.Text = "Store details for windows login";
            chkLinkWindows.UseVisualStyleBackColor = true;
            // 
            // SignUpForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(268, 348);
            Controls.Add(chkLinkWindows);
            Controls.Add(txtJobTitle);
            Controls.Add(txtFullName);
            Controls.Add(txtDepartment);
            Controls.Add(Lbl_Department);
            Controls.Add(Lbl_JobTitle);
            Controls.Add(Lbl_FullName);
            Controls.Add(txtPassword);
            Controls.Add(Txt_Title);
            Controls.Add(Lbl_Password);
            Controls.Add(Lbl_Username);
            Controls.Add(txtUsername);
            Controls.Add(btnCreate);
            Name = "SignUpForm";
            Text = "SignUpForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnCreate;
        private TextBox txtUsername;
        private Label Lbl_Username;
        private Label Lbl_Password;
        private Label Txt_Title;
        private TextBox txtPassword;
        private Label Lbl_FullName;
        private Label Lbl_JobTitle;
        private Label Lbl_Department;
        private TextBox txtDepartment;
        private TextBox txtFullName;
        private TextBox txtJobTitle;
        private CheckBox chkLinkWindows;
    }
}