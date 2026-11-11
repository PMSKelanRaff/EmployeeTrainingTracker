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
    /// Admin dashboard "Skills Matrix" tab: the Skills Matrix from the database - the editable LIVE copy
    /// (migration 005) when it is installed, otherwise the current approved import, read-only - or, in PREVIEW
    /// mode, a local workbook copy read in memory. No workbook is ever written.
    ///
    /// Editing (live copy, Admins): tick "Edit", select cells and type 1-5, or Delete for not assessed (or use the
    /// right-click menu). Changes stay highlighted and unsaved until Save, which writes them all at once with a
    /// change-log entry each, and refuses (saving nothing) if someone else changed one of the cells meanwhile.
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
        private const string GroupRowTag = "group-row";

        // Design sizes at scale 1.0. This view is added to the dashboard after ResponsiveScaler took its snapshot,
        // so it scales itself (ApplyScale) to the dashboard's current scale, like the dashboard's other grids.
        private const int LineHeight = 24;
        private const int BarHeight = 32;
        private const int GridHeaderHeight = 44;
        private const int GridRowHeight = 22;
        private const int SkillColWidth = 290;
        private const int InabColWidth = 92;
        private const int MemberColMinWidth = 92;
        private const int AreaComboWidth = 240;
        private const string BaseFontFamily = "Segoe UI";
        private const float BaseFontSize = 9F;

        private readonly int _adminEmployeeId;
        // true = the "Skills Matrix Import" tab (workbook preview, dry run, prepare/import; named users only);
        // false = the "Skills Matrix" tab (the matrix from the database: view and edit)
        private readonly bool _importTools;
        // true = the Manager dashboard's tab: the live copy only, limited to the people the manager manages
        private readonly bool _managerView;
        // Who is looking (live copy): Admins edit everyone; Managers their people except themselves
        private SkillsMatrixLiveService.Access? _access;
        private const string ImportStartText =
            "Skills Matrix import tools (restricted).\n\n" +
            "Open a workbook COPY with \"Open workbook copy (preview)…\" above to preview it, run a dry run with the employee " +
            "reconciliation, and prepare an import. Nothing is written until an import is confirmed.";
        private bool _loaded;
        private SkillMatrixImportInfo? _import;
        private SkillMatrixData? _matrix;

        // LIVE copy (migration 005): shown and editable when installed and seeded
        private bool _live;
        private sealed record PendingEdit(int? NewLevel, int ExpectedVersion, string OldText);
        private readonly Dictionary<(long SkillId, long MemberId), PendingEdit> _pending = new();
        private int _areaIndex = -1;
        private bool _suppressAreaChange;
        private bool _closeHooked;
        private static readonly Color PendingBack = Color.FromArgb(255, 230, 153);
        private bool EditActive => _live && _preview == null && chkEdit.Checked;
        private float _scale = 1f;
        private bool _scaleApplied;
        private bool _legendNeedsConfirmation;
        // Shared, cached font (never disposed here): re-created for the current scale
        private Font BoldFont => ResponsiveScaler.ScaledFont(BaseFontFamily, BaseFontSize, FontStyle.Bold, _scale);
        private int S(int designPixels) => (int)Math.Round(designPixels * _scale);

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
        // Live copy: people archived in the Employees tab are hidden unless this is ticked (then shown greyed, read-only)
        private readonly CheckBox chkShowArchived = new() { Text = "Show archived people", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 3, 3, 3), Visible = false };
        // Refresh: the sheet to show again after reloading
        private string? _restoreAreaName;
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
        // EDITING (live copy only)
        private readonly CheckBox chkEdit = new() { Text = "Edit", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 3, 3, 3), Visible = false };
        private readonly Button btnSave = new() { Text = "Save", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false, Enabled = false };
        private readonly Button btnDiscard = new() { Text = "Discard", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false, Enabled = false };
        private readonly Button btnHistory = new() { Text = "History…", AutoSize = true, Anchor = AnchorStyles.Left, Visible = false };
        private readonly Label lblPending = new() { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.DarkGoldenrod, Margin = new Padding(6, 0, 6, 0) };
        private readonly ContextMenuStrip cellMenu = new();
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

        public SkillsMatrixView(int adminEmployeeId, bool importTools = false, bool managerView = false)
        {
            _adminEmployeeId = adminEmployeeId;
            _importTools = importTools;
            _managerView = managerView && !importTools;
            // Sizes are set explicitly by ApplyScale; WinForms' own font auto-scaling would fight it
            AutoScaleMode = AutoScaleMode.None;
            lblPreviewBanner.BackColor = SystemColors.Info;

            // --- Matrix layout: header | legend | legend warning | grid | footer ---------------------
            var headerControls = new Control[]
            {
                new Label { Text = "Sheet:", AutoSize = true, Anchor = AnchorStyles.Left }, cmbArea, chkShowHidden, chkShowArchived, chkColour,
                btnNotes, btnDryRun, btnPrepare, chkEdit, btnSave, btnDiscard, lblPending, btnHistory, btnRefresh,
            };
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = headerControls.Length + 1, RowCount = 1, Margin = new Padding(0) };
            for (int i = 0; i < headerControls.Length; i++)
            {
                header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                header.Controls.Add(headerControls[i], i, 0);
            }
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.Controls.Add(lblSource, headerControls.Length, 0);

            // Legend row: the sheet's key cells as coloured chips (workbook colours) + the key wording / notes
            var legendRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            legendRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            legendRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            legendRow.Controls.Add(legendChips, 0, 0);
            legendRow.Controls.Add(lblLegend, 1, 0);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, BarHeight));   // header
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

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, BarHeight));    // top bar
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));            // preview banner (shown in preview)
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));          // content
            root.Controls.Add(topBar, 0, 0);
            root.Controls.Add(lblPreviewBanner, 0, 1);
            root.Controls.Add(content, 0, 2);
            Controls.Add(root);

            cmbArea.SelectedIndexChanged += (_, _) => OnAreaChanged();
            chkShowHidden.CheckedChanged += (_, _) => RenderMatrix(keepScroll: true);
            chkColour.CheckedChanged += (_, _) => { if (_matrix != null) { RenderLegend(_matrix.Legend); RenderMatrix(keepScroll: true); } };
            toolTip.SetToolTip(chkColour, "Show the workbook's own colours (level colours, INAB green/red, headings, key) exactly as Excel displays them. Untick for a plain view.");
            btnRefresh.Click += (_, _) =>
            {
                if (!ConfirmLeaveEdits()) return;
                bool wasPreview = _preview != null;
                ExitPreview();
                if (_importTools) { _matrix = null; ShowEmpty(ImportStartText); return; }   // "Close preview"
                // Stay on the sheet being viewed (by name: sheet IDs change when the live copy is rebuilt)
                _restoreAreaName = wasPreview ? null : (cmbArea.SelectedItem as SkillAreaInfo)?.Name;
                _loaded = false;
                EnsureLoaded();
            };
            chkShowArchived.CheckedChanged += (_, _) => LoadSelectedArea(keepScroll: true);
            toolTip.SetToolTip(chkShowArchived, "People archived in the Employees tab are hidden from the Skills Matrix; their levels are kept. " +
                                                "Tick to show them (greyed, read-only).");
            btnPreview.Click += (_, _) => { if (ConfirmLeaveEdits()) OpenPreview(); };

            // Editing (live copy)
            chkEdit.CheckedChanged += (_, _) => OnEditToggled();
            btnSave.Click += (_, _) => SaveEdits();
            btnDiscard.Click += (_, _) => DiscardEdits(ask: true);
            btnHistory.Click += (_, _) => ShowHistory();
            grid.KeyPress += Grid_KeyPress;
            grid.KeyDown += Grid_KeyDown;
            grid.CellMouseDown += Grid_CellMouseDown;
            BuildCellMenu();
            toolTip.SetToolTip(chkEdit, "Edit levels: select one or more cells and type 1-5, or press Delete for 'not assessed' " +
                                        "(or right-click). Changes are highlighted until you Save. Every saved change is logged.");
            toolTip.SetToolTip(btnHistory, "Every change made to this sheet in the app: who, when, old and new value.");
            btnNotes.Click += (_, _) => ShowNotes();
            btnDryRun.Click += (_, _) => RunDryRun();
            btnPrepare.Click += (_, _) => PrepareImport();
            toolTip.SetToolTip(btnPrepare, "Build and check an import plan from the reviewed reconciliation CSV: shows exactly what would be written, " +
                                           "blocking errors and the acknowledgements needed. Nothing is written.");
            toolTip.SetToolTip(btnDryRun, "DRY RUN: report what an import of this workbook copy would contain, with an employee " +
                                          "reconciliation for review. Nothing is imported and the database is not used.");
            toolTip.SetToolTip(lblPreviewInfo, lblPreviewInfo.Text.Trim());

            // Follow the dashboard's scale: the dashboard rescales on window resize, which resizes this view
            Resize += (_, _) => ApplyScale();
            ParentChanged += (_, _) => ApplyScale();
            grid.Resize += (_, _) => FitColumns();

            ApplyScale(force: true);
            ShowEmpty(_importTools ? ImportStartText : "Open this tab to load the Skills Matrix.");
        }

        /// <summary>
        /// Sizes fonts, fixed row heights, grid header/row heights and column widths for the dashboard's current
        /// scale (ResponsiveScaler), so the matrix grows and shrinks with the window like the other grids.
        /// </summary>
        private void ApplyScale(bool force = false)
        {
            float scale = ResponsiveScaler.GetScale(this);
            if (!force && _scaleApplied && Math.Abs(scale - _scale) < 0.001f) return;
            _scale = scale;
            _scaleApplied = true;

            SuspendLayout();
            try
            {
                Font = ResponsiveScaler.ScaledFont(BaseFontFamily, BaseFontSize, FontStyle.Regular, scale);
                lblPreviewBanner.Font = BoldFont;
                lblEmpty.Font = ResponsiveScaler.ScaledFont(BaseFontFamily, 11F, FontStyle.Regular, scale);
                cmbArea.Width = S(AreaComboWidth);

                root.RowStyles[0].Height = _importTools ? S(BarHeight) : 0;   // preview button: import tab only
                root.RowStyles[RootBannerRow].Height = _preview != null ? S(LineHeight) : 0;
                layout.RowStyles[0].Height = S(BarHeight);
                layout.RowStyles[1].Height = S(LineHeight);
                layout.RowStyles[LayoutLegendWarningRow].Height = _legendNeedsConfirmation ? S(LineHeight) : 0;
                layout.RowStyles[4].Height = S(LineHeight);

                UIHelpers.ApplyGridFonts(grid, scale);
                grid.ColumnHeadersHeight = S(GridHeaderHeight);
                grid.RowTemplate.Height = S(GridRowHeight);
            }
            finally
            {
                ResumeLayout(true);
            }

            // Cell styles hold fonts and column widths of the previous scale: rebuild the sheet shown
            if (_matrix != null)
            {
                RenderLegend(_matrix.Legend);
                RenderMatrix(keepScroll: true);
            }
        }

        /// <summary>
        /// Person columns are at least their minimum width; when the window is wider than the sheet needs,
        /// they share the spare width so the grid fills the tab like the other grids.
        /// </summary>
        private void FitColumns()
        {
            int members = grid.Columns.Count - 2;
            if (members <= 0) return;

            int scrollBar = grid.Controls.OfType<VScrollBar>().Any(v => v.Visible) ? SystemInformation.VerticalScrollBarWidth : 0;
            int available = grid.ClientSize.Width - scrollBar - grid.Columns[0].Width - grid.Columns[1].Width - 2;
            int width = Math.Max(S(MemberColMinWidth), available / members);
            for (int i = 2; i < grid.Columns.Count; i++)
                if (grid.Columns[i].Width != width)
                    grid.Columns[i].Width = width;
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

            // Import tab: nothing is loaded from the database; the tools work on a workbook copy
            if (_importTools)
            {
                if (_preview == null)
                    ShowEmpty(SkillsMatrixService.IsImportUser(_adminEmployeeId) ? ImportStartText
                              : "The Skills Matrix import tools are restricted to named users.");
                return;
            }

            // Closing the dashboard with unsaved edits: ask first
            if (!_closeHooked && FindForm() is Form form)
            {
                form.FormClosing += (_, e) => { if (_pending.Count > 0 && !ConfirmLeaveEdits()) e.Cancel = true; };
                _closeHooked = true;
            }

            if (_managerView)
            {
                LoadManagerView();
                return;
            }

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

                // The editable live copy (migration 005) when installed and seeded; otherwise the import, read-only
                var live = SkillsMatrixLiveService.GetState(_adminEmployeeId);
                _live = live.Installed && live.BasedOnImportId != null;
                _access = _live ? SkillsMatrixLiveService.GetAccess(_adminEmployeeId) : null;

                var areas = _live ? SkillsMatrixLiveService.GetAreas(_adminEmployeeId)
                                  : SkillsMatrixService.GetAreas(_adminEmployeeId, import!.ImportId);
                if (areas.Count == 0)
                {
                    ShowEmpty("The current Skills Matrix contains no sheets.");
                    return;
                }

                string sourceText = $"{import!.SourceFileName}" +
                                    (string.IsNullOrWhiteSpace(import.SourceVersion) ? "" : $" ({import.SourceVersion})") +
                                    $"  ·  SHA-256 {import.SourceSha256[..12]}…  ·  imported {import.ImportedAt:yyyy-MM-dd}" +
                                    $"  ·  approved by {import.ApprovedBy}";
                if (!_live)
                    lblSource.Text = $"Source: {sourceText}  ·  read-only";
                else if (live.BasedOnImportId == import.ImportId)
                    lblSource.Text = $"Live copy of import {import.ImportId}: {sourceText}  ·  changes are logged";
                else
                    lblSource.Text = $"⚠ Live copy is based on import {live.BasedOnImportId}, but the current import is {import.ImportId} - " +
                                     "it has not been rebuilt from the new import yet";
                lblSource.ForeColor = _live && live.BasedOnImportId != import.ImportId ? Color.DarkOrange : SystemColors.GrayText;
                toolTip.SetToolTip(lblSource, $"Full SHA-256: {import.SourceSha256}" +
                                              (_live ? $"\nLive copy built from import {live.BasedOnImportId} on {live.SeededAt:yyyy-MM-dd HH:mm} by {live.SeededBy}" : "") +
                                              (string.IsNullOrWhiteSpace(import.Notes) ? "" : $"\n\nImport notes:\n{import.Notes}"));

                chkEdit.Visible = _live;
                btnHistory.Visible = _live;
                UpdateEditUi();

                ShowMatrixLayout();
                cmbArea.DisplayMember = nameof(SkillAreaInfo.Name);
                BindAreas(areas);
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

        /// <summary>Fills the sheet picker and shows a sheet: the one being viewed before a Refresh if it still exists, else the first.</summary>
        private void BindAreas(List<SkillAreaInfo> areas)
        {
            chkShowArchived.Visible = _live;
            int restore = _restoreAreaName == null ? -1 : areas.FindIndex(a => a.Name == _restoreAreaName);
            _restoreAreaName = null;
            _suppressAreaChange = true;
            try
            {
                cmbArea.DataSource = areas;
                if (restore >= 0) cmbArea.SelectedIndex = restore;
            }
            finally { _suppressAreaChange = false; }
            OnAreaChanged();
        }

        /// <summary>Manager dashboard: the live copy, showing (and letting them edit) only the people they manage.</summary>
        private void LoadManagerView()
        {
            try
            {
                var live = SkillsMatrixLiveService.GetState(_adminEmployeeId);
                if (!live.Installed || live.BasedOnImportId == null)
                {
                    ShowEmpty("The Skills Matrix is not available yet.");
                    return;
                }
                _live = true;
                _access = SkillsMatrixLiveService.GetAccess(_adminEmployeeId);
                _import = new SkillMatrixImportInfo(live.BasedOnImportId.Value, "", "", null, "", live.SeededAt ?? DateTime.MinValue, null);

                var areas = SkillsMatrixLiveService.GetAreas(_adminEmployeeId);
                if (areas.Count == 0)
                {
                    ShowEmpty("None of the people you manage are on the Skills Matrix yet.");
                    return;
                }

                lblSource.Text = "Skills Matrix  ·  the people you manage  ·  changes are logged";
                lblSource.ForeColor = SystemColors.GrayText;
                toolTip.SetToolTip(lblSource, "You can change the levels of the people in the groups you manage (not your own). " +
                                              "Every change is logged with your name; see History.");
                chkEdit.Visible = true;
                btnHistory.Visible = true;
                UpdateEditUi();

                ShowMatrixLayout();
                cmbArea.DisplayMember = nameof(SkillAreaInfo.Name);
                BindAreas(areas);
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
            // Import tools: active Admin on the import list only (a read-only check of the login)
            try
            {
                if (!_importTools) throw new UnauthorizedAccessException("The workbook preview is on the Skills Matrix Import tab.");
                SkillsMatrixService.EnsureImportUser(_adminEmployeeId);
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
            SetRowHeight(root, RootBannerRow, S(LineHeight));
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
            lblSource.ForeColor = SystemColors.GrayText;
            _pending.Clear();           // (the caller has already asked about unsaved edits)
            chkEdit.Checked = false;    // a workbook preview is never editable
            chkEdit.Visible = false;
            btnHistory.Visible = false;
            UpdateEditUi();
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

        /// <summary>Sheet picker changed: unsaved edits belong to the sheet shown, so ask first (Cancel keeps the sheet).</summary>
        private void OnAreaChanged()
        {
            if (_suppressAreaChange) return;
            if (_pending.Count > 0 && cmbArea.SelectedIndex != _areaIndex && !ConfirmLeaveEdits())
            {
                _suppressAreaChange = true;
                try { cmbArea.SelectedIndex = _areaIndex; }
                finally { _suppressAreaChange = false; }
                LoadSelectedArea(keepScroll: true);   // (after a refused save: show the current values, keep other edits)
                return;
            }
            _areaIndex = cmbArea.SelectedIndex;
            LoadSelectedArea();
        }

        private void LoadSelectedArea(bool keepScroll = false)
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
                _matrix = _live ? SkillsMatrixLiveService.GetMatrix(_adminEmployeeId, area.AreaId, chkShowArchived.Checked)
                                : SkillsMatrixService.GetMatrix(_adminEmployeeId, _import.ImportId, area.AreaId);
                _colours = ImportedColours(_matrix);
                chkColour.Visible = _colours != null;
                RenderLegend(_matrix.Legend);
                RenderMatrix(keepScroll);
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
            grid.ColumnHeadersHeight = S(GridHeaderHeight);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.RowTemplate.Height = S(GridRowHeight);
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
            _legendNeedsConfirmation = legend.NeedsConfirmation;
            SetRowHeight(layout, LayoutLegendWarningRow, legend.NeedsConfirmation ? S(LineHeight) : 0);
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
            if (d.Bold) style.Font = BoldFont;
        }

        private Dictionary<int, string> _levelLabels = new();

        private void RenderMatrix(bool keepScroll = false)
        {
            if (_matrix == null) return;
            var m = _matrix;
            bool workbookColours = UseWorkbookColours;
            var colours = workbookColours ? _colours : null;
            int keepRow = keepScroll ? grid.FirstDisplayedScrollingRowIndex : -1;
            int keepCol = keepScroll ? grid.FirstDisplayedScrollingColumnIndex : -1;

            grid.SuspendLayout();
            grid.Rows.Clear();
            grid.Columns.Clear();
            // Workbook colours: no app row banding, so unfilled cells are white as in Excel
            grid.AlternatingRowsDefaultCellStyle.BackColor = workbookColours ? Color.White : Color.FromArgb(245, 245, 245);

            var skillCol = new DataGridViewTextBoxColumn { Name = "Skill", HeaderText = "Skill", Width = S(SkillColWidth), Frozen = true, SortMode = DataGridViewColumnSortMode.NotSortable };
            skillCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.Columns.Add(skillCol);
            var inabCol = new DataGridViewTextBoxColumn { Name = "Inab", HeaderText = "INAB accred.", Width = S(InabColWidth), Frozen = true, SortMode = DataGridViewColumnSortMode.NotSortable, ToolTipText = "INAB accredited (as written on the sheet)" };
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
                    HeaderText = member.SourceHeaderText + (_live && member.EmployeeArchived ? " (archived)" : ""),
                    Width = S(MemberColMinWidth),
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

            _levelLabels = m.Legend.Entries
                .GroupBy(e => e.Level)
                .ToDictionary(g => g.Key, g => string.Join(" / ", g.Select(e => e.Label)));

            int hiddenCount = m.Skills.Count(s => s.HiddenInSource);
            var boldFont = BoldFont;

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
                groupRow.Cells[0].Value = _live ? "Team / manager" : "Team / group (from the sheet's headings)";
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
                grid.Rows[catRow].Tag = category;
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
                    row.Tag = skill;

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
                        PaintMemberCell(row.Cells[2 + i], skill, m.Members[i]);
                }
            }

            grid.ResumeLayout();
            FitColumns();
            // Back to the top when switching sheets (a frozen row can't be the first scrolling row);
            // after a save, reload or display change, stay where the user was
            var firstScrollable = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => !r.Frozen);
            if (keepRow >= 0 && keepRow < grid.Rows.Count && !grid.Rows[keepRow].Frozen)
                grid.FirstDisplayedScrollingRowIndex = keepRow;
            else if (firstScrollable != null)
                grid.FirstDisplayedScrollingRowIndex = firstScrollable.Index;
            if (keepCol >= 2 && keepCol < grid.Columns.Count)
                grid.FirstDisplayedScrollingColumnIndex = keepCol;
            else if (grid.Columns.Count > 2)
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
                : _live
                    ? "Live copy: levels changed in the app use the colour of that level in the sheet's key and show who changed them (hover). " +
                      "Every change is in History."
                    : "Read-only view of the approved import. The audited workbook remains the reference document.");
            toolTip.SetToolTip(lblFooter, string.Join("\n", footerTip));

            // Review notes (preview): items the workbook reader flagged on this sheet
            int noteCount = _preview != null ? _previewArea?.Warnings.Count ?? 0 : 0;
            btnNotes.Visible = _preview != null;
            btnNotes.Text = noteCount > 0 ? $"⚠ Review notes ({noteCount})" : "Review notes";
            btnNotes.ForeColor = noteCount > 0 ? Color.DarkOrange : SystemColors.ControlText;
            toolTip.SetToolTip(btnNotes, "Hidden rows, duplicate names, formulas, values outside ranges and other items flagged on this sheet.");
        }

        /// <summary>Paints one person's cell for a skill: unsaved change, changed in the app, or as imported.</summary>
        private void PaintMemberCell(DataGridViewCell cell, SkillRowInfo skill, SkillMemberInfo member)
        {
            cell.Style = new DataGridViewCellStyle();   // reset: the cell may be repainted after an edit
            if (_matrix == null || !_matrix.Cells.TryGetValue((skill.SkillId, member.MemberId), out var data))
            {
                // Outside the sheet's source range for this person
                cell.Value = "";
                cell.Style.BackColor = SystemColors.ControlLight;
                cell.ToolTipText = "Not part of this sheet's source range" + (EditActive ? " (not editable)" : "");
                return;
            }

            if (_pending.TryGetValue((skill.SkillId, member.MemberId), out var pending))
            {
                string newText = pending.NewLevel is int nl ? LevelLabel(nl) : SkillsMatrixLiveService.NotAssessedText;
                cell.Value = pending.NewLevel?.ToString() ?? UnassessedText;
                cell.Style.BackColor = PendingBack;
                cell.Style.SelectionBackColor = ControlPaint.Dark(PendingBack, 0.1f);
                cell.Style.ForeColor = cell.Style.SelectionForeColor = Color.Black;
                cell.Style.Font = BoldFont;
                cell.ToolTipText = $"UNSAVED: {pending.OldText} -> {newText}\nSave to keep it, Discard to undo.";
                return;
            }

            string changed = data.UpdatedAt is DateTime at ? $"\nChanged in the app by {data.UpdatedByName} on {at:yyyy-MM-dd HH:mm}" : "";
            string where = string.IsNullOrEmpty(data.SourceCell) ? "" : $"\n{data.SourceCell}";
            if (member.EmployeeArchived && _live)
                where += "\n(archived in the Employees tab - read-only)";
            else if (EditActive && _access != null && !_access.CanEdit(member.EmployeeId))
                where += member.EmployeeId == _access.EmployeeId ? "\n(your own level - read-only)" : "\n(read-only)";
            switch (data.Status)
            {
                case "Level":
                    cell.Value = data.Level;
                    // As imported: the cell's colour exactly as in the workbook. Changed in the app: the colour of that
                    // level in the sheet's own key, so it follows the workbook's colour standard.
                    PaintStyle(cell.Style, data.UpdatedAt != null ? LevelColour(data.Level!.Value) : Look(data.SourceCell));
                    cell.ToolTipText = LevelLabel(data.Level!.Value) + changed + where;
                    break;
                case "Unassessed":
                    cell.Value = UnassessedText;
                    if (data.UpdatedAt == null) PaintStyle(cell.Style, Look(data.SourceCell));
                    cell.Style.ForeColor = SystemColors.GrayText;
                    cell.ToolTipText = (data.UpdatedAt == null ? "Not assessed (blank in source)" : "Not assessed") + changed + where;
                    break;
                default: // Flagged
                    cell.Value = FlaggedText;
                    PaintStyle(cell.Style, Look(data.SourceCell));   // keep the workbook's fill; the ⚠ marks it
                    cell.Style.ForeColor = Color.DarkOrange;
                    cell.Style.Font = BoldFont;
                    cell.ToolTipText = $"Flagged for review: {data.FlagReason}\nSource value: {data.RawValue}{where}" +
                                       (EditActive ? "\nType a level (1-5) or Delete to resolve it." : "");
                    break;
            }
            if (member.EmployeeArchived && _live)
                cell.Style.ForeColor = cell.Style.SelectionForeColor = SystemColors.GrayText;   // archived: shown only on request
        }

        private string LevelLabel(int level) =>
            _levelLabels.TryGetValue(level, out var label) ? label : $"Level {level} (not listed in this sheet's legend)";

        /// <summary>The colours of a level's key cell on this sheet (workbook colours on), or null.</summary>
        private SkillsMatrixWorkbookColours.CellDisplay? LevelColour(int level)
        {
            if (!UseWorkbookColours) return null;
            var key = _colours!.Legend.Where(k => k.Level == level).Select(k => Look(k.Ref)).FirstOrDefault(d => d != null);
            return key;
        }

        // ------------------------------------------------------------------------------------------
        // EDITING (live copy): pending changes are kept here until Save; Save writes them all at once.

        private void OnEditToggled()
        {
            if (!chkEdit.Checked && _pending.Count > 0 && !ConfirmLeaveEdits())
            {
                chkEdit.Checked = true;   // stay in edit mode (Cancel)
                return;
            }
            UpdateEditUi();
            RenderMatrix(keepScroll: true);
        }

        private void UpdateEditUi()
        {
            bool edit = EditActive;
            btnSave.Visible = btnDiscard.Visible = edit || _pending.Count > 0;
            btnSave.Enabled = btnDiscard.Enabled = _pending.Count > 0;
            lblPending.Text = _pending.Count > 0 ? $"{_pending.Count} unsaved change{(_pending.Count == 1 ? "" : "s")}"
                            : edit ? "Editing: select cells, type 1-5 or Delete" : "";
            lblPending.ForeColor = _pending.Count > 0 ? Color.DarkGoldenrod : SystemColors.GrayText;
        }

        private bool TryEditable(DataGridViewCell cell, out SkillRowInfo skill, out SkillMemberInfo member, out SkillCellInfo data)
        {
            skill = null!; member = null!; data = null!;
            if (!EditActive || _matrix == null || cell.ColumnIndex < 2 || cell.RowIndex < 0) return false;
            if (grid.Rows[cell.RowIndex].Tag is not SkillRowInfo s) return false;
            int i = cell.ColumnIndex - 2;
            if (i >= _matrix.Members.Count) return false;
            if (_access != null && !_access.CanEdit(_matrix.Members[i].EmployeeId)) return false;   // e.g. a manager's own column
            if (_matrix.Members[i].EmployeeArchived) return false;                                  // archived: read-only
            if (!_matrix.Cells.TryGetValue((s.SkillId, _matrix.Members[i].MemberId), out var d)) return false;
            skill = s; member = _matrix.Members[i]; data = d;
            return true;
        }

        /// <summary>Sets every selected editable cell to a level (null = not assessed), as an unsaved change.</summary>
        private void SetLevelOnSelection(int? level)
        {
            foreach (DataGridViewCell cell in grid.SelectedCells)
            {
                if (!TryEditable(cell, out var skill, out var member, out var data)) continue;
                var key = (skill.SkillId, member.MemberId);
                bool unchanged = level.HasValue ? data.Status == "Level" && data.Level == level : data.Status == "Unassessed";
                if (unchanged) _pending.Remove(key);
                else _pending[key] = new PendingEdit(level, data.Version, SkillsMatrixLiveService.DescribeValue(data.Status, data.Level, data.RawValue));
                PaintMemberCell(cell, skill, member);
            }
            UpdateEditUi();
        }

        private void UndoSelection()
        {
            foreach (DataGridViewCell cell in grid.SelectedCells)
                if (TryEditable(cell, out var skill, out var member, out _) && _pending.Remove((skill.SkillId, member.MemberId)))
                    PaintMemberCell(cell, skill, member);
            UpdateEditUi();
        }

        private void Grid_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!EditActive) return;
            if (e.KeyChar is >= '1' and <= '5') { SetLevelOnSelection(e.KeyChar - '0'); e.Handled = true; }
            else if (e.KeyChar is '0' or '-' or ' ') { SetLevelOnSelection(null); e.Handled = true; }
        }

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!EditActive) return;
            if (e.KeyCode is Keys.Delete or Keys.Back && !e.Control) { SetLevelOnSelection(null); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { SaveEdits(); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { UndoSelection(); e.Handled = e.SuppressKeyPress = true; }
        }

        private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (!EditActive || e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 2) return;
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            if (!cell.Selected)
            {
                grid.ClearSelection();
                cell.Selected = true;
            }
            if (grid.SelectedCells.Cast<DataGridViewCell>().Any(c => TryEditable(c, out _, out _, out _)))
                cellMenu.Show(Cursor.Position);
        }

        private void BuildCellMenu()
        {
            cellMenu.Opening += (_, _) =>
            {
                cellMenu.Items.Clear();
                for (int level = 1; level <= 5; level++)
                {
                    int l = level;
                    cellMenu.Items.Add(_levelLabels.TryGetValue(l, out var label) ? label : $"{l}", null, (_, _) => SetLevelOnSelection(l));
                }
                cellMenu.Items.Add("Not assessed", null, (_, _) => SetLevelOnSelection(null));
                cellMenu.Items.Add(new ToolStripSeparator());
                var undo = cellMenu.Items.Add("Undo unsaved change", null, (_, _) => UndoSelection());
                undo.Enabled = grid.SelectedCells.Cast<DataGridViewCell>().Any(c =>
                    TryEditable(c, out var s, out var m, out _) && _pending.ContainsKey((s.SkillId, m.MemberId)));
            };
        }

        /// <summary>Asks what to do with unsaved changes. Returns false when the user cancels (or a save failed).</summary>
        private bool ConfirmLeaveEdits()
        {
            if (_pending.Count == 0) return true;
            var answer = MessageBox.Show(this,
                $"You have {_pending.Count} unsaved change{(_pending.Count == 1 ? "" : "s")} on '{_matrix?.Area.Name}'.\n\nSave them now?",
                "Skills Matrix", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes) return SaveEdits();
            if (answer == DialogResult.No) { DiscardEdits(ask: false); return true; }
            return false;
        }

        /// <summary>Saves every unsaved change on the sheet shown, all at once. Returns true when saved.</summary>
        private bool SaveEdits()
        {
            if (_pending.Count == 0 || _matrix == null) return true;
            long areaId = _matrix.Area.AreaId;
            bool sameSheetSelected = cmbArea.SelectedIndex == _areaIndex;
            var changes = _pending.Select(kv => new SkillsMatrixLiveService.CellChange(
                kv.Key.SkillId, kv.Key.MemberId, kv.Value.ExpectedVersion, kv.Value.NewLevel)).ToList();

            SkillsMatrixLiveService.SaveResult result;
            try
            {
                Cursor = Cursors.WaitCursor;
                result = SkillsMatrixLiveService.SaveCells(_adminEmployeeId, areaId, changes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Nothing was saved:\n{ex.Message}", "Skills Matrix", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (!result.Succeeded)
            {
                foreach (var c in result.Conflicts) _pending.Remove((c.SkillId, c.ColumnId));
                var lines = result.Conflicts.Take(10).Select(c =>
                    $"•  {c.SkillName} / {c.EmployeeName}: now {c.CurrentValue}" +
                    (c.ChangedBy != null ? $" (changed by {c.ChangedBy} at {c.ChangedAt:HH:mm})" : ""));
                MessageBox.Show(this,
                    "Nothing was saved: someone else changed " + (result.Conflicts.Count == 1 ? "this cell" : $"these {result.Conflicts.Count} cells") +
                    " after you opened the sheet.\n\n" + string.Join("\n", lines) +
                    (result.Conflicts.Count > 10 ? $"\n…and {result.Conflicts.Count - 10} more" : "") +
                    "\n\nThe sheet now shows their values. Your other changes are kept (still unsaved) - check them and Save again.",
                    "Skills Matrix - changed by someone else", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (sameSheetSelected) LoadSelectedArea(keepScroll: true);
                UpdateEditUi();
                return false;
            }

            _pending.Clear();
            if (sameSheetSelected) LoadSelectedArea(keepScroll: true);
            UpdateEditUi();
            lblPending.Text = $"Saved {result.Saved} change{(result.Saved == 1 ? "" : "s")}";
            return true;
        }

        private void DiscardEdits(bool ask)
        {
            if (_pending.Count == 0) return;
            if (ask && MessageBox.Show(this, $"Discard {_pending.Count} unsaved change{(_pending.Count == 1 ? "" : "s")}?", "Skills Matrix",
                                       MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _pending.Clear();
            RenderMatrix(keepScroll: true);
            UpdateEditUi();
        }

        /// <summary>The sheet's change log, newest first, with "Undo selected change" for level changes.</summary>
        private void ShowHistory()
        {
            if (_matrix == null || !_live) return;
            if (!ConfirmLeaveEdits()) return;   // an undo reloads the sheet: deal with unsaved edits first

            string sheet = _matrix.Area.Name;
            long areaId = _matrix.Area.AreaId;
            var changes = new List<SkillsMatrixLiveService.ChangeEntry>();

            using var dialog = new Form
            {
                Size = new Size(1150, 580),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                ShowInTaskbar = false,
            };
            var list = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            };
            UIHelpers.StyleDataGridView(list);
            var btnUndo = new Button { Text = "Undo selected change", AutoSize = true, Enabled = false };
            var lblHint = new Label
            {
                AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 8, 3, 3),
                Text = "Undo puts the cell back to its 'From' value (only if it hasn't been changed again since). The undo is logged too.",
            };
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(4) };
            bottom.Controls.Add(btnUndo);
            bottom.Controls.Add(lblHint);
            dialog.Controls.Add(list);
            dialog.Controls.Add(bottom);

            SkillsMatrixLiveService.ChangeEntry? Selected() =>
                list.CurrentRow is { Index: var i } && i >= 0 && i < changes.Count ? changes[i] : null;

            bool Reload()
            {
                try
                {
                    changes = SkillsMatrixLiveService.GetChanges(_adminEmployeeId, areaId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Could not load the history:\n{ex.Message}", "Skills Matrix", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                dialog.Text = $"History - {sheet} ({changes.Count} change{(changes.Count == 1 ? "" : "s")}, newest first)";
                list.DataSource = changes.Select(c => new
                {
                    When = c.ChangedAt.ToString("yyyy-MM-dd HH:mm"),
                    By = c.ChangedBy,
                    Change = c.Action switch { "SetCell" => "Level changed", "Reseed" => "Rebuilt from import", _ => c.Action },
                    Skill = c.SkillName ?? "",
                    Person = c.EmployeeName ?? "",
                    From = c.OldValue ?? "",
                    To = c.NewValue ?? "",
                    Note = c.Note ?? "",
                }).ToList();
                return true;
            }

            list.SelectionChanged += (_, _) => btnUndo.Enabled = Selected() is { Action: "SetCell" };
            btnUndo.Click += (_, _) =>
            {
                if (Selected() is not { Action: "SetCell" } change) return;
                if (MessageBox.Show(dialog,
                        $"Put '{change.SkillName}' / {change.EmployeeName} back from {change.NewValue} to {change.OldValue}?",
                        "Undo change", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                try
                {
                    var result = SkillsMatrixLiveService.UndoChange(_adminEmployeeId, change.ChangeId);
                    if (!result.Succeeded)
                        MessageBox.Show(dialog, "Nothing was changed: the cell was changed by someone else just now. Check the history and try again.",
                                        "Undo change", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(dialog, $"Nothing was changed:\n{ex.Message}", "Undo change", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                Reload();
                LoadSelectedArea(keepScroll: true);
            };

            if (!Reload()) return;
            dialog.ShowDialog(this);
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
                toolTip.Dispose();   // fonts come from ResponsiveScaler's shared cache and are not disposed here
                cellMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
