using System.Runtime.CompilerServices;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// Scales a form's fonts, control sizes and margins proportionally as the window grows or shrinks.
    /// The layout captured at attach time (the designer / windowed size) is scale 1.0; anchoring
    /// behaviour from the designer is preserved, just with scaled sizes and margins.
    /// </summary>
    public sealed class ResponsiveScaler
    {
        // Limits so the UI never becomes unreadably small or comically large
        private const float MinScale = 0.8f;
        private const float MaxScale = 1.6f;
        // Quantise the scale so dragging the window edge doesn't create a new font every pixel
        private const float ScaleStep = 0.05f;

        private static readonly ConditionalWeakTable<Form, ResponsiveScaler> Attached = new();
        private static readonly Dictionary<(string, float, FontStyle, GraphicsUnit), Font> FontCache = new();

        private readonly Form _form;
        private readonly Size _baseClientSize;
        private readonly Dictionary<Control, Snapshot> _snapshots = new();
        private float _scale = 1f;
        private bool _applying;

        private sealed class Snapshot
        {
            public Rectangle Bounds;
            public Size ParentSize;
            public AnchorStyles Anchor;
            public Font Font = null!;
            public int RowTemplateHeight;
        }

        private ResponsiveScaler(Form form)
        {
            _form = form;
            _baseClientSize = form.ClientSize;
            Capture(form);
            form.Resize += (_, _) => Apply();
        }

        /// <summary>Call once, straight after InitializeComponent().</summary>
        public static void Attach(Form form)
        {
            if (!Attached.TryGetValue(form, out _))
                Attached.Add(form, new ResponsiveScaler(form));
        }

        /// <summary>Current scale of the form that owns this control (1.0 if not attached).</summary>
        public static float GetScale(Control control)
        {
            var form = control.FindForm();
            return form != null && Attached.TryGetValue(form, out var scaler) ? scaler._scale : 1f;
        }

        public static Font ScaledFont(string family, float baseSize, FontStyle style, float scale,
            GraphicsUnit unit = GraphicsUnit.Point)
        {
            float size = MathF.Round(baseSize * scale * 4f) / 4f;
            var key = (family, size, style, unit);
            if (!FontCache.TryGetValue(key, out var font))
            {
                font = new Font(family, size, style, unit);
                FontCache[key] = font;
            }
            return font;
        }

        private void Capture(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                var snap = new Snapshot
                {
                    Bounds = child.Bounds,
                    ParentSize = ParentClientSize(child),
                    Anchor = child.Anchor,
                    Font = child.Font,
                };
                if (child is DataGridView dgv)
                    snap.RowTemplateHeight = dgv.RowTemplate.Height;
                _snapshots[child] = snap;

                // We position these ourselves from now on, so stop the anchor engine moving them too
                if (IsPositionedByUs(child))
                    child.Anchor = AnchorStyles.Top | AnchorStyles.Left;

                if (IsLayoutContainer(child))
                {
                    Capture(child);
                    // Hidden tab pages (and the split panels inside them) only resize when shown, after the
                    // form's Resize has already run, so re-position their contents whenever they change size
                    var container = child;
                    container.Resize += (_, _) =>
                    {
                        if (!_applying)
                            ApplyBounds(container);
                    };
                }
            }
        }

        private void Apply()
        {
            if (_form.WindowState == FormWindowState.Minimized || _form.ClientSize.Width <= 0 || _form.ClientSize.Height <= 0)
                return;

            float raw = Math.Min(
                (float)_form.ClientSize.Width / _baseClientSize.Width,
                (float)_form.ClientSize.Height / _baseClientSize.Height);
            float scale = Math.Clamp(MathF.Round(raw / ScaleStep) * ScaleStep, MinScale, MaxScale);
            bool fontsChanged = Math.Abs(scale - _scale) > 0.001f;
            _scale = scale;

            _applying = true;
            _form.SuspendLayout();
            try
            {
                // Fonts first: they change tab header heights, which changes the space available to tab pages
                if (fontsChanged)
                    ApplyFonts(_form);
                ApplyBounds(_form);
            }
            finally
            {
                // Cleared first so containers resized by this final layout pass still re-position their contents
                _applying = false;
                _form.ResumeLayout(true);
            }
        }

        private void ApplyFonts(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (!_snapshots.TryGetValue(child, out var snap))
                    continue;

                // Changing a ContainerControl's font triggers WinForms auto-scaling of its children, so leave those alone
                if (child is not ContainerControl)
                    child.Font = ScaledFont(snap.Font.FontFamily.Name, snap.Font.Size, snap.Font.Style, _scale, snap.Font.Unit);

                if (child is DataGridView dgv)
                {
                    UIHelpers.ApplyGridFonts(dgv, _scale);
                    int rowHeight = (int)Math.Round(snap.RowTemplateHeight * _scale);
                    dgv.RowTemplate.Height = rowHeight;
                    foreach (DataGridViewRow row in dgv.Rows)
                        row.Height = rowHeight;
                }

                if (IsLayoutContainer(child))
                    ApplyFonts(child);
            }
        }

        private void ApplyBounds(Control parent)
        {
            parent.SuspendLayout();
            foreach (Control child in parent.Controls)
            {
                if (_snapshots.TryGetValue(child, out var snap) && IsPositionedByUs(child))
                    child.Bounds = ScaledBounds(snap, ParentClientSize(child));

                if (IsLayoutContainer(child))
                    ApplyBounds(child);
            }
            parent.ResumeLayout(false);
        }

        private Rectangle ScaledBounds(Snapshot snap, Size parentSize)
        {
            Rectangle b = snap.Bounds;
            var (x, w) = ScaleAxis(b.Left, b.Width, snap.ParentSize.Width, parentSize.Width,
                snap.Anchor.HasFlag(AnchorStyles.Left), snap.Anchor.HasFlag(AnchorStyles.Right));
            var (y, h) = ScaleAxis(b.Top, b.Height, snap.ParentSize.Height, parentSize.Height,
                snap.Anchor.HasFlag(AnchorStyles.Top), snap.Anchor.HasFlag(AnchorStyles.Bottom));
            return new Rectangle(x, y, Math.Max(w, 1), Math.Max(h, 1));
        }

        // Mirrors WinForms anchoring along one axis, with the design-time margins and size multiplied by the scale
        private (int pos, int size) ScaleAxis(int pos0, int size0, int parent0, int parent, bool nearAnchor, bool farAnchor)
        {
            float near = pos0 * _scale;
            float far = (parent0 - pos0 - size0) * _scale;
            float size = size0 * _scale;

            if (nearAnchor && farAnchor)
                return ((int)near, (int)(parent - near - far));
            if (farAnchor)
                return ((int)(parent - far - size), (int)size);
            if (nearAnchor)
                return ((int)near, (int)size);

            // Unanchored: keep the control's centre at the same relative position
            float centre = (pos0 + size0 / 2f) / parent0 * parent;
            return ((int)(centre - size / 2f), (int)size);
        }

        // Only walk into real layout containers; controls like DataGridView manage their own child
        // controls (e.g. its scrollbars) and must not have them repositioned
        private static bool IsLayoutContainer(Control c) =>
            c is TabControl or Panel or GroupBox or SplitContainer;

        private static bool IsPositionedByUs(Control c) =>
            c.Dock == DockStyle.None && c is not TabPage && c is not SplitterPanel;

        // Unselected tab pages aren't resized until they're shown, so measure against the tab control instead
        private static Size ParentClientSize(Control c) =>
            c.Parent is TabPage { Parent: TabControl tabs }
                ? tabs.DisplayRectangle.Size
                : c.Parent?.ClientSize ?? Size.Empty;
    }
}
