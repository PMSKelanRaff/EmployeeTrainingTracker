using EmployeeTrainingTracker.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static EmployeeTrainingTracker.Utilities.SkillsMatrixService;

namespace EmployeeTrainingTracker
{
    /// <summary>
    /// Admin dashboard "Skills Matrix" tab: a READ-ONLY view of the current approved import of the
    /// audited Skills Matrix workbook - or, in PREVIEW mode, of a local workbook copy read in memory.
    /// Nothing here edits data or touches any workbook.
    ///
    /// Shows numbers and the sheet's own text legend (no colour meanings are inferred), and keeps
    /// four cell states visibly distinct: an assessed level, unassessed (blank in source), a
    /// flagged value, and a cell outside the sheet's source range.
    /// Stays in an explicit empty state until an approved import exists (or a preview is opened).
    ///
    /// Layout: every line above/below the grid is a fixed single line (full text on hover), so the
    /// grid always gets the remaining space at any window size. Long notes open in a separate list.
    /// </summary>
    public sealed class SkillsMatrixView : UserControl
    {
        private const string UnassessedText = "—";
        private const string FlaggedText = "⚠";
        private const int LineHeight = 24;
        private const string GroupRowTag = "group-row";

        private readonly int _adminEmployeeId;
        private bool _loaded;
        private SkillMatrixImportInfo? _import;
        private SkillMatrixData? _matrix;
        private Font? _boldFont;

        // PREVIEW ONLY: data read in memory from a local workbook copy (never from or to the database)
        private SkillsMatrixWorkbookPreview.PreviewResult? _preview;
        private SkillsMatrixWorkbookPreview.PreviewArea? _previewArea;
        // Workbook colours for the sheet shown: from the preview, or from an imported snapshot's SkillMatrixCellFormats
        private SkillsMatrixWorkbookPreview.PreviewColours? _colours;

        private readonly Button btnPreview = new() { Text = "Open workbook copy (preview)…", AutoSize = true, Dock = DockStyle.Left };
        private readonly Label lblPreviewInfo = SingleLine(SystemColors.GrayText,
            "  Steps:  1. Open a workbook COPY (read into memory only)   2. Dry run report - save the files   " +
            "3. Record CONFIRMED / EXCLUDED for every name in the reconciliation CSV   4. Prepare import - checks everything; writes nothing.");
        private readonly Label lblPreviewBanner = SingleLine(SystemColors.InfoText,
            "PREVIEW  ·  read-only workbook copy  ·  NOT an approved database import  ·  names as written, NOT matched to employees  ·  nothing is saved");

        private readonly ComboBox cmbArea = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Anchor = AnchorStyles.Left };
        private readonly CheckBox chkShowHidden = new() { Text = "Show hidden rows", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 3, 3, 3) };
        // The workbook's own colour standard (preview): every cell is painted with the colours Excel displays
        // for it - level colours, INAB green/red, headings, key - as worked out by SkillsMatrixWorkbookColours.
        private readonly CheckBox chkColour = new() { Text = "Workbook colours", AutoSize = true, Checked = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 3, 3, 3), Visible = false };
        private readonly FlowLayoutPanel legendChips = new() { AutoSize = true, WrapContents = false, Margin = new Padding(3, 2, 6, 0), Anchor = AnchorStyles.Left, Visible = false };
        private readonly Button btnNotes = new() { Text = "Review notes", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
        // DRY RUN (preview only): reports what an import would contain; never imports or touches the database
        private readonly Button btnDryRun = new() { Text = "Dry run report…", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
        // PREPARE IMPORT (preview only): builds and checks an import plan from reviewed decisions; writes nothing
        private readonly Button btnPrepare = new() { Text = "Prepare import…", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
        private readonly Button btnRefresh = new() { Text = "Refresh", AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly Label lblSource = SingleLine(SystemColors.GrayText, "");
        private readonly Label lblLegend = SingleLine(SystemColors.ControlText, "");
        private readonly Label lblLegendWarning = SingleLine(Color.DarkOrange, "");
        private readonly DataGridView grid = new() { Dock = DockStyle.Fill, Margin = new Padding(0) };
        private readonly Label lblFooter = SingleLine(SystemColors.GrayText, "");
        private readonly Label lblEmpty = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11F) };
        private readonly ToolTip toolTip = new() { AutoPopDelay = 30000 };

        private readonly TableLayoutPanel root = NewStack(3);
        private readonly TableLayoutPanel layout = NewStack(5);

        // Row indexes in 'root' and 'layout'
        private const int RootBannerRow = 1;
        private const int LayoutLegendWarningRow = 2;

        public SkillsMatrixView(int adminEmployeeId)
        {
            _adminEmployeeId = adminEmployeeId;
            lblPreviewBanner.BackColor = SystemColors.Info;
            lblPreviewBanner.Font = new Font(lblPreviewBanner.Font, FontStyle.Bold);

            // --- Matrix layout: header | legend | legend warning | grid | footer ---------------------
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 1, Margin = new Padding(0) };
            for (int i = 0; i < 8; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.Controls.Add(new Label { Text = "Sheet:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            header.Controls.Add(cmbArea, 1, 0);
            header.Controls.Add(chkShowHidden, 2, 0);
            header.Controls.Add(chkColour, 3, 0);
            header.Controls.Add(btnNotes, 4, 0);
            header.Controls.Add(btnDryRun, 5, 0);
            header.Controls.Add(btnPrepare, 6, 0);
            header.Controls.Add(btnRefresh, 7, 0);
            header.Controls.Add(lblSource, 8, 0);

            // Legend row: the sheet's key cells as coloured chips (workbook colours) + the key wording / notes
            var legendRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            legendRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            legendRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            legendRow.Controls.Add(legendChips, 0, 0);
            legendRow.Controls.Add(lblLegend, 1, 0);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));          // header
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LineHeight));  // legend
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));           // legend warning (shown when needed)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));         // grid
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, LineHeight));  // footer
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(legendRow, 0, 1);
            layout.Controls.Add(lblLegendWarning, 0, 2);
            layout.Controls.Add(grid, 0, 3);
            layout.Controls.Add(lblFooter, 0, 4);

            ConfigureGrid();

            // --- Root: top bar | preview banner | content (matrix or empty-state message) ------------
            var topBar = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            topBar.Controls.Add(lblPreviewInfo);   // Fill (added first so the docked button takes its space first)
            topBar.Controls.Add(btnPreview);       // Left

            var content = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            content.Controls.Add(layout);
            content.Controls.Add(lblEmpty);

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));           // top bar
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));            // preview banner (shown in preview)
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));          // content
            root.Controls.Add(topBar, 0, 0);
            root.Controls.Add(lblPreviewBanner, 0, 1);
            root.Controls.Add(content, 0, 2);
            Controls.Add(root);

            cmbArea.SelectedIndexChanged += (_, _) => LoadSelectedArea();
            chkShowHidden.CheckedChanged += (_, _) => RenderMatrix();
            chkColour.CheckedChanged += (_, _) => { if (_matrix != null) { RenderLegend(_matrix.Legend); RenderMatrix(); } };
            toolTip.SetToolTip(chkColour, "Show the workbook's own colours (level colours, INAB green/red, headings, key) exactly as Excel displays them. Untick for a plain view.");
            btnRefresh.Click += (_, _) => { ExitPreview(); _loaded = false; EnsureLoaded(); };
            btnPreview.Click += (_, _) => OpenPreview();
            btnNotes.Click += (_, _) => ShowNotes();
            btnDryRun.Click += (_, _) => RunDryRun();
            btnPrepare.Click += (_, _) => PrepareImport();
            toolTip.SetToolTip(btnPrepare, "Build and check an import plan from the reviewed reconciliation CSV: shows exactly what would be written, " +
                                           "blocking errors and the acknowledgements needed. Nothing is written.");
            toolTip.SetToolTip(btnDryRun, "DRY RUN: report what an import of this workbook copy would contain, with an employee " +
                                          "reconciliation for review. Nothing is imported and the database is not used.");
            toolTip.SetToolTip(lblPreviewInfo, lblPreviewInfo.Text.Trim());

            ShowEmpty("Open this tab to load the Skills Matrix.");
        }

        private static Label SingleLine(Color color, string text) => new()
        {
            AutoSize = false,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = color,
            Margin = new Padding(3, 0, 3, 0),
            Text = text,
        };

        // A one-column stack whose column is pinned to the available width (so nothing can widen it)
        private static TableLayoutPanel NewStack(int rows)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows, Margin = new Padding(0), Padding = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return t;
        }

        private static void SetRowHeight(TableLayoutPanel t, int row, int height) => t.RowStyles[row].Height = height;

        /// <summary>Loads on first use (called when the tab is opened), so the dashboard starts as before.</summary>
        public void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var (state, import) = SkillsMatrixService.GetCurrentImport(_adminEmployeeId);
                _import = import;

                switch (state)
                {
                    case SkillsMatrixState.NotInstalled:
                        ShowEmpty("The Skills Matrix has not been set up in the database yet " +
                                  "(migration 004_skills_matrix.sql has not been run).\n\n" +
                                  "To review the matrix now, use \"Open workbook copy (preview)…\" above.");
                        return;
                    case SkillsMatrixState.NoApprovedImport:
                        ShowEmpty("No approved Skills Matrix import has been loaded yet.\n\n" +
                                  "This view stays empty until an approved import of the verified workbook copy is completed.\n" +
                                  "To review the matrix now, use \"Open workbook copy (preview)…\" above.");
                        return;
                }

                var areas = SkillsMatrixService.GetAreas(_adminEmployeeId, import!.ImportId);
                if (areas.Count == 0)
                {
                    ShowEmpty("The current Skills Matrix import contains no sheets.");
                    return;
                }

                lblSource.Text = $"Source: {import.SourceFileName}" +
                                 (string.IsNullOrWhiteSpace(import.SourceVersion) ? "" : $" ({import.SourceVersion})") +
                                 $"  ·  SHA-256 {import.SourceSha256[..12]}…  ·  imported {import.ImportedAt:yyyy-MM-dd}" +
                                 $"  ·  approved by {import.ApprovedBy}  ·  read-only";
                toolTip.SetToolTip(lblSource, $"Full SHA-256: {import.SourceSha256}" +
                                              (string.IsNullOrWhiteSpace(import.Notes) ? "" : $"\n\nImport notes:\n{import.Notes}"));

                ShowMatrixLayout();
                cmbArea.DisplayMember = nameof(SkillAreaInfo.Name);
                cmbArea.DataSource = areas; // triggers LoadSelectedArea
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowEmpty(ex.Message);
            }
            catch (Exception ex)
            {
                _loaded = false; // allow Refresh to retry
                ShowEmpty($"Could not load the Skills Matrix:\n{ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------
        // PREVIEW ONLY: read-only view of a local workbook copy (no database reads or writes for data)

        private void OpenPreview()
        {
            // Same access rule as the database view: active Admin only (a read-only check of the login)
            try
            {
                SkillsMatrixService.EnsureAdmin(_adminEmployeeId);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowEmpty(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not confirm Admin access:\n{ex.Message}", "Skills Matrix preview", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dialog = new OpenFileDialog
            {
                Title = "Choose a LOCAL COPY of the Skills Matrix workbook (read-only preview)",
                Filter = "Excel workbook copy (*.xlsx)|*.xlsx",
                CheckFileExists = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            string? reject = SkillsMatrixWorkbookPreview.RejectReason(dialog.FileName);
            if (reject != null)
            {
                MessageBox.Show(reject, "Skills Matrix preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SkillsMatrixWorkbookPreview.PreviewResult result;
            try
            {
                Cursor = Cursors.WaitCursor;
                result = SkillsMatrixWorkbookPreview.Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read the workbook copy:\n{ex.Message}", "Skills Matrix preview", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (!string.Equals(result.Sha256Before, result.Sha256After, StringComparison.OrdinalIgnoreCase))
                MessageBox.Show("The file changed on disk while it was being read (it may be open and being edited). " +
                                "The preview shows the content that was read; reopen it once the file is no longer being changed.",
                                "Skills Matrix preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if (!result.MatchesVerifiedCopy &&
                MessageBox.Show($"This file's SHA-256 does not match the workbook copy verified on 2026-10-21.\n\n" +
                                $"Selected:  {result.Sha256Before}\nVerified:  {SkillsMatrixWorkbookPreview.VerifiedCopySha256}\n\n" +
                                "It may be a different version of the workbook. Preview it anyway?",
                                "Skills Matrix preview", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ShowPreview(result);
        }

        /// <summary>Displays an already-loaded workbook preview (no file dialog, no database access).</summary>
        internal void ShowPreview(SkillsMatrixWorkbookPreview.PreviewResult result)
        {
            if (result.Areas.Count == 0)
            {
                ShowEmpty("No recognised Skills Matrix sheets were found in that workbook copy.");
                return;
            }

            _preview = result;
            SetRowHeight(root, RootBannerRow, LineHeight);
            toolTip.SetToolTip(lblPreviewBanner,
                "PREVIEW - read-only view of a local workbook copy.\n" +
                "This is NOT an approved database import and nothing is saved.\n" +
                "Names are shown exactly as written in the workbook and are NOT matched to employee records.\n" +
                "Hidden/legacy sheets and the 'Skill Matrix required' template are not shown.");

            bool unchanged = string.Equals(result.Sha256Before, result.Sha256After, StringComparison.OrdinalIgnoreCase);
            lblSource.Text = $"{System.IO.Path.GetFileName(result.FilePath)}  ·  SHA-256 {result.Sha256Before[..12]}…  ·  " +
                             (result.MatchesVerifiedCopy ? "matches verified copy" : "DOES NOT match verified copy") +
                             (unchanged ? "  ·  unchanged on disk" : "  ·  FILE CHANGED WHILE READING");
            toolTip.SetToolTip(lblSource, $"{result.FilePath}\nSHA-256 before reading: {result.Sha256Before}\nSHA-256 after reading:  {result.Sha256After}\n" +
                                          $"Verified copy (2026-10-21): {SkillsMatrixWorkbookPreview.VerifiedCopySha256}");
            btnRefresh.Text = "Close preview";
            chkColour.Visible = true;   // workbook colours are available for a workbook preview
            btnDryRun.Visible = true;   // dry run works on this in-memory preview only
            btnPrepare.Visible = true;

            ShowMatrixLayout();
            cmbArea.DisplayMember = nameof(SkillAreaInfo.Name);
            cmbArea.DataSource = result.Areas.Select(a => a.Area).ToList(); // triggers LoadSelectedArea
        }

        private void ExitPreview()
        {
            _preview = null;
            _previewArea = null;
            _colours = null;
            SetRowHeight(root, RootBannerRow, 0);
            btnNotes.Visible = false;
            btnDryRun.Visible = false;
            btnPrepare.Visible = false;
            btnRefresh.Text = "Refresh";
            chkColour.Visible = false;
            legendChips.Visible = false;
        }

        // ------------------------------------------------------------------------------------------
        // DRY RUN: works only on the preview already read into memory (the Admin check was done when the
        // preview was opened). No database connection, no import; the only output is the on-screen report
        // and, if the user chooses, new report files.

        private void RunDryRun()
        {
            if (_preview == null) return;

            var choice = MessageBox.Show(
                "Reconcile the workbook's names against an employee list?\n\n" +
                "The dry run does not read employee records from the database. Choose a local CSV file with the columns " +
                "EmployeeID, FullName and (optionally) IsArchived - for example a test list.\n\n" +
                "Yes = choose a file    No = no list (every name is reported unmatched)    Cancel = stop",
                "Skills Matrix dry run", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel) return;

            SkillsMatrixReconciliation.EmployeeRoster? roster = null;
            if (choice == DialogResult.Yes)
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Choose a local employee list (CSV) for the dry-run reconciliation",
                    Filter = "CSV file (*.csv)|*.csv",
                    CheckFileExists = true,
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    roster = SkillsMatrixReconciliation.LoadRosterCsv(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not read the employee list:\n{ex.Message}", "Skills Matrix dry run", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            var result = SkillsMatrixDryRun.Run(_preview, roster);
            using var form = new SkillsMatrixDryRunForm(result);
            form.ShowDialog(this);
        }

        // PREPARE IMPORT: a separate step from the dry run. Needs the employee list the decisions were made against;
        // the plan window itself writes nothing (its Import step is switched off in this build).
        private void PrepareImport()
        {
            if (_preview == null) return;
            using var dialog = new OpenFileDialog
            {
                Title = "Prepare import - step 1 of 2: choose the employee list (CSV) used for the reviewed reconciliation",
                Filter = "CSV file (*.csv)|*.csv",
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            SkillsMatrixReconciliation.EmployeeRoster roster;
            try
            {
                roster = SkillsMatrixReconciliation.LoadRosterCsv(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read the employee list:\n{ex.Message}", "Prepare import", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using var form = new SkillsMatrixImportForm(SkillsMatrixDryRun.Run(_preview, roster), _adminEmployeeId);
            form.ShowDialog(this);
        }

        private void ShowNotes()
        {
            if (_preview == null || _previewArea == null) return;

            var lines = new List<string>
            {
                $"Sheet: {_previewArea.Area.Name}",
                "Items flagged for review. Nothing has been changed or reinterpreted; values are shown as written.",
                "",
            };
            lines.AddRange(_previewArea.Warnings.Select(w => "•  " + w));
            lines.Add("");
            lines.Add("Not previewed (whole workbook):");
            lines.AddRange(_preview.ExcludedSheets.Select(s => "•  " + s));

            using var dialog = new Form
            {
                Text = $"Review notes - {_previewArea.Area.Name} (preview)",
                Size = new Size(900, 520),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                ShowInTaskbar = false,
            };
            dialog.Controls.Add(new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Segoe UI", 9.5F),
                Text = string.Join(Environment.NewLine, lines),
            });
            dialog.ShowDialog(this);
        }

        private void LoadSelectedArea()
        {
            if (_preview != null)
            {
                if (cmbArea.SelectedItem is not SkillAreaInfo previewArea) return;
                _previewArea = _preview.Areas.First(a => a.Area.AreaId == previewArea.AreaId);
                _matrix = _previewArea.Data;
                _colours = _previewArea.Colours;
                RenderLegend(_matrix.Legend);
                RenderMatrix();
                return;
            }

            if (_import == null || cmbArea.SelectedItem is not SkillAreaInfo area) return;

            try
            {
                _matrix = SkillsMatrixService.GetMatrix(_adminEmployeeId, _import.ImportId, area.AreaId);
                _colours = ImportedColours(_matrix);
                chkColour.Visible = _colours != null;
                RenderLegend(_matrix.Legend);
                RenderMatrix();
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowEmpty(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load sheet '{area.Name}':\n{ex.Message}", "Skills Matrix", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ------------------------------------------------------------------------------------------
        // Rendering

        private void ConfigureGrid()
        {
            UIHelpers.StyleDataGridView(grid);
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // many people columns: don't squash them
            grid.ReadOnly = true;
            grid.EditMode = DataGridViewEditMode.EditProgrammatically;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToOrderColumns = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.ShowCellToolTips = true;
            grid.ScrollBars = ScrollBars.Both;
            grid.RowHeadersVisible = false;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            // Compact two-line headers (names); team/group labels live in a frozen first row instead
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 44;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.RowTemplate.Height = 22;
        }

        private void RenderLegend(SkillLegend legend)
        {
            // One entry per level, wording exactly as written. Where the sheet words a level more than
            // once differently, both wordings are shown side by side ("4: Trained / 4: Skilled in Area").
            var byLevel = legend.Entries.GroupBy(e => e.Level).OrderBy(g => g.Key).ToList();
            string labels = string.Join("     ", byLevel.Select(g => string.Join(" / ", g.OrderBy(e => e.KeyNumber).Select(e => e.Label))));
            bool hasDescriptions = legend.Entries.Any(e => !string.IsNullOrWhiteSpace(e.Description));

            string fullKey = $"{legend.ScaleName}, as written in the workbook ({legend.SourceRef}):\n" +
                             string.Join("\n", byLevel.SelectMany(g => g.Select(e =>
                                 string.IsNullOrWhiteSpace(e.Description) ? e.Label : $"{e.Label} - {e.Description}")));

            // Workbook colours: show the sheet's key cells as chips, painted exactly as the workbook shows them
            legendChips.SuspendLayout();
            foreach (Control old in legendChips.Controls.Cast<Control>().ToList()) { legendChips.Controls.Remove(old); old.Dispose(); }
            var keyCells = UseWorkbookColours ? _colours!.Legend : null;
            if (keyCells is { Count: > 0 })
            {
                foreach (var key in keyCells.GroupBy(k => (k.Level, k.Label)).Select(g => g.First()).OrderBy(k => k.Level))
                {
                    var chip = new Label { Text = key.Label, AutoSize = true, Padding = new Padding(4, 2, 4, 2), Margin = new Padding(0, 0, 3, 0) };
                    if (Look(key.Ref) is { } d)
                    {
                        if (d.Fill is Color f) chip.BackColor = f;
                        chip.ForeColor = d.Font;
                    }
                    toolTip.SetToolTip(chip, $"{key.Label}{(key.Description != null ? " - " + key.Description : "")}\nKey cell {key.Ref}, colours as in the workbook");
                    legendChips.Controls.Add(chip);
                }
                legendChips.Visible = true;
                lblLegend.Text = hasDescriptions ? "  (hover the key for descriptions)" : "";
            }
            else
            {
                legendChips.Visible = false;
                lblLegend.Text = byLevel.Count == 0
                    ? "Key: (no legend recorded for this sheet)"
                    : $"Key (as written):   {labels}" + (hasDescriptions ? "     (hover for descriptions)" : "");
            }
            legendChips.ResumeLayout();
            toolTip.SetToolTip(lblLegend, fullKey);

            lblLegendWarning.Text = legend.NeedsConfirmation ? $"⚠ This legend needs confirmation: {legend.LegendNote}" : "";
            toolTip.SetToolTip(lblLegendWarning, lblLegendWarning.Text);
            SetRowHeight(layout, LayoutLegendWarningRow, legend.NeedsConfirmation ? LineHeight : 0);
        }

        // ---- Workbook colours (preview) ------------------------------------------------------------

        private bool UseWorkbookColours => _colours != null && chkColour.Checked;

        /// <summary>Workbook colours of an imported snapshot (from SkillMatrixCellFormats), or null if none were stored.</summary>
        private static SkillsMatrixWorkbookPreview.PreviewColours? ImportedColours(SkillMatrixData m)
        {
            if (m.Formats is not { Count: > 0 } formats) return null;
            return new SkillsMatrixWorkbookPreview.PreviewColours(
                formats,
                m.Skills.Where(s => s.InabSourceRef != null).ToDictionary(s => s.SkillId, s => s.InabSourceRef!),
                m.Members.Where(x => x.HeaderSourceRef != null).ToDictionary(x => x.MemberId, x => x.HeaderSourceRef!),
                m.Members.Where(x => x.SubGroupSourceRef != null).ToDictionary(x => x.MemberId, x => x.SubGroupSourceRef!),
                m.Area.SkillHeaderRef, m.Area.InabHeaderRef,
                m.Legend.Entries.Where(e => e.SourceRef != null)
                    .Select(e => new SkillsMatrixWorkbookPreview.LegendCell(e.Level, e.Label, e.Description, e.SourceRef!)).ToList());
        }

        /// <summary>The colours Excel displays for a workbook cell, when workbook colours are on.</summary>
        private SkillsMatrixWorkbookColours.CellDisplay? Look(string? sourceRef) =>
            UseWorkbookColours && sourceRef != null && _colours!.Display.TryGetValue(sourceRef, out var d) ? d : null;

        private void PaintStyle(DataGridViewCellStyle style, SkillsMatrixWorkbookColours.CellDisplay? d)
        {
            if (d == null) return;
            if (d.Fill is Color fill)
            {
                style.BackColor = fill;
                style.SelectionBackColor = ControlPaint.Dark(fill, 0.08f);  // selection stays recognisable
            }
            else
            {
                style.BackColor = Color.White;                               // unfilled in the workbook
                style.SelectionBackColor = SystemColors.ControlLight;
            }
            style.ForeColor = d.Font;
            style.SelectionForeColor = d.Font;
            if (d.Bold && _boldFont != null) style.Font = _boldFont;
        }

        private void RenderMatrix()
        {
            if (_matrix == null) return;
            var m = _matrix;
            bool workbookColours = UseWorkbookColours;
            var colours = workbookColours ? _colours : null;

            grid.SuspendLayout();
            grid.Rows.Clear();
            grid.Columns.Clear();
            // Workbook colours: no app row banding, so unfilled cells are white as in Excel
            grid.AlternatingRowsDefaultCellStyle.BackColor = workbookColours ? Color.White : Color.FromArgb(245, 245, 245);
            _boldFont ??= new Font(grid.DefaultCellStyle.Font ?? Font, FontStyle.Bold);

            var skillCol = new DataGridViewTextBoxColumn { Name = "Skill", HeaderText = "Skill", Width = 290, Frozen = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            skillCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.Columns.Add(skillCol);
            var inabCol = new DataGridViewTextBoxColumn { Name = "Inab", HeaderText = "INAB accred.", Width = 92, Frozen = true, SortMode = DataGridViewColumnSortMode.NotSortable, ToolTipText = "INAB accredited (as written on the sheet)" };
            inabCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.Columns.Add(inabCol);
            if (colours != null)
            {
                PaintStyle(skillCol.HeaderCell.Style, Look(colours.SkillHeaderRef));
                // Sheets without an INAB column: plain header, like the workbook's own headings
                PaintStyle(inabCol.HeaderCell.Style, Look(colours.InabHeaderRef)
                    ?? new SkillsMatrixWorkbookColours.CellDisplay(null, SystemColors.GrayText, false));
            }

            foreach (var member in m.Members)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = $"M{member.MemberId}",
                    HeaderText = member.SourceHeaderText,
                    Width = 92,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    ToolTipText = (member.EmployeeId <= 0
                        ? $"'{member.SourceHeaderText}' - exactly as written in the workbook, column {member.SourceColumn}.\n" +
                          "Preview: not matched to an employee record."
                        : $"Employee: {member.EmployeeName}{(member.EmployeeArchived ? " (archived)" : "")}\n" +
                          $"As written in source: '{member.SourceHeaderText}', column {member.SourceColumn}") +
                        (string.IsNullOrWhiteSpace(member.SubGroupLabel) ? "" : $"\nTeam / group: {member.SubGroupLabel}"),
                };
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                if (colours != null && colours.HeaderRefs.TryGetValue(member.MemberId, out var headerRef))
                    PaintStyle(col.HeaderCell.Style, Look(headerRef));   // e.g. a student's name highlighted in the workbook
                grid.Columns.Add(col);
            }

            var levelLabels = m.Legend.Entries
                .GroupBy(e => e.Level)
                .ToDictionary(g => g.Key, g => string.Join(" / ", g.Select(e => e.Label)));

            int hiddenCount = m.Skills.Count(s => s.HiddenInSource);
            var boldFont = _boldFont ??= new Font(grid.DefaultCellStyle.Font ?? Font, FontStyle.Bold);

            // Frozen first row with each person's team/group label (from the sheet's merged group headings)
            if (m.Members.Any(x => !string.IsNullOrWhiteSpace(x.SubGroupLabel)))
            {
                int g = grid.Rows.Add();
                var groupRow = grid.Rows[g];
                groupRow.Tag = GroupRowTag;
                groupRow.Frozen = true;
                groupRow.DefaultCellStyle.BackColor = SystemColors.Info;
                groupRow.DefaultCellStyle.ForeColor = SystemColors.InfoText;
                groupRow.DefaultCellStyle.SelectionBackColor = SystemColors.Info;
                groupRow.DefaultCellStyle.SelectionForeColor = SystemColors.InfoText;
                groupRow.Cells[0].Value = "Team / group (from the sheet's headings)";
                for (int i = 0; i < m.Members.Count; i++)
                {
                    var label = m.Members[i].SubGroupLabel ?? "";
                    groupRow.Cells[2 + i].Value = label;
                    groupRow.Cells[2 + i].ToolTipText = label;
                    if (colours != null && colours.GroupRefs.TryGetValue(m.Members[i].MemberId, out var groupRef))
                        PaintStyle(groupRow.Cells[2 + i].Style, Look(groupRef));
                }
            }

            foreach (var category in m.Categories)
            {
                var skillsInCategory = m.Skills
                    .Where(s => s.CategoryId == category.CategoryId && (chkShowHidden.Checked || !s.HiddenInSource))
                    .ToList();

                // Category header row
                int catRow = grid.Rows.Add();
                var catCells = grid.Rows[catRow].Cells;
                catCells[0].Value = category.Name;
                catCells[0].ToolTipText = $"Category heading - {category.SourceRef}";
                grid.Rows[catRow].DefaultCellStyle.Font = boldFont;
                grid.Rows[catRow].DefaultCellStyle.BackColor = SystemColors.ControlLight;
                if (colours != null)
                    PaintStyle(grid.Rows[catRow].DefaultCellStyle, Look(category.SourceRef));   // whole heading row, as in the workbook

                foreach (var skill in skillsInCategory)
                {
                    int rowIndex = grid.Rows.Add();
                    var row = grid.Rows[rowIndex];

                    row.Cells[0].Value = skill.HiddenInSource ? $"{skill.Name}  (hidden in workbook)" : skill.Name;
                    row.Cells[0].ToolTipText = skill.SourceRef + (skill.HiddenInSource ? " - this row is hidden in the workbook" : "");
                    if (colours != null)
                        PaintStyle(row.Cells[0].Style, Look(skill.SourceRef));
                    if (skill.HiddenInSource)
                        row.DefaultCellStyle.ForeColor = row.Cells[0].Style.ForeColor = SystemColors.GrayText;

                    row.Cells[1].Value = skill.InabAccredited switch
                    {
                        true => "Yes",
                        false => "No",
                        null => string.IsNullOrWhiteSpace(skill.InabRaw) ? UnassessedText : $"{FlaggedText} {skill.InabRaw}",
                    };
                    row.Cells[1].ToolTipText = skill.InabAccredited != null
                        ? $"As written: '{skill.InabRaw}'"
                        : string.IsNullOrWhiteSpace(skill.InabRaw)
                            ? "Not specified on the sheet"
                            : $"Unexpected value, shown as written: '{skill.InabRaw}'";
                    if (colours != null && colours.InabRefs.TryGetValue(skill.SkillId, out var inabRef))
                        PaintStyle(row.Cells[1].Style, Look(inabRef));   // INAB Yes/No green/red, as in the workbook

                    for (int i = 0; i < m.Members.Count; i++)
                    {
                        var cell = row.Cells[2 + i];
                        if (!m.Cells.TryGetValue((skill.SkillId, m.Members[i].MemberId), out var data))
                        {
                            // Outside the sheet's source range for this person
                            cell.Value = "";
                            cell.Style.BackColor = SystemColors.ControlLight;
                            cell.ToolTipText = "Not part of this sheet's source range";
                            continue;
                        }

                        switch (data.Status)
                        {
                            case "Level":
                                cell.Value = data.Level;
                                PaintStyle(cell.Style, Look(data.SourceCell));   // level colour exactly as in the workbook
                                cell.ToolTipText = levelLabels.TryGetValue(data.Level!.Value, out var label)
                                    ? $"{label}\n{data.SourceCell}"
                                    : $"Level {data.Level} (not listed in this sheet's legend)\n{data.SourceCell}";
                                break;
                            case "Unassessed":
                                cell.Value = UnassessedText;
                                PaintStyle(cell.Style, Look(data.SourceCell));
                                cell.Style.ForeColor = SystemColors.GrayText;
                                cell.ToolTipText = $"Not assessed (blank in source)\n{data.SourceCell}";
                                break;
                            default: // Flagged
                                cell.Value = FlaggedText;
                                PaintStyle(cell.Style, Look(data.SourceCell));   // keep the workbook's fill; the ⚠ marks it
                                cell.Style.ForeColor = Color.DarkOrange;
                                cell.Style.Font = boldFont;
                                cell.ToolTipText = $"Flagged for review: {data.FlagReason}\n" +
                                                   $"Source value: {data.RawValue}\n{data.SourceCell}";
                                break;
                        }
                    }
                }
            }

            grid.ResumeLayout();
            // Back to the top when switching sheets (a frozen row can't be the first scrolling row)
            var firstScrollable = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => !r.Frozen);
            if (firstScrollable != null)
                grid.FirstDisplayedScrollingRowIndex = firstScrollable.Index;
            if (grid.Columns.Count > 2)
                grid.FirstDisplayedScrollingColumnIndex = 2;
            grid.CurrentCell = null;   // no misleading highlighted cell on load
            grid.ClearSelection();

            // Footer: cell key + counts on one line; full detail on hover / in Review notes
            var parts = new List<string>
            {
                $"{UnassessedText} not assessed (blank)",
                $"{FlaggedText} flagged (hover)",
                "grey = outside the sheet's range",
                UseWorkbookColours ? "colours as in the workbook" : "plain view",
                $"{m.Skills.Count} skills · {m.Members.Count} people",
            };
            if (hiddenCount > 0)
                parts.Add($"{hiddenCount} hidden row(s) " + (chkShowHidden.Checked ? "shown greyed" : "not shown"));
            if (m.ExcludedColumns.Count > 0)
                parts.Add($"{m.ExcludedColumns.Count} source column(s) not shown");
            if (_preview != null)
                parts.Add($"{_preview.ExcludedSheets.Count} sheet(s) not previewed");
            lblFooter.Text = string.Join("   ·   ", parts);

            var footerTip = new List<string>
            {
                $"{UnassessedText} = not assessed (blank in source)    {FlaggedText} = flagged value (hover the cell)    shaded = outside this sheet's source range",
                "Hover any cell for its exact workbook reference (e.g. Laboratory!C5) and the legend wording for its level.",
            };
            if (m.ExcludedColumns.Count > 0)
                footerTip.Add("Source columns not shown: " + string.Join("; ", m.ExcludedColumns.Select(x => $"{x.SourceHeaderText} [{x.SourceColumn}] - {x.Reason}")));
            footerTip.Add(_preview != null
                ? "PREVIEW of a local workbook copy - not an approved database import. Not previewed: " + string.Join("; ", _preview.ExcludedSheets)
                : "Read-only view of the approved import. The audited workbook remains the reference document.");
            toolTip.SetToolTip(lblFooter, string.Join("\n", footerTip));

            // Review notes (preview): items the workbook reader flagged on this sheet
            int noteCount = _preview != null ? _previewArea?.Warnings.Count ?? 0 : 0;
            btnNotes.Visible = _preview != null;
            btnNotes.Text = noteCount > 0 ? $"⚠ Review notes ({noteCount})" : "Review notes";
            btnNotes.ForeColor = noteCount > 0 ? Color.DarkOrange : SystemColors.ControlText;
            toolTip.SetToolTip(btnNotes, "Hidden rows, duplicate names, formulas, values outside ranges and other items flagged on this sheet.");
        }

        private void ShowEmpty(string message)
        {
            lblEmpty.Text = message;
            lblEmpty.Visible = true;
            layout.Visible = false;
        }

        private void ShowMatrixLayout()
        {
            lblEmpty.Visible = false;
            layout.Visible = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                toolTip.Dispose();
                _boldFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
