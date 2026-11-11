using EmployeeTrainingTracker.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixDryRun;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixImportPlan;

namespace EmployeeTrainingTracker
{
    /// <summary>
    /// "Prepare import": combines a dry run with the reviewer's recorded decisions into an import plan, shows
    /// exactly what would be written, lists blocking errors, and collects the required acknowledgements.
    ///
    /// The final Import step is controlled by ImportExecutionEnabled (switched on 2026-11-11 for the approved
    /// first import; set it to false to disable the button again). It requires a READY plan, every acknowledgement, a
    /// re-check of Admin access and typing "IMPORT &lt;first 8 characters of the file's SHA-256&gt;".
    /// </summary>
    internal sealed class SkillsMatrixImportForm : Form
    {
        /// <summary>
        /// Import execution: enabled 2026-11-11 for the approved first import (plan SkillsMatrix_IMPORTPLAN_20261111_093043,
        /// production pre-check passed). Still requires a READY plan, every acknowledgement, Admin access and the typed confirmation.
        /// </summary>
        internal static readonly bool ImportExecutionEnabled = true;

        private readonly DryRunResult _dry;
        private readonly int _adminEmployeeId;
        private DecisionsFile? _decisions;
        private ImportPlan? _plan;
        private readonly HashSet<string> _acknowledged = new(StringComparer.Ordinal);
        private bool _filling;

        private readonly Label lblDecisions = new() { AutoSize = true, Text = "(none chosen)", Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
        private readonly TextBox txtApprovedBy = new() { Width = 260, Anchor = AnchorStyles.Left };
        private readonly TextBox txtVersion = new() { Width = 160, Anchor = AnchorStyles.Left, PlaceholderText = "e.g. 2026 v1" };
        private readonly TextBox txtNotes = new() { Dock = DockStyle.Fill, PlaceholderText = "Optional notes stored with the import" };
        private readonly TextBox txtSummary = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F) };
        private readonly CheckedListBox clbAcks = new() { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        private readonly Label lblStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private readonly Button btnImport = new() { AutoSize = true };

        public SkillsMatrixImportForm(DryRunResult dry, int adminEmployeeId)
        {
            _dry = dry;
            _adminEmployeeId = adminEmployeeId;
            Text = "Skills Matrix - Prepare import (nothing is written)";
            Size = new Size(1200, 760);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var banner = new Label
            {
                Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 6, 0),
                BackColor = SystemColors.Info, ForeColor = SystemColors.InfoText, Font = new Font(Font, FontStyle.Bold),
                Text = "PREPARE IMPORT - shows exactly what an approved import would write. Nothing is written from this window" +
                       (ImportExecutionEnabled ? " until you confirm the final Import step." : " (the Import step is switched off in this build)."),
            };

            // Details: decisions file, approval, version, notes
            var details = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(6, 4, 6, 4) };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var btnDecisions = new Button { Text = "Choose reviewed reconciliation CSV…", AutoSize = true };
            btnDecisions.Click += (_, _) => ChooseDecisions();
            details.Controls.Add(Caption("1. Reviewed decisions:"), 0, 0); details.Controls.Add(lblDecisions, 1, 0); details.Controls.Add(btnDecisions, 2, 0);
            details.Controls.Add(Caption("2. Approved by:"), 0, 1); details.Controls.Add(txtApprovedBy, 1, 1);
            details.Controls.Add(Caption("   Version label:"), 0, 2); details.Controls.Add(txtVersion, 1, 2);
            details.Controls.Add(Caption("   Notes:"), 0, 3); details.Controls.Add(txtNotes, 1, 3);
            details.SetColumnSpan(txtNotes, 2);
            var help = new Label
            {
                AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 2, 3, 6),
                Text = "In the reconciliation CSV, set HumanConfirmation to CONFIRMED (with ConfirmedEmployeeID) or EXCLUDED (with the reason in ReviewerNotes) " +
                       "for every name. Save it as 'CSV UTF-8'. Unconfirmed names block the import.",
            };
            details.Controls.Add(help, 1, 4);
            details.SetColumnSpan(help, 2);
            foreach (var t in new[] { txtApprovedBy, txtVersion, txtNotes }) t.Leave += (_, _) => Rebuild();

            // Summary (left) and acknowledgements (right)
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 700 };
            split.Panel1.Controls.Add(txtSummary);
            var ackPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            ackPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            ackPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            ackPanel.Controls.Add(new Label { AutoSize = true, Margin = new Padding(3, 6, 3, 3), Font = new Font(Font, FontStyle.Bold),
                                              Text = "3. Acknowledgements (each must be ticked by you):" }, 0, 0);
            ackPanel.Controls.Add(clbAcks, 0, 1);
            split.Panel2.Controls.Add(ackPanel);
            clbAcks.ItemCheck += (_, e) =>
            {
                if (_filling || _plan == null) return;
                string key = _plan.Acknowledgements[e.Index].Key;
                if (e.NewValue == CheckState.Checked) _acknowledged.Add(key); else _acknowledged.Remove(key);
                BeginInvoke(new Action(Render));
            };

            // Buttons
            var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 42, ColumnCount = 4, Padding = new Padding(6) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 3; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var btnSave = new Button { Text = "Save plan summary…", AutoSize = true };
            var btnClose = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            btnSave.Click += (_, _) => SaveSummary();
            btnImport.Click += (_, _) => RunImport();
            buttons.Controls.Add(lblStatus, 0, 0);
            buttons.Controls.Add(btnSave, 1, 0);
            buttons.Controls.Add(btnImport, 2, 0);
            buttons.Controls.Add(btnClose, 3, 0);

            Controls.Add(split);
            Controls.Add(details);
            Controls.Add(buttons);
            Controls.Add(banner);
            Rebuild();
        }

        private static Label Caption(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };

        private void ChooseDecisions()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choose the REVIEWED reconciliation CSV (from the dry run)",
                Filter = "CSV file (*.csv)|*.csv",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _decisions = LoadDecisionsCsv(dialog.FileName);
                lblDecisions.Text = $"{_decisions.Path}   ({_decisions.Rows.Count} rows, SHA-256 {_decisions.Sha256[..12]}…)";
                lblDecisions.ForeColor = SystemColors.ControlText;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not read the decisions file:\n{ex.Message}", "Prepare import", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Rebuild();
        }

        private string ImportedBy => $"{Environment.UserName} (Training Tracker admin, employee ID {_adminEmployeeId})";

        private void Rebuild()
        {
            _plan = Build(_dry, _decisions, new ImportDetails(txtApprovedBy.Text, ImportedBy, txtVersion.Text, txtNotes.Text));
            var keys = _plan.Acknowledgements.Select(a => a.Key).ToHashSet();
            _acknowledged.RemoveWhere(k => !keys.Contains(k));   // keep ticks that still apply

            _filling = true;
            clbAcks.Items.Clear();
            foreach (var a in _plan.Acknowledgements) clbAcks.Items.Add(a.Text, _acknowledged.Contains(a.Key));
            _filling = false;
            Render();
        }

        private void Render()
        {
            if (_plan == null) return;
            txtSummary.Text = RenderSummary(_plan, _acknowledged);
            bool ready = _plan.IsReady(_acknowledged);
            int outstanding = _plan.Acknowledgements.Count(a => !_acknowledged.Contains(a.Key));
            lblStatus.Text = ready
                ? "READY - every decision is recorded and acknowledged."
                : $"NOT READY - {_plan.Errors.Count} blocking error(s), {outstanding} acknowledgement(s) outstanding.";
            lblStatus.ForeColor = ready ? Color.DarkGreen : Color.DarkRed;
            btnImport.Text = ImportExecutionEnabled ? "Import…" : "Import (switched off in this build)";
            btnImport.Enabled = ImportExecutionEnabled && ready;
        }

        private void SaveSummary()
        {
            if (_plan == null) return;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            using var dialog = new SaveFileDialog
            {
                Title = "Save the import plan summary (a new file; nothing is imported)",
                Filter = "Text file (*.txt)|*.txt",
                FileName = $"SkillsMatrix_IMPORTPLAN_{stamp}.txt",
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                using var fs = new FileStream(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None);   // never overwrites
                using var sw = new StreamWriter(fs, new UTF8Encoding(true));
                sw.Write(RenderSummary(_plan, _acknowledged));
                MessageBox.Show(this, "Saved (nothing was imported):\n" + dialog.FileName, "Prepare import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (IOException ex) when (File.Exists(dialog.FileName))
            {
                MessageBox.Show(this, $"That file already exists and was not overwritten:\n{dialog.FileName}\n\n{ex.Message}", "Prepare import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not save:\n{ex.Message}", "Prepare import", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>The approved first import. Unreachable while ImportExecutionEnabled is false (the button stays disabled).</summary>
        private void RunImport()
        {
            if (!ImportExecutionEnabled || _plan == null || !_plan.IsReady(_acknowledged)) return;

            using var conn = DatabaseHelper.GetConnection();
            // NpgsqlConnection.Host is empty until the connection is open, so read the target from the connection string
            var cs = new Npgsql.NpgsqlConnectionStringBuilder(conn.ConnectionString);
            string target = $"{cs.Host}:{cs.Port} / database '{cs.Database}' as user '{cs.Username}'";
            if (string.IsNullOrWhiteSpace(cs.Host) || string.IsNullOrWhiteSpace(cs.Database))
            {
                MessageBox.Show(this, "The database host or name is not configured, so the import was not started.", "Skills Matrix import",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string expected = SkillsMatrixImportWriter.ExpectedConfirmation(_plan);
            int? edits;
            try
            {
                using var check = DatabaseHelper.GetConnection();
                check.Open();
                edits = SkillsMatrixLiveService.EditsSinceRebuild(check);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not check the live copy, so the import was not started:\n{ex.Message}", "Skills Matrix import",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string liveText = edits switch
            {
                null => "",
                0 => "\n\nThe live copy (what the Skills Matrix tab shows and edits) is rebuilt from this import. No changes have been made in the app since it was last built.",
                _ => $"\n\nWARNING: the live copy (what the Skills Matrix tab shows and edits) is rebuilt from this import. " +
                     $"{edits} change(s) made in the app since it was last built will be REPLACED by the workbook's values. They stay listed in History.",
            };
            string? typed = Prompt($"This writes the Skills Matrix import to:\n\n    {target}\n\n" +
                                   $"{_plan.Members} member columns, {_plan.Cells} cells, {_plan.Formats} formatting rows. It becomes the current snapshot; " +
                                   $"earlier snapshots are kept unchanged.{liveText}\n\nType  {expected}  to confirm:");
            if (typed == null) return;
            try
            {
                SkillsMatrixService.EnsureImportUser(_adminEmployeeId);
                conn.Open();
                var r = SkillsMatrixImportWriter.Write(conn, _plan, _acknowledged, typed, _adminEmployeeId);
                MessageBox.Show(this, $"Import {r.ImportId} written and made current ({r.Columns} columns, {r.Cells} cells, {r.Formats} formatting rows, {r.Took.TotalSeconds:0.0} s)." +
                                      (r.LiveCellsRebuilt is int live ? $"\n\nThe live copy was rebuilt from it ({live} cells). Refresh the Skills Matrix tab." : ""),
                                "Skills Matrix import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (SkillsMatrixImportWriter.CommitOutcomeUnknownException ex)
            {
                MessageBox.Show(this, ex.Message, "Skills Matrix import - outcome unknown", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"The import was NOT written (nothing was kept):\n{ex.Message}", "Skills Matrix import", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string? Prompt(string message)
        {
            using var f = new Form { Text = "Confirm Skills Matrix import", Size = new Size(700, 400), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false };
            var box = new TextBox { Dock = DockStyle.Bottom };
            var ok = new Button { Text = "Import", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
            f.Controls.Add(new Label { Text = message, Dock = DockStyle.Fill, Padding = new Padding(8) });
            f.Controls.Add(box);
            f.Controls.Add(ok);
            f.AcceptButton = ok;
            return f.ShowDialog(this) == DialogResult.OK ? box.Text : null;
        }
    }
}
