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
            SuspendLayout();
            // 
            // txtUsername
            // 
            txtUsername.Location = new Point(12, 107);
            txtUsername.Name = "txtUsername";
            txtUsername.Size = new Size(191, 23);
            txtUsername.TabIndex = 0;
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(12, 151);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(191, 23);
            txtPassword.TabIndex = 1;
            // 
            // lbl_Username
            // 
            lbl_Username.AutoSize = true;
            lbl_Username.Location = new Point(15, 89);
            lbl_Username.Name = "lbl_Username";
            lbl_Username.Size = new Size(71, 15);
            lbl_Username.TabIndex = 2;
            lbl_Username.Text = "User Name :";
            // 
            // lbl_Password
            // 
            lbl_Password.AutoSize = true;
            lbl_Password.Location = new Point(12, 133);
            lbl_Password.Name = "lbl_Password";
            lbl_Password.Size = new Size(63, 15);
            lbl_Password.TabIndex = 3;
            lbl_Password.Text = "Password :";
            // 
            // Login_btn
            // 
            Login_btn.Location = new Point(145, 192);
            Login_btn.Name = "Login_btn";
            Login_btn.Size = new Size(58, 23);
            Login_btn.TabIndex = 4;
            Login_btn.Text = "Log In";
            Login_btn.UseVisualStyleBackColor = true;
            Login_btn.Click += Login_btn_Click;
            // 
            // WindowsLogin_btn
            // 
            WindowsLogin_btn.Location = new Point(11, 192);
            WindowsLogin_btn.Name = "WindowsLogin_btn";
            WindowsLogin_btn.Size = new Size(108, 23);
            WindowsLogin_btn.TabIndex = 5;
            WindowsLogin_btn.Text = "Windows Log In";
            WindowsLogin_btn.UseVisualStyleBackColor = true;
            WindowsLogin_btn.Click += WindowsLogin_btn_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI Black", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.Location = new Point(25, 35);
            label1.Name = "label1";
            label1.Size = new Size(178, 21);
            label1.TabIndex = 6;
            label1.Text = "PMS Training Tracker";
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(229, 227);
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
    }
}