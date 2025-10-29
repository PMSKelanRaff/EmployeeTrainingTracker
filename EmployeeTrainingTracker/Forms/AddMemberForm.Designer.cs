namespace EmployeeTrainingTracker.Utilities
{
    partial class AddMemberForm
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
            dgvAvailable = new DataGridView();
            btnAdd = new Button();
            btnCancel = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvAvailable).BeginInit();
            SuspendLayout();
            // 
            // dgvAvailable
            // 
            dgvAvailable.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAvailable.Location = new Point(12, 12);
            dgvAvailable.Name = "dgvAvailable";
            dgvAvailable.Size = new Size(389, 397);
            dgvAvailable.TabIndex = 0;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(245, 415);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(75, 23);
            btnAdd.TabIndex = 1;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(326, 415);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(75, 23);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // AddMemberForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(413, 446);
            Controls.Add(btnCancel);
            Controls.Add(btnAdd);
            Controls.Add(dgvAvailable);
            Name = "AddMemberForm";
            Text = "AddMemberForm";
            ((System.ComponentModel.ISupportInitialize)dgvAvailable).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView dgvAvailable;
        private Button btnAdd;
        private Button btnCancel;
    }
}