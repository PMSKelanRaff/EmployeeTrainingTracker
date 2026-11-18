namespace EmployeeTrainingTracker
{
    partial class LoginForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginForm));
            txtUsername = new TextBox();
            txtPassword = new TextBox();
            lbl_Username = new Label();
            lbl_Password = new Label();
            Login_btn = new Button();
            WindowsLogin_btn = new Button();
            label1 = new Label();
            SignUp_btn = new Button();
            login_hidePasswordCheckBox = new CheckBox();
            forgotPasswordLbl = new LinkLabel();
            SuspendLayout();
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(12, 95);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(191, 23);
            txtUsername.TabIndex = 1;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(12, 139);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(191, 23);
            txtPassword.TabIndex = 2;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // lbl_Username
            // 
            lbl_Username.AutoSize = true;
            lbl_Username.Location = new Point(12, 77);
            lbl_Username.Name = "lbl_Username";
            lbl_Username.Size = new Size(42, 15);
            lbl_Username.TabIndex = 2;
            lbl_Username.Text = "Email :";
            // 
            // lbl_Password
            // 
            lbl_Password.AutoSize = true;
            lbl_Password.Location = new Point(12, 121);
            lbl_Password.Name = "lbl_Password";
            lbl_Password.Size = new Size(63, 15);
            lbl_Password.TabIndex = 3;
            lbl_Password.Text = "Password :";
            // 
            // Login_btn
            // 
            Login_btn.Location = new Point(11, 232);
            Login_btn.Name = "Login_btn";
            Login_btn.Size = new Size(58, 23);
            Login_btn.TabIndex = 4;
            Login_btn.Text = "Log In";
            Login_btn.UseVisualStyleBackColor = true;
            Login_btn.Click += Login_btn_Click;
            // 
            // WindowsLogin_btn
            // 
            WindowsLogin_btn.Location = new Point(15, 203);
            WindowsLogin_btn.Name = "WindowsLogin_btn";
            WindowsLogin_btn.Size = new Size(108, 23);
            WindowsLogin_btn.TabIndex = 3;
            WindowsLogin_btn.Text = "Windows Log In";
            WindowsLogin_btn.UseVisualStyleBackColor = true;
            WindowsLogin_btn.Click += WindowsLogin_btn_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI Black", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.Location = new Point(25, 26);
            label1.Name = "label1";
            label1.Size = new Size(178, 21);
            label1.TabIndex = 6;
            label1.Text = "PMS Training Tracker";
            // 
            // SignUp_btn
            // 
            SignUp_btn.Location = new Point(75, 232);
            SignUp_btn.Name = "SignUp_btn";
            SignUp_btn.Size = new Size(58, 23);
            SignUp_btn.TabIndex = 5;
            SignUp_btn.Text = "Sign Up";
            SignUp_btn.UseVisualStyleBackColor = true;
            SignUp_btn.Click += SignUp_btn_Click;
            // 
            // login_hidePasswordCheckBox
            // 
            login_hidePasswordCheckBox.AutoSize = true;
            login_hidePasswordCheckBox.Checked = true;
            login_hidePasswordCheckBox.CheckState = CheckState.Checked;
            login_hidePasswordCheckBox.Location = new Point(15, 167);
            login_hidePasswordCheckBox.Name = "login_hidePasswordCheckBox";
            login_hidePasswordCheckBox.Size = new Size(104, 19);
            login_hidePasswordCheckBox.TabIndex = 7;
            login_hidePasswordCheckBox.Text = "Hide Password";
            login_hidePasswordCheckBox.UseVisualStyleBackColor = true;
            login_hidePasswordCheckBox.CheckedChanged += login_hidePasswordCheckBox_CheckedChanged;
            // 
            // forgotPasswordLbl
            // 
            forgotPasswordLbl.AutoSize = true;
            forgotPasswordLbl.Location = new Point(117, 262);
            forgotPasswordLbl.Name = "forgotPasswordLbl";
            forgotPasswordLbl.Size = new Size(100, 15);
            forgotPasswordLbl.TabIndex = 8;
            forgotPasswordLbl.TabStop = true;
            forgotPasswordLbl.Text = "Forgot Password?";
            forgotPasswordLbl.LinkClicked += forgotPasswordLbl_LinkClicked;
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(229, 286);
            Controls.Add(forgotPasswordLbl);
            Controls.Add(login_hidePasswordCheckBox);
            Controls.Add(SignUp_btn);
            Controls.Add(label1);
            Controls.Add(WindowsLogin_btn);
            Controls.Add(Login_btn);
            Controls.Add(lbl_Password);
            Controls.Add(lbl_Username);
            Controls.Add(txtPassword);
            Controls.Add(txtUsername);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "LoginForm";
            Text = "Login Form";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtUsername;
        private TextBox txtPassword;
        private Label lbl_Username;
        private Label lbl_Password;
        private Button Login_btn;
        private Button WindowsLogin_btn;
        private Label label1;
        private Button SignUp_btn;
        private CheckBox login_hidePasswordCheckBox;
        private LinkLabel forgotPasswordLbl;
    }
}