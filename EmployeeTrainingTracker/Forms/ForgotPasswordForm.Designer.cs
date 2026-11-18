namespace EmployeeTrainingTracker.Forms
{
    partial class ForgotPasswordForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ForgotPasswordForm));
            emailTxt = new TextBox();
            forgotpasswordTxt = new Label();
            emailLbl = new Label();
            resetCodeLbl = new Label();
            resetCodeTxt = new TextBox();
            newPasswordLbl = new Label();
            newPasswordTxt = new TextBox();
            sendResetCodeBtn = new Button();
            SuspendLayout();
            // 
            // emailTxt
            // 
            emailTxt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            emailTxt.Location = new Point(45, 114);
            emailTxt.Name = "emailTxt";
            emailTxt.Size = new Size(218, 23);
            emailTxt.TabIndex = 0;
            // 
            // forgotpasswordTxt
            // 
            forgotpasswordTxt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            forgotpasswordTxt.AutoSize = true;
            forgotpasswordTxt.Font = new Font("Segoe UI Semibold", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            forgotpasswordTxt.Location = new Point(45, 27);
            forgotpasswordTxt.Name = "forgotpasswordTxt";
            forgotpasswordTxt.Size = new Size(204, 25);
            forgotpasswordTxt.TabIndex = 1;
            forgotpasswordTxt.Text = "Forgot Your Password?";
            // 
            // emailLbl
            // 
            emailLbl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            emailLbl.AutoSize = true;
            emailLbl.Location = new Point(45, 96);
            emailLbl.Name = "emailLbl";
            emailLbl.Size = new Size(98, 15);
            emailLbl.TabIndex = 2;
            emailLbl.Text = "Enter Email here :";
            // 
            // resetCodeLbl
            // 
            resetCodeLbl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            resetCodeLbl.AutoSize = true;
            resetCodeLbl.Location = new Point(45, 150);
            resetCodeLbl.Name = "resetCodeLbl";
            resetCodeLbl.Size = new Size(98, 15);
            resetCodeLbl.TabIndex = 4;
            resetCodeLbl.Text = "Reset Code here :";
            // 
            // resetCodeTxt
            // 
            resetCodeTxt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            resetCodeTxt.Location = new Point(45, 168);
            resetCodeTxt.Name = "resetCodeTxt";
            resetCodeTxt.Size = new Size(218, 23);
            resetCodeTxt.TabIndex = 3;
            // 
            // newPasswordLbl
            // 
            newPasswordLbl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            newPasswordLbl.AutoSize = true;
            newPasswordLbl.Location = new Point(45, 206);
            newPasswordLbl.Name = "newPasswordLbl";
            newPasswordLbl.Size = new Size(116, 15);
            newPasswordLbl.TabIndex = 6;
            newPasswordLbl.Text = "New Password here :";
            // 
            // newPasswordTxt
            // 
            newPasswordTxt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            newPasswordTxt.Location = new Point(45, 224);
            newPasswordTxt.Name = "newPasswordTxt";
            newPasswordTxt.Size = new Size(218, 23);
            newPasswordTxt.TabIndex = 5;
            // 
            // sendResetCodeBtn
            // 
            sendResetCodeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sendResetCodeBtn.Location = new Point(91, 284);
            sendResetCodeBtn.Name = "sendResetCodeBtn";
            sendResetCodeBtn.Size = new Size(109, 23);
            sendResetCodeBtn.TabIndex = 7;
            sendResetCodeBtn.Text = "Send Reset Code";
            sendResetCodeBtn.UseVisualStyleBackColor = true;
            sendResetCodeBtn.Click += sendResetCodeBtn_Click;
            // 
            // ForgotPasswordForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(330, 358);
            Controls.Add(sendResetCodeBtn);
            Controls.Add(newPasswordLbl);
            Controls.Add(newPasswordTxt);
            Controls.Add(resetCodeLbl);
            Controls.Add(resetCodeTxt);
            Controls.Add(emailLbl);
            Controls.Add(forgotpasswordTxt);
            Controls.Add(emailTxt);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "ForgotPasswordForm";
            Text = "ForgotPasswordForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox emailTxt;
        private Label forgotpasswordTxt;
        private Label emailLbl;
        private Label resetCodeLbl;
        private TextBox resetCodeTxt;
        private Label newPasswordLbl;
        private TextBox newPasswordTxt;
        private Button sendResetCodeBtn;
    }
}