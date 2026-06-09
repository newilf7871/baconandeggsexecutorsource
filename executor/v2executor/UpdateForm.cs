// © BNE Softworks (github.com/newilf7871)
// chrome webview tabs from https://github.com/adamschwartz/chrome-tabs credit to him
// licensed under GNU GPLv3 (see LICENSE)
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace v2executor
{
    public partial class UpdateForm : Form
    {
        private Label labelStatus;
        private Label labelPercentage;
        private int _progress = 0;

        public UpdateForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.labelStatus = new Label();
            this.labelPercentage = new Label();
            this.SuspendLayout();

            this.labelStatus.AutoSize = true;
            this.labelStatus.ForeColor = Theme.TextPrimary;
            this.labelStatus.Location = new Point(30, 50);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new Size(100, 15);
            this.labelStatus.TabIndex = 1;
            this.labelStatus.Text = "initializing...";
            this.labelStatus.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            this.labelPercentage.ForeColor = Theme.TextPrimary;
            this.labelPercentage.Location = new Point(270, 50);
            this.labelPercentage.Name = "labelPercentage";
            this.labelPercentage.Size = new Size(100, 15);
            this.labelPercentage.TabIndex = 2;
            this.labelPercentage.Text = "0%";
            this.labelPercentage.TextAlign = ContentAlignment.TopRight;

            this.ClientSize = new Size(400, 150);
            this.Controls.Add(this.labelPercentage);
            this.Controls.Add(this.labelStatus);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "UpdateForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "updating v2executor";
            this.BackColor = Theme.Background;
            this.TopMost = true;
            this.DoubleBuffered = true;

            this.Paint += UpdateForm_Paint;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void UpdateForm_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var p = new Pen(Theme.Border, 1))
            {
                g.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
            }

            using (var b = new SolidBrush(Theme.Accent))
            {
                g.FillRectangle(b, 0, 0, this.Width, 3);
            }

            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var b = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString("bacon & eggs - update service", f, b, 12, 12);
            }

            int barX = 30, barY = 80, barW = 340, barH = 10;
            var trackRect = new Rectangle(barX, barY, barW, barH);
            using (var trackPath = RoundedRect(trackRect, barH / 2))
            using (var trackBrush = new SolidBrush(Theme.SurfaceAlt))
            {
                g.FillPath(trackBrush, trackPath);
            }

            if (_progress > 0)
            {
                int fillW = (int)(barW * (_progress / 100.0));
                if (fillW > 0)
                {
                    var fillRect = new Rectangle(barX, barY, fillW, barH);
                    using (var fillPath = RoundedRect(fillRect, barH / 2))
                    using (var fillBrush = new LinearGradientBrush(
                        new Point(barX, barY), new Point(barX + fillW, barY),
                        Theme.Accent, Color.FromArgb(139, 92, 246)))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void UpdateProgress(int percentage, string status)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateProgress(percentage, status)));
                return;
            }

            if (percentage >= 0 && percentage <= 100)
            {
                _progress = percentage;
                this.labelPercentage.Text = $"{percentage}%";
            }

            if (!string.IsNullOrEmpty(status))
            {
                this.labelStatus.Text = status;
            }

            this.Invalidate();
        }
    }
}
