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
            dataGridView1 = new DataGridView();
            txtCertName = new TextBox();
            dtpIssueDate = new DateTimePicker();
            dtpExpiryDate = new DateTimePicker();
            btnAdd = new Button();
            btnEdit = new Button();
            btnDelete = new Button();
            txtFilePath = new TextBox();
            lbl_Certname = new Label();
            lbl_IssueDate = new Label();
            lbl_ExpiryDate = new Label();
            label1 = new Label();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(8, 7);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(574, 399);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(588, 88);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 1;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(588, 161);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 2;
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(588, 205);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 3;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(713, 415);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(75, 23);
            btnAdd.TabIndex = 4;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // btnEdit
            // 
            btnEdit.Location = new Point(632, 415);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(75, 23);
            btnEdit.TabIndex = 5;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(512, 415);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(75, 23);
            btnDelete.TabIndex = 6;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // txtFilePath
            // 
            txtFilePath.Location = new Point(588, 275);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 7;
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(588, 70);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 12;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(588, 143);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 13;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(588, 187);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 14;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(588, 257);
            label1.Name = "label1";
            label1.Size = new Size(115, 15);
            label1.TabIndex = 16;
            label1.Text = "Certificate File path :";
            // 
            // EmployeeDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(label1);
            Controls.Add(lbl_ExpiryDate);
            Controls.Add(lbl_IssueDate);
            Controls.Add(lbl_Certname);
            Controls.Add(txtFilePath);
            Controls.Add(btnDelete);
            Controls.Add(btnEdit);
            Controls.Add(btnAdd);
            Controls.Add(dtpExpiryDate);
            Controls.Add(dtpIssueDate);
            Controls.Add(txtCertName);
            Controls.Add(dataGridView1);
            Name = "EmployeeDashboard";
            Text = "EmployeeDashboard";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dataGridView1;
        private TextBox txtCertName;
        private DateTimePicker dtpIssueDate;
        private DateTimePicker dtpExpiryDate;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private TextBox txtFilePath;
        private Label lbl_Certname;
        private Label lbl_IssueDate;
        private Label lbl_ExpiryDate;
        private Label label1;
    }
}