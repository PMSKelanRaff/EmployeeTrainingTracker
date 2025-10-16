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
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            txtProvider = new TextBox();
            txtHrs = new TextBox();
            txtKey = new ComboBox();
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
            dataGridView1.SelectionChanged += dataGridView1_SelectionChanged;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(588, 43);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 1;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(588, 213);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 2;
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(588, 270);
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
            txtFilePath.Location = new Point(588, 340);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new Size(200, 23);
            txtFilePath.TabIndex = 7;
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(588, 25);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 12;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(588, 195);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 13;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(588, 252);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 14;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(588, 322);
            label1.Name = "label1";
            label1.Size = new Size(115, 15);
            label1.TabIndex = 16;
            label1.Text = "Certificate File path :";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(588, 132);
            label7.Name = "label7";
            label7.Size = new Size(57, 15);
            label7.TabIndex = 50;
            label7.Text = "Provider :";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(688, 83);
            label6.Name = "label6";
            label6.Size = new Size(45, 15);
            label6.TabIndex = 49;
            label6.Text = "Hours :";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(588, 83);
            label5.Name = "label5";
            label5.Size = new Size(32, 15);
            label5.TabIndex = 48;
            label5.Text = "Key :";
            // 
            // txtProvider
            // 
            txtProvider.Location = new Point(588, 150);
            txtProvider.Name = "txtProvider";
            txtProvider.Size = new Size(200, 23);
            txtProvider.TabIndex = 47;
            // 
            // txtHrs
            // 
            txtHrs.Location = new Point(688, 101);
            txtHrs.Name = "txtHrs";
            txtHrs.Size = new Size(100, 23);
            txtHrs.TabIndex = 46;
            // 
            // txtKey
            // 
            txtKey.FormattingEnabled = true;
            txtKey.Items.AddRange(new object[] { "T", "R", "P" });
            txtKey.Location = new Point(588, 101);
            txtKey.Name = "txtKey";
            txtKey.Size = new Size(71, 23);
            txtKey.TabIndex = 45;
            // 
            // EmployeeDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(label7);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(txtProvider);
            Controls.Add(txtHrs);
            Controls.Add(txtKey);
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
        private Label label7;
        private Label label6;
        private Label label5;
        private TextBox txtProvider;
        private TextBox txtHrs;
        private ComboBox txtKey;
    }
}