namespace LogVice
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    public sealed class LookupForm : Form, IThemed
    {
        readonly FluentTextBox input;
        readonly CodePanel result;
        Theme theme = Theme.Light();
        const string Hint = "Type a code: a hex HRESULT (0x87D00324), a signed decimal as ConfigMgr prints it (-2016410844), or a Win32 or MSI exit code (1603).\r\n\r\nCodes outside the built-in table are looked up in the Windows message tables on this computer.";

        public LookupForm()
        {
            Text = "Error lookup";
            Font = Fonts.Ui;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            Size = new Size(Dpi.S(580), Dpi.S(380));
            MinimumSize = new Size(Dpi.S(380), Dpi.S(260));
            Padding = new Padding(Dpi.S(20), Dpi.S(16), Dpi.S(20), Dpi.S(16));

            input = new FluentTextBox(Glyph.Lookup, "0x87D00324, -2016410844 or 1603");
            input.Dock = DockStyle.Top;
            input.Box.Font = Fonts.Mono;
            Panel gap = new Panel();
            gap.Dock = DockStyle.Top;
            gap.Height = Dpi.S(16);
            result = new CodePanel();
            result.Dock = DockStyle.Fill;
            result.ShowDivider = false;
            result.Heading = "";
            result.Hint = Hint;
            Controls.Add(result);
            Controls.Add(gap);
            Controls.Add(input);
            input.TextChanged += delegate { ShowResult(); };
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Bg;
            foreach (Control c in Controls)
            {
                IThemed th = c as IThemed;
                if (th != null) th.ApplyTheme(t);
                else c.BackColor = t.Bg;
            }
            if (IsHandleCreated) NativeMethods.StyleWindow(Handle, t);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
        }

        void ShowResult()
        {
            uint? u = Codes.ParseCode(input.Text);
            input.Invalid = u == null && input.Text.Trim().Length > 0;
            result.Hint = input.Text.Trim().Length == 0 ? Hint : "Use hex (0x80070005), a signed decimal (-2147024891) or a Win32 or MSI code (1603).";
            List<uint> list = new List<uint>();
            if (u != null) list.Add(u.Value);
            result.SetCodes(list);
        }

        public void SetCode(string code)
        {
            input.Text = code;
        }

        public void FocusInput()
        {
            input.Box.Focus();
            input.Box.SelectAll();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Hide(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }

    public sealed class LineForm : Form
    {
        readonly FluentTextBox input;
        readonly FluentButton bGo;
        readonly Theme theme;
        public int Value;

        public LineForm(string file, Theme t)
        {
            theme = t;
            Text = "Go to line";
            Font = Fonts.Ui;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            BackColor = t.Bg;
            int x = Dpi.S(20), w = Dpi.S(340);
            Label l = new Label();
            l.AutoSize = false;
            l.Text = "Line in " + file;
            l.Font = Fonts.UiSemibold;
            l.ForeColor = t.Text;
            l.BackColor = t.Bg;
            l.AutoEllipsis = true;
            l.SetBounds(x, Dpi.S(16), w, Fonts.UiSemibold.Height + Dpi.S(4));
            input = new FluentTextBox(Glyph.Rows, "Line number");
            input.Box.Font = Fonts.Mono;
            bGo = new FluentButton(ButtonKind.Accent, Glyph.None, "Go");
            bGo.Width = bGo.GetPreferredSize(Size.Empty).Width;
            int y = Dpi.S(16) + Fonts.UiSemibold.Height + Dpi.S(10);
            input.SetBounds(x, y, w - bGo.Width - Dpi.S(8), Dpi.S(32));
            bGo.SetBounds(x + w - bGo.Width, y, bGo.Width, Dpi.S(32));
            Controls.Add(l);
            Controls.Add(input);
            Controls.Add(bGo);
            ClientSize = new Size(w + x * 2, y + Dpi.S(32) + Dpi.S(20));
            input.ApplyTheme(t);
            bGo.ApplyTheme(t);
            bGo.Enabled = false;
            input.TextChanged += delegate
            {
                int v;
                bool ok = int.TryParse(input.Text.Trim().Replace(",", "").Replace(".", ""), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;
                input.Invalid = !ok && input.Text.Trim().Length > 0;
                bGo.Enabled = ok;
                if (ok) Value = v;
            };
            bGo.Click += delegate { if (bGo.Enabled) { DialogResult = DialogResult.OK; Close(); } };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            input.Box.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            if (keyData == Keys.Enter && bGo.Enabled) { DialogResult = DialogResult.OK; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
