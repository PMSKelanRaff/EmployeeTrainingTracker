using EmployeeTrainingTracker.Utilities;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixDryRun;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixReconciliation;
using ReviewKindNames = EmployeeTrainingTracker.Utilities.SkillsMatrixWorkbookPreview.ReviewKind;

namespace EmployeeTrainingTracker
{
    /// <summary>
    /// Shows a Skills Matrix DRY RUN: the report, the employee reconciliation (all unconfirmed) and the
    /// review items. Its only action is saving the report files to a folder the user chooses.
    /// There is no import, confirm or database action here.
    /// </summary>
    internal sealed class SkillsMatrixDryRunForm : Form
    {
        private readonly DryRunResult _result;

        public SkillsMatrixDryRunForm(DryRunResult result)
        {
            _result = result;
            Text = "Skills Matrix - DRY RUN (nothing imported, nothing written to the database)";
            Size = new Size(1150, 700);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var banner = new Label
            {
                Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 6, 0),
                BackColor = SystemColors.Info, ForeColor = SystemColors.InfoText, Font = new Font(Font, FontStyle.Bold),
                Text = Banner,
            };

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var decisions = DecisionRows(result).ToList();
            tabs.TabPages.Add(GridPage($"Decisions needed ({decisions.Count})",
                new[] { "Type", "Item (as written)", "Where", "Current state", "Decision needed" },
                decisions.Select(d => new object?[] { d.Type, d.Item, d.Where, d.State, d.Needed }),
                (row, i) => row.DefaultCellStyle.BackColor = decisions[i].Colour));
            tabs.TabPages.Add(TextPage("Summary", RenderReport(result)));
            tabs.TabPages.Add(GridPage($"Employee reconciliation ({result.Reconciliation.Count})",
                new[] { "Original name (as written)", "Appears in", "Status", "Match type", "Confidence", "Proposed ID",
                        "Proposed employee", "Archived", "Reason", "Human confirmation" },
                result.Reconciliation.Select(x => new object?[]
                {
                    x.OriginalName, string.Join(", ", x.Occurrences.Select(o => o.HeaderCell)), StatusText(x.Status), x.MatchType,
                    x.Confidence, x.ProposedEmployeeId, x.ProposedEmployeeName,
                    x.ProposedEmployeeArchived switch { true => "Yes", false => "No", null => x.ProposedEmployeeId == null ? "" : "Unknown" },
                    x.Reason, x.Confirmation,
                }),
                (row, i) => row.DefaultCellStyle.BackColor = StatusColour(result.Reconciliation[i].Status)));
            tabs.TabPages.Add(GridPage($"Review items ({result.ReviewItems.Count})",
                new[] { "Sheet", "Cell", "Kind", "Detail" },
                result.ReviewItems.Select(i => new object?[] { i.Sheet, i.Cell, i.Kind, i.Message })));

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            var save = new Button { Text = "Save dry-run report files…", AutoSize = true };
            save.Click += (_, _) => SaveFiles();
            buttons.Controls.Add(close);
            buttons.Controls.Add(save);
            buttons.Controls.Add(new Label
            {
                AutoSize = true, Margin = new Padding(3, 8, 12, 3), ForeColor = SystemColors.GrayText,
                Text = "Saves a report and three CSV files (new files only). Every employee match stays UNCONFIRMED.",
            });
            AcceptButton = close;

            Controls.Add(tabs);
            Controls.Add(buttons);
            Controls.Add(banner);
        }

        private static TabPage TextPage(string title, string text)
        {
            var page = new TabPage(title);
            page.Controls.Add(new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 9.5F), Text = text,
            });
            return page;
        }

        // App colours for the review lists only (not workbook colours): green = confirm, amber = decide, red = resolve
        private static readonly Color Confirm = Color.FromArgb(226, 239, 218), Decide = Color.FromArgb(255, 242, 204), Resolve = Color.FromArgb(248, 215, 218);

        private static Color StatusColour(MatchStatus s) => s switch
        {
            MatchStatus.ExactCandidate => Confirm,
            MatchStatus.SuggestedMatch => Decide,
            _ => Resolve,
        };

        private sealed record DecisionRow(string Type, string Item, string Where, string State, string Needed, Color Colour);

        /// <summary>Everything a person must decide or check before an import, most important first.</summary>
        private static System.Collections.Generic.IEnumerable<DecisionRow> DecisionRows(DryRunResult r)
        {
            foreach (var x in r.Reconciliation.OrderBy(x => x.Status switch
                     { MatchStatus.Unmatched => 0, MatchStatus.StudentOrNonEmployee => 1, MatchStatus.ArchivedEmployee => 2, MatchStatus.SuggestedMatch => 3, _ => 4 }))
            {
                string needed = x.Status switch
                {
                    MatchStatus.ExactCandidate => $"Confirm employee {x.ProposedEmployeeId} (CONFIRMED + ID), or EXCLUDED with a reason",
                    MatchStatus.SuggestedMatch => x.ProposedEmployeeId != null
                        ? $"Decide: is this employee {x.ProposedEmployeeId}? CONFIRMED + the right ID, or EXCLUDED with a reason"
                        : "Choose the right employee ID (CONFIRMED), or EXCLUDED with a reason",
                    MatchStatus.ArchivedEmployee => $"Decide whether to include archived employee {x.ProposedEmployeeId}, or EXCLUDED with a reason",
                    MatchStatus.StudentOrNonEmployee => "EXCLUDED with a reason - or CONFIRMED + ID if this person is an employee",
                    _ => "Find the employee ID (CONFIRMED), or EXCLUDED with a reason",
                };
                yield return new DecisionRow("Employee name", x.OriginalName, string.Join(", ", x.Occurrences.Select(o => o.HeaderCell)),
                    $"{StatusText(x.Status)} - UNCONFIRMED", needed, StatusColour(x.Status));
            }
            foreach (var i in r.ReviewItems.Where(i => i.Kind is ReviewKindNames.AmbiguousLegend or ReviewKindNames.LevelNotInKey))
                yield return new DecisionRow(i.Kind == ReviewKindNames.AmbiguousLegend ? "Level definitions" : "Ambiguous level", i.Message, i.Cell ?? i.Sheet,
                    "Imported as written, flagged", "Confirm how this key / level should be read (acknowledged when preparing the import)", Decide);
            foreach (var i in r.ReviewItems.Where(i => i.Kind is ReviewKindNames.Formula or ReviewKindNames.UnexpectedValue or ReviewKindNames.OutOfRange))
                yield return new DecisionRow(i.Kind == ReviewKindNames.Formula ? "Formula cell" : "Flagged value", i.Message, i.Cell ?? i.Sheet,
                    "Flagged - never converted to a level", "Accept as Flagged (acknowledged when preparing the import), or correct the workbook and re-run", Decide);
            foreach (var i in r.ReviewItems.Where(i => i.Kind is not (ReviewKindNames.AmbiguousLegend or ReviewKindNames.LevelNotInKey
                                                         or ReviewKindNames.Formula or ReviewKindNames.UnexpectedValue or ReviewKindNames.OutOfRange)))
                yield return new DecisionRow("Check", i.Message, i.Cell ?? i.Sheet, i.Kind, "Check - kept as written", SystemColors.Window);
        }

        private static TabPage GridPage(string title, string[] headers, System.Collections.Generic.IEnumerable<object?[]> rows,
                                        Action<DataGridViewRow, int>? style = null)
        {
            var page = new TabPage(title);
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, ShowCellToolTips = true,
            };
            UIHelpers.StyleDataGridView(grid);
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            foreach (var h in headers)
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = h, SortMode = DataGridViewColumnSortMode.Automatic });
            int n = 0;
            foreach (var r in rows)
            {
                int index = grid.Rows.Add(r);
                style?.Invoke(grid.Rows[index], n++);
            }
            page.Controls.Add(grid);
            return page;
        }

        private void SaveFiles()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose a folder for the dry-run report files (new files are created; nothing is overwritten)",
                UseDescriptionForTitle = true,
                InitialDirectory = System.IO.Path.GetDirectoryName(_result.Workbook.FilePath) ?? "",
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var written = WriteFiles(_result, dialog.SelectedPath);
                MessageBox.Show(this, "Dry-run files saved (nothing was imported):\n\n" + string.Join("\n", written) +
                                      "\n\nThey contain employee names - store and share them accordingly.",
                                "Skills Matrix dry run", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not save the dry-run files:\n{ex.Message}", "Skills Matrix dry run",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
