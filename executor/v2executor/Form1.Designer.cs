namespace v2executor
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            button1 = new RoundBtn();
            button2 = new RoundBtn();
            button3 = new RoundBtn();
            button4 = new RoundBtn();
            button5 = new RoundBtn();
            button6 = new RoundBtn();
            button7 = new RoundBtn();
            richTextBox1 = new RichTextBox();
            _statusLabel = new Label();
            SuspendLayout();

            richTextBox1.Location = new Point(52, 38);
            richTextBox1.Size = new Size(748, 406);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.TabIndex = 0;
            richTextBox1.Text = "";
            richTextBox1.ScrollBars = RichTextBoxScrollBars.Vertical;


            button3.Location = new Point(6, 138);
            button3.Size = new Size(40, 40);
            button3.Name = "button3";
            button3.TabIndex = 3;
            button3.Text = "💾";
            button3.Click += button3_Click;

            button4.Location = new Point(6, 186);
            button4.Size = new Size(40, 40);
            button4.Name = "button4";
            button4.TabIndex = 4;
            button4.Text = "📂";
            button4.Click += button4_Click;

            button5.Location = new Point(6, 234);
            button5.Size = new Size(40, 40);
            button5.Name = "button5";
            button5.TabIndex = 5;
            button5.Text = "🔍";
            button5.Click += button5_Click;

            button6.Location = new Point(6, 282);
            button6.Size = new Size(40, 40);
            button6.Name = "button6";
            button6.TabIndex = 6;
            button6.Text = "🗑";
            button6.Click += button6_Click;

            button7.Location = new Point(6, 330);
            button7.Size = new Size(40, 40);
            button7.Name = "button7";
            button7.TabIndex = 7;
            button7.Text = "☾";
            button7.Click += button7_Click;

            button2.Location = new Point(452, 452);
            button2.Size = new Size(148, 44);
            button2.Name = "button2";
            button2.TabIndex = 2;
            button2.Text = "⚡  attach";
            button2.Click += button2_Click;

            button1.Location = new Point(608, 452);
            button1.Size = new Size(148, 44);
            button1.Name = "button1";
            button1.TabIndex = 1;
            button1.Text = "▶  execute";
            button1.Click += button1_Click;

            _statusLabel.Location = new Point(52, 464);
            _statusLabel.Size = new Size(380, 20);
            _statusLabel.Name = "_statusLabel";
            _statusLabel.TabIndex = 8;
            _statusLabel.Text = "not attached";
            _statusLabel.ForeColor = Theme.TextMuted;
            _statusLabel.Font = new Font("Segoe UI", 9f);
            _statusLabel.BackColor = Color.Transparent;

            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 504);
            FormBorderStyle = FormBorderStyle.None;
            Font = new Font("Segoe UI", 9F);
            Name = "Form1";
            Text = "bacon & eggs executor";
            Icon = (Icon)resources.GetObject("$this.Icon");
            Load += Form1_Load;

            Controls.Add(richTextBox1);
            Controls.Add(button1);
            Controls.Add(button2);
            Controls.Add(button3);
            Controls.Add(button4);
            Controls.Add(button5);
            Controls.Add(button6);
            Controls.Add(button7);
            Controls.Add(_statusLabel);

            ResumeLayout(false);
        }

        private RoundBtn button1;
        private RoundBtn button2;
        private RichTextBox richTextBox1;
        private RoundBtn button3;
        private RoundBtn button4;
        private RoundBtn button5;
        private RoundBtn button6;
        private RoundBtn button7;
        private Label _statusLabel;
    }
}