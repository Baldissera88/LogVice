namespace LogVice
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    // A card that shows the text for a ticket as it will be pasted.
    public sealed class TicketCard : Panel, IThemed, IMeasured
    {
        Theme theme = Theme.Light();
        public readonly TextBox Box = new TextBox();

        public TicketCard(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Box.Multiline = true;
            Box.ReadOnly = true;
            Box.WordWrap = true;
            Box.ScrollBars = ScrollBars.Vertical;
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Fonts.Mono;
            Box.Text = text;
            Box.Select(0, 0);
            Controls.Add(Box);
        }

        int TextW(int width)
        {
            return Math.Max(Dpi.S(120), width - Dpi.S(40));
        }

        public int Measure(int width)
        {
            int h = Draw.WrapHeight(Box.Text.Replace("\r\n", "\n"), Fonts.Mono, TextW(width)) + Fonts.Mono.Height;
            return Math.Max(Dpi.S(120), Math.Min(Dpi.S(430), h + Dpi.S(28)));
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Card;
            Box.BackColor = t.Card;
            Box.ForeColor = t.Text;
            try { NativeMethods.ScrollTheme(Box, t.IsDark); } catch (Exception) { }
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            Box.SetBounds(Dpi.S(20), Dpi.S(14), TextW(Width), Math.Max(Dpi.S(40), Height - Dpi.S(28)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : theme.Bg);
            Draw.Card(e.Graphics, ClientRectangle, theme);
        }
    }

    // Copy for ticket: the text that goes to the clipboard, shown first.
    public sealed class TicketForm : PageForm
    {
        readonly string text;

        public TicketForm(Theme t, string text, int rows, int codes) : base("Copy for ticket", t)
        {
            this.text = text;
            Size = new Size(Dpi.S(1000), Dpi.S(720));
            Add(new SectionTitle("Copy for ticket"));
            string what = rows == 1 ? "1 row" : rows.ToString("N0") + " rows";
            string more = codes == 0 ? "" : codes == 1 ? " and the error code in it" : " and the " + codes + " error codes in them";
            Add(new SettingRow(Glyph.Copy, "Plain text for a ticket or an e-mail", what + more + ". The text has no formatting, so it pastes the same everywhere.", null));
            Add(new TicketCard(text));
            FlatButton copy = Footer("Copy", true, delegate { CopyText(); });
            copyButton = copy;
        }

        FlatButton copyButton;

        void CopyText()
        {
            copyButton.Text = Menus.Copy(text) ? "Copied" : "Clipboard busy";
        }
    }
}
