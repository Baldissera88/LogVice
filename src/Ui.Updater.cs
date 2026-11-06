namespace LogVice
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    public sealed class UpdaterForm : Form
    {
        readonly Label heading, status;
        readonly ProgressBar bar;
        readonly Theme theme;
        bool done;

        public UpdaterForm(string version)
        {
            string pref = Settings.Get("Theme");
            theme = pref == "Dark" || (pref != "Light" && Theme.SystemPrefersDark()) ? Theme.Dark() : Theme.Light();
            Text = "LogVice update";
            Font = Fonts.Ui;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            Icon appIcon = Program.AppIcon();
            if (appIcon != null) Icon = appIcon;
            ClientSize = new Size(Dpi.S(460), Dpi.S(150));
            Padding = new Padding(Dpi.S(24), Dpi.S(20), Dpi.S(24), Dpi.S(22));
            BackColor = theme.Bg;
            ForeColor = theme.Text;

            heading = new Label();
            heading.Dock = DockStyle.Top;
            heading.Font = Fonts.UiSemibold;
            heading.Height = Fonts.UiSemibold.Height + Dpi.S(10);
            heading.Text = "Updating LogVice" + (string.IsNullOrEmpty(version) ? "" : " to " + version);
            heading.UseMnemonic = false;

            status = new Label();
            status.Dock = DockStyle.Top;
            status.Height = Fonts.Ui.Height * 2 + Dpi.S(8);
            status.ForeColor = theme.Text2;
            status.UseMnemonic = false;
            status.Text = "Starting…";

            bar = new ProgressBar();
            bar.Dock = DockStyle.Bottom;
            bar.Height = Dpi.S(6);
            bar.Style = ProgressBarStyle.Marquee;
            bar.MarqueeAnimationSpeed = 30;

            Controls.Add(status);
            Controls.Add(heading);
            Controls.Add(bar);
            FormClosing += delegate (object s, FormClosingEventArgs e)
            {
                if (!done && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            };
        }

        public void Status(string text)
        {
            Ui(delegate { status.Text = text; }, false);
        }

        public void Problem(string text)
        {
            Ui(delegate { MessageBox.Show(this, text, "LogVice update", MessageBoxButtons.OK, MessageBoxIcon.Warning); }, true);
        }

        public void Finish()
        {
            Ui(delegate
            {
                done = true;
                Close();
            }, false);
        }

        void Ui(Action a, bool wait)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                if (wait) Invoke(a);
                else BeginInvoke(a);
            }
            catch (InvalidOperationException) { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.StyleWindow(Handle, theme);
        }
    }
}
