using EmployeeTrainingTracker.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixMerge;

namespace EmployeeTrainingTracker
{
    /// <summary>
    /// Shows how a re-import would treat the levels changed in the app (SkillsMatrixMerge) and collects a decision
    /// for every conflict (changed in both the app and the workbook, differently). Nothing is written here:
    /// "Use these decisions" returns the merge for the Import step.
    /// </summary>
    internal sealed class SkillsMatrixMergeForm : Form
    {
        private const string KeepApp = "Keep the app's value";
        private const string TakeExcel = "Take the workbook's value";
        private const string Auto = "Kept automatically (changed only in the app)";

        private readonly Report _report;
        private readonly List<AppEdit> _conflicts;
        private readonly DataGridView grid = new()
        {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };
        private readonly Button btnOk = new() { Text = "Use these decisions", AutoSize = true };
        private readonly Label lblStatus = new() { AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };

        public MergePlan? Result { get; private set; }

        public SkillsMatrixMergeForm(Report report)
        {
            _report = report;
            _conflicts = report.Conflicts.ToList();
            Text = "Skills Matrix - compare the new workbook with the live copy (nothing is written)";
            Size = new Size(1250, 760);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var summary = new TextBox
            {
                Dock = DockStyle.Top, Height = 230, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9F), Text = SummaryText(report),
            };

            grid.Columns.Add(Column("Sheet", 12));
            grid.Columns.Add(Column("Skill", 24));
            grid.Columns.Add(Column("Person", 14));
            grid.Columns.Add(Column("At import", 9));
            grid.Columns.Add(Column("In the app (changed by)", 20));
            grid.Columns.Add(Column("In the new workbook", 11));
            var result = new DataGridViewComboBoxColumn { HeaderText = "Result", FillWeight = 26, FlatStyle = FlatStyle.Flat };
            result.Items.AddRange(KeepApp, TakeExcel, Auto, "");
            grid.Columns.Add(result);
            UIHelpers.StyleDataGridView(grid);

            // Conflicts first (they need a decision), then the automatic keeps, then what cannot be kept
            foreach (var e in report.Edits.OrderBy(e => e.Outcome == Outcome.Conflict ? 0 : e.Outcome == Outcome.KeepApp ? 1 : 2))
            {
                int i = grid.Rows.Add(e.Sheet, e.Skill, e.EmployeeName, e.BaseValue,
                                      $"{e.AppValue}  ({e.ChangedBy}, {e.ChangedAt:yyyy-MM-dd})", e.ExcelValue ?? "-", "");
                var row = grid.Rows[i];
                row.Tag = e;
                for (int c = 0; c < 6; c++) row.Cells[c].ReadOnly = true;
                switch (e.Outcome)
                {
                    case Outcome.Conflict:
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 200);
                        row.Cells[6].ToolTipText = "Changed in both the app and the workbook, to different values: choose which to keep.";
                        break;
                    case Outcome.KeepApp:
                        row.Cells[6].Value = Auto;
                        row.Cells[6].ReadOnly = true;
                        break;
                    default:
                        // Cannot be kept: show the reason as plain text instead of a drop-down
                        row.Cells[6] = new DataGridViewTextBoxCell { Value = "Cannot be kept: " + e.LostReason };
                        row.Cells[6].ReadOnly = true;
                        row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
                        break;
                }
            }
            grid.CellValueChanged += (_, _) => UpdateStatus();
            grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.DataError += (_, e) => e.ThrowException = false;

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6), FlowDirection = FlowDirection.LeftToRight };
            var btnAllApp = new Button { Text = "Keep all app values", AutoSize = true, Enabled = _conflicts.Count > 0 };
            var btnAllExcel = new Button { Text = "Take all workbook values", AutoSize = true, Enabled = _conflicts.Count > 0 };
            var btnCancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            btnAllApp.Click += (_, _) => SetAll(KeepApp);
            btnAllExcel.Click += (_, _) => SetAll(TakeExcel);
            btnOk.Click += (_, _) => Accept();
            bottom.Controls.AddRange(new Control[] { btnAllApp, btnAllExcel, btnOk, btnCancel, lblStatus });

            Controls.Add(grid);
            Controls.Add(summary);
            Controls.Add(bottom);
            CancelButton = btnCancel;
            UpdateStatus();
        }

        private static DataGridViewTextBoxColumn Column(string header, int weight) => new() { HeaderText = header, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable };

        private static string SummaryText(Report r)
        {
            var lines = new List<string>
            {
                $"Live copy built from import {r.BasedOnImportId}. The import rebuilds it from the new workbook; the app values below marked 'keep' are put back.",
                "",
                $"Changed only in the app - kept automatically:        {r.KeptAutomatically}",
                $"Changed in both, differently - YOU CHOOSE below:     {r.Conflicts.Count()}",
                $"Changed in both to the same value:                   {r.SameInBoth}",
                $"Changed in the app, not in the new workbook:         {r.Lost}  (cannot be kept)",
                $"Changed only in the workbook - taken from it:        {r.ExcelChangedCells}",
                "",
                $"Structure (follows the workbook): {(r.Structure.Count == 0 ? "no changes" : "")}",
            };
            lines.AddRange(r.Structure.Select(s => "   " + s));
            if (r.ExcelChanges.Count > 0)
            {
                lines.Add("");
                lines.Add("Changed only in the workbook:");
                lines.AddRange(r.ExcelChanges.Take(300).Select(s => "   " + s));
                if (r.ExcelChanges.Count > 300) lines.Add($"   ... and {r.ExcelChanges.Count - 300} more (all are in the saved plan summary)");
            }
            return string.Join(Environment.NewLine, lines);
        }

        private IEnumerable<DataGridViewRow> ConflictRows =>
            grid.Rows.Cast<DataGridViewRow>().Where(r => r.Tag is AppEdit { Outcome: Outcome.Conflict });

        private void SetAll(string choice)
        {
            foreach (var row in ConflictRows) row.Cells[6].Value = choice;
            UpdateStatus();
        }

        private List<bool?> Decisions() => _conflicts.Select(c =>
        {
            var row = ConflictRows.First(r => ReferenceEquals(r.Tag, c));
            return row.Cells[6].Value as string switch { KeepApp => (bool?)true, TakeExcel => false, _ => null };
        }).ToList();

        private void UpdateStatus()
        {
            int open = Decisions().Count(d => d == null);
            btnOk.Enabled = open == 0;
            lblStatus.Text = open == 0 ? "Every conflict is decided." : $"{open} conflict(s) still to decide.";
            lblStatus.ForeColor = open == 0 ? Color.DarkGreen : Color.DarkRed;
        }

        private void Accept()
        {
            try
            {
                Result = Decide(_report, Decisions());
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Compare with the live copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
