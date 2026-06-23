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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EmployeeDashboard));
            tabControl = new TabControl();
            tabCertificates = new TabPage();
            btnRequestDelete = new Button();
            btnUpload = new Button();
            label7 = new Label();
            label6 = new Label();
            label5 = new Label();
            txtProvider = new TextBox();
            txtHrs = new TextBox();
            txtKey = new ComboBox();
            lbl_ExpiryDate = new Label();
            lbl_IssueDate = new Label();
            lbl_Certname = new Label();
            btnEdit = new Button();
            dtpExpiryDate = new DateTimePicker();
            dtpIssueDate = new DateTimePicker();
            txtCertName = new TextBox();
            dataGridView1 = new DataGridView();
            tabTraining = new TabPage();
            dgvPlannedTraining = new DataGridView();
            tabAcknowledgements = new TabPage();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            textBox1 = new TextBox();
            txtAckHours = new TextBox();
            comboBox1 = new ComboBox();
            label8 = new Label();
            label9 = new Label();
            dtpAckDate = new DateTimePicker();
            txtTopic = new TextBox();
            dgvAcknowledgements = new DataGridView();
            tabControl.SuspendLayout();
            tabCertificates.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tabTraining.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).BeginInit();
            tabAcknowledgements.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvAcknowledgements).BeginInit();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Controls.Add(tabCertificates);
            tabControl.Controls.Add(tabTraining);
            tabControl.Controls.Add(tabAcknowledgements);
            tabControl.Location = new Point(3, -1);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1019, 473);
            tabControl.TabIndex = 0;
            // 
            // tabCertificates
            // 
            tabCertificates.Controls.Add(btnRequestDelete);
            tabCertificates.Controls.Add(btnUpload);
            tabCertificates.Controls.Add(label7);
            tabCertificates.Controls.Add(label6);
            tabCertificates.Controls.Add(label5);
            tabCertificates.Controls.Add(txtProvider);
            tabCertificates.Controls.Add(txtHrs);
            tabCertificates.Controls.Add(txtKey);
            tabCertificates.Controls.Add(lbl_ExpiryDate);
            tabCertificates.Controls.Add(lbl_IssueDate);
            tabCertificates.Controls.Add(lbl_Certname);
            tabCertificates.Controls.Add(btnEdit);
            tabCertificates.Controls.Add(dtpExpiryDate);
            tabCertificates.Controls.Add(dtpIssueDate);
            tabCertificates.Controls.Add(txtCertName);
            tabCertificates.Controls.Add(dataGridView1);
            tabCertificates.Location = new Point(4, 24);
            tabCertificates.Name = "tabCertificates";
            tabCertificates.Padding = new Padding(3);
            tabCertificates.Size = new Size(1011, 445);
            tabCertificates.TabIndex = 0;
            tabCertificates.Text = "Certificates";
            tabCertificates.UseVisualStyleBackColor = true;
            // 
            // btnRequestDelete
            // 
            btnRequestDelete.Location = new Point(812, 416);
            btnRequestDelete.Name = "btnRequestDelete";
            btnRequestDelete.Size = new Size(104, 23);
            btnRequestDelete.TabIndex = 71;
            btnRequestDelete.Text = "Request Deletion";
            btnRequestDelete.UseVisualStyleBackColor = true;
            btnRequestDelete.Click += btnRequestDelete_Click;
            // 
            // btnUpload
            // 
            btnUpload.Location = new Point(922, 287);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(75, 23);
            btnUpload.TabIndex = 70;
            btnUpload.Text = "Upload";
            btnUpload.UseVisualStyleBackColor = true;
            btnUpload.Click += btnUpload_Click;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(797, 111);
            label7.Name = "label7";
            label7.Size = new Size(57, 15);
            label7.TabIndex = 69;
            label7.Text = "Provider :";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(897, 62);
            label6.Name = "label6";
            label6.Size = new Size(71, 15);
            label6.TabIndex = 68;
            label6.Text = "CPD Hours :";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(797, 62);
            label5.Name = "label5";
            label5.Size = new Size(32, 15);
            label5.TabIndex = 67;
            label5.Text = "Key :";
            // 
            // txtProvider
            // 
            txtProvider.Location = new Point(797, 129);
            txtProvider.Name = "txtProvider";
            txtProvider.Size = new Size(200, 23);
            txtProvider.TabIndex = 4;
            // 
            // txtHrs
            // 
            txtHrs.Location = new Point(897, 80);
            txtHrs.Name = "txtHrs";
            txtHrs.Size = new Size(100, 23);
            txtHrs.TabIndex = 3;
            // 
            // txtKey
            // 
            txtKey.FormattingEnabled = true;
            txtKey.Items.AddRange(new object[] { "Training", "Retraining", "Proficiency" });
            txtKey.Location = new Point(797, 80);
            txtKey.Name = "txtKey";
            txtKey.Size = new Size(71, 23);
            txtKey.TabIndex = 2;
            // 
            // lbl_ExpiryDate
            // 
            lbl_ExpiryDate.AutoSize = true;
            lbl_ExpiryDate.Location = new Point(797, 231);
            lbl_ExpiryDate.Name = "lbl_ExpiryDate";
            lbl_ExpiryDate.Size = new Size(71, 15);
            lbl_ExpiryDate.TabIndex = 62;
            lbl_ExpiryDate.Text = "Expiry Date :";
            // 
            // lbl_IssueDate
            // 
            lbl_IssueDate.AutoSize = true;
            lbl_IssueDate.Location = new Point(797, 174);
            lbl_IssueDate.Name = "lbl_IssueDate";
            lbl_IssueDate.Size = new Size(66, 15);
            lbl_IssueDate.TabIndex = 61;
            lbl_IssueDate.Text = "Issue Date :";
            // 
            // lbl_Certname
            // 
            lbl_Certname.AutoSize = true;
            lbl_Certname.Location = new Point(797, 4);
            lbl_Certname.Name = "lbl_Certname";
            lbl_Certname.Size = new Size(105, 15);
            lbl_Certname.TabIndex = 60;
            lbl_Certname.Text = "Certificate Name : ";
            // 
            // btnEdit
            // 
            btnEdit.Location = new Point(922, 416);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(75, 23);
            btnEdit.TabIndex = 9;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // dtpExpiryDate
            // 
            dtpExpiryDate.Location = new Point(797, 249);
            dtpExpiryDate.Name = "dtpExpiryDate";
            dtpExpiryDate.ShowCheckBox = true;
            dtpExpiryDate.Size = new Size(200, 23);
            dtpExpiryDate.TabIndex = 6;
            // 
            // dtpIssueDate
            // 
            dtpIssueDate.Location = new Point(797, 192);
            dtpIssueDate.Name = "dtpIssueDate";
            dtpIssueDate.Size = new Size(200, 23);
            dtpIssueDate.TabIndex = 5;
            // 
            // txtCertName
            // 
            txtCertName.Location = new Point(797, 22);
            txtCertName.Name = "txtCertName";
            txtCertName.Size = new Size(200, 23);
            txtCertName.TabIndex = 1;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(786, 407);
            dataGridView1.TabIndex = 53;
            dataGridView1.CellContentClick += dataGridView1_CellContentClick;
            dataGridView1.SelectionChanged += dataGridView1_SelectionChanged;
            // 
            // tabTraining
            // 
            tabTraining.Controls.Add(dgvPlannedTraining);
            tabTraining.Location = new Point(4, 24);
            tabTraining.Name = "tabTraining";
            tabTraining.Padding = new Padding(3);
            tabTraining.Size = new Size(1011, 445);
            tabTraining.TabIndex = 1;
            tabTraining.Text = "Training";
            tabTraining.UseVisualStyleBackColor = true;
            // 
            // dgvPlannedTraining
            // 
            dgvPlannedTraining.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPlannedTraining.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPlannedTraining.Location = new Point(6, 6);
            dgvPlannedTraining.Name = "dgvPlannedTraining";
            dgvPlannedTraining.Size = new Size(994, 430);
            dgvPlannedTraining.TabIndex = 4;
            // 
            // tabAcknowledgements
            // 
            tabAcknowledgements.Controls.Add(label2);
            tabAcknowledgements.Controls.Add(label3);
            tabAcknowledgements.Controls.Add(label4);
            tabAcknowledgements.Controls.Add(textBox1);
            tabAcknowledgements.Controls.Add(txtAckHours);
            tabAcknowledgements.Controls.Add(comboBox1);
            tabAcknowledgements.Controls.Add(label8);
            tabAcknowledgements.Controls.Add(label9);
            tabAcknowledgements.Controls.Add(dtpAckDate);
            tabAcknowledgements.Controls.Add(txtTopic);
            tabAcknowledgements.Controls.Add(dgvAcknowledgements);
            tabAcknowledgements.Location = new Point(4, 24);
            tabAcknowledgements.Name = "tabAcknowledgements";
            tabAcknowledgements.Size = new Size(1011, 445);
            tabAcknowledgements.TabIndex = 2;
            tabAcknowledgements.Text = "Acknowledgements";
            tabAcknowledgements.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(797, 119);
            label2.Name = "label2";
            label2.Size = new Size(57, 15);
            label2.TabIndex = 79;
            label2.Text = "Provider :";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(897, 70);
            label3.Name = "label3";
            label3.Size = new Size(71, 15);
            label3.TabIndex = 78;
            label3.Text = "CPD Hours :";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(797, 70);
            label4.Name = "label4";
            label4.Size = new Size(32, 15);
            label4.TabIndex = 77;
            label4.Text = "Key :";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(797, 137);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(200, 23);
            textBox1.TabIndex = 73;
            // 
            // txtAckHours
            // 
            txtAckHours.Location = new Point(897, 88);
            txtAckHours.Name = "txtAckHours";
            txtAckHours.Size = new Size(100, 23);
            txtAckHours.TabIndex = 72;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "Training", "Retraining", "Proficiency" });
            comboBox1.Location = new Point(797, 88);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(71, 23);
            comboBox1.TabIndex = 71;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(797, 182);
            label8.Name = "label8";
            label8.Size = new Size(66, 15);
            label8.TabIndex = 76;
            label8.Text = "Issue Date :";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(797, 12);
            label9.Name = "label9";
            label9.Size = new Size(105, 15);
            label9.TabIndex = 75;
            label9.Text = "Certificate Name : ";
            // 
            // dtpAckDate
            // 
            dtpAckDate.Location = new Point(797, 200);
            dtpAckDate.Name = "dtpAckDate";
            dtpAckDate.Size = new Size(200, 23);
            dtpAckDate.TabIndex = 74;
            // 
            // txtTopic
            // 
            txtTopic.Location = new Point(797, 30);
            txtTopic.Name = "txtTopic";
            txtTopic.Size = new Size(200, 23);
            txtTopic.TabIndex = 70;
            // 
            // dgvAcknowledgements
            // 
            dgvAcknowledgements.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAcknowledgements.Location = new Point(5, 3);
            dgvAcknowledgements.Name = "dgvAcknowledgements";
            dgvAcknowledgements.Size = new Size(786, 407);
            dgvAcknowledgements.TabIndex = 54;
            // 
            // EmployeeDashboard
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1019, 471);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "EmployeeDashboard";
            Text = "Employee Dashboard";
            Load += EmployeeDashboard_Load;
            tabControl.ResumeLayout(false);
            tabCertificates.ResumeLayout(false);
            tabCertificates.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tabTraining.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvPlannedTraining).EndInit();
            tabAcknowledgements.ResumeLayout(false);
            tabAcknowledgements.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvAcknowledgements).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabCertificates;
        private TabPage tabTraining;
        private Button btnBrowseFile;
        private Label label7;
        private Label label6;
        private Label label5;
        private TextBox txtProvider;
        private TextBox txtHrs;
        private ComboBox txtKey;
        private Label label1;
        private Label lbl_ExpiryDate;
        private Label lbl_IssueDate;
        private Label lbl_Certname;
        private TextBox txtFilePath;
        private Button btnEdit;
        private DateTimePicker dtpExpiryDate;
        private DateTimePicker dtpIssueDate;
        private TextBox txtCertName;
        private DataGridView dataGridView1;
        private DataGridView dgvPlannedTraining;
        private Button btnUpload;
        private Button btnRequestDelete;
        private TabPage tabAcknowledgements;
        private DataGridView dgvAcknowledgements;
        private Label label2;
        private Label label3;
        private Label label4;
        private TextBox textBox1;
        private TextBox txtAckHours;
        private ComboBox comboBox1;
        private Label label8;
        private Label label9;
        private DateTimePicker dtpAckDate;
        private TextBox txtTopic;
    }
}