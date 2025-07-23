namespace CastleOverlayV2
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private System.Windows.Forms.Button btnLoadRun1;
        private System.Windows.Forms.Button btnLoadRun2;
        private System.Windows.Forms.Button btnLoadRun3;
        private System.Windows.Forms.FlowLayoutPanel topButtonPanel;
        private System.Windows.Forms.Button btnToggleRun1;
        private System.Windows.Forms.Button btnToggleRun2;
        private System.Windows.Forms.Button btnToggleRun3;
        private System.Windows.Forms.Button btnDeleteRun1;
        private System.Windows.Forms.Button btnDeleteRun2;
        private System.Windows.Forms.Button btnDeleteRun3;
        private System.Windows.Forms.Button btnLoadRaceBox1;
        private System.Windows.Forms.Button btnLoadRaceBox2;
        private System.Windows.Forms.Button btnLoadRaceBox3;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.formsPlot1 = new ScottPlot.WinForms.FormsPlot();

            this.btnLoadRun1 = new System.Windows.Forms.Button();
            this.btnLoadRun2 = new System.Windows.Forms.Button();
            this.btnLoadRun3 = new System.Windows.Forms.Button();

            this.btnToggleRun1 = new System.Windows.Forms.Button();
            this.btnToggleRun2 = new System.Windows.Forms.Button();
            this.btnToggleRun3 = new System.Windows.Forms.Button();

            this.btnDeleteRun1 = new System.Windows.Forms.Button();
            this.btnDeleteRun2 = new System.Windows.Forms.Button();
            this.btnDeleteRun3 = new System.Windows.Forms.Button();

            this.topButtonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnLoadRaceBox1 = new System.Windows.Forms.Button();
            this.btnLoadRaceBox2 = new System.Windows.Forms.Button();
            this.btnLoadRaceBox3 = new System.Windows.Forms.Button();

            this.SuspendLayout();

            this.topButtonPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight; // ✅ buttons flow L→R
            this.topButtonPanel.Dock = System.Windows.Forms.DockStyle.Top;                       // ✅ sticks to top of form
            this.topButtonPanel.Height = 60;                                                    // ✅ gives initial height
            this.topButtonPanel.Padding = new System.Windows.Forms.Padding(10, 5, 0, 5);        // ✅ nice inner spacing
            this.topButtonPanel.AutoSize = true;                                                // ✅ grows to fit buttons
            this.topButtonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;// ✅ only grows as needed


            // === Run 1 Buttons ===
            this.btnLoadRun1.Text = "Load Run 1";
            this.btnToggleRun1.Text = "Hide";
            this.btnDeleteRun1.Text = "Delete";
            this.btnLoadRaceBox1.Text = "Load RaceBox 1";

            this.btnLoadRun1.AutoSize = true;
            this.btnToggleRun1.AutoSize = true;
            this.btnDeleteRun1.AutoSize = true;
            this.btnLoadRaceBox1.AutoSize = true;

            this.btnLoadRun1.Click += new System.EventHandler(this.LoadRun1Button_Click);
            this.btnToggleRun1.Click += new System.EventHandler(this.ToggleRun1Button_Click);
            this.btnDeleteRun1.Click += new System.EventHandler(this.DeleteRun1Button_Click);
            this.btnLoadRaceBox1.Click += new System.EventHandler(this.LoadRaceBox1Button_Click);


            // === Run 1 Panel ===
            var panelRun1 = new TableLayoutPanel();
            panelRun1.ColumnCount = 3;
            panelRun1.RowCount = 2;
            panelRun1.AutoSize = true;
            panelRun1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelRun1.Margin = new Padding(6);
            panelRun1.Padding = new Padding(4);
            panelRun1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun1.Controls.Add(this.btnLoadRun1, 0, 0);
            panelRun1.Controls.Add(this.btnToggleRun1, 1, 0);
            panelRun1.Controls.Add(this.btnDeleteRun1, 2, 0);
            panelRun1.Controls.Add(this.btnLoadRaceBox1, 0, 1);
            panelRun1.SetColumnSpan(this.btnLoadRaceBox1, 3);
            this.topButtonPanel.Controls.Add(panelRun1);
            this.topButtonPanel.Controls.Add(Spacer());

            // === Run 2 Buttons ===
            this.btnLoadRun2.Text = "Load Run 2";
            this.btnToggleRun2.Text = "Hide";
            this.btnDeleteRun2.Text = "Delete";
            this.btnLoadRaceBox2.Text = "Load RaceBox 2";

            this.btnLoadRun2.AutoSize = true;
            this.btnToggleRun2.AutoSize = true;
            this.btnDeleteRun2.AutoSize = true;
            this.btnLoadRaceBox2.AutoSize = true;

            this.btnLoadRun2.Click += new System.EventHandler(this.LoadRun2Button_Click);
            this.btnToggleRun2.Click += new System.EventHandler(this.ToggleRun2Button_Click);
            this.btnDeleteRun2.Click += new System.EventHandler(this.DeleteRun2Button_Click);
            this.btnLoadRaceBox2.Click += new System.EventHandler(this.LoadRaceBox2Button_Click);

            // === Run 2 Panel ===
            var panelRun2 = new TableLayoutPanel();
            panelRun2.ColumnCount = 3;
            panelRun2.RowCount = 2;
            panelRun2.AutoSize = true;
            panelRun2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelRun2.Margin = new Padding(6);
            panelRun2.Padding = new Padding(4);
            panelRun2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun2.Controls.Add(this.btnLoadRun2, 0, 0);
            panelRun2.Controls.Add(this.btnToggleRun2, 1, 0);
            panelRun2.Controls.Add(this.btnDeleteRun2, 2, 0);
            panelRun2.Controls.Add(this.btnLoadRaceBox2, 0, 1);
            panelRun2.SetColumnSpan(this.btnLoadRaceBox2, 3);
            this.topButtonPanel.Controls.Add(panelRun2);
            this.topButtonPanel.Controls.Add(Spacer());


            // === Run 3 Buttons ===
            this.btnLoadRun3.Text = "Load Run 3";
            this.btnToggleRun3.Text = "Hide";
            this.btnDeleteRun3.Text = "Delete";
            this.btnLoadRaceBox3.Text = "Load RaceBox 3";

            this.btnLoadRun3.AutoSize = true;
            this.btnToggleRun3.AutoSize = true;
            this.btnDeleteRun3.AutoSize = true;
            this.btnLoadRaceBox3.AutoSize = true;

            this.btnLoadRun3.Click += new System.EventHandler(this.LoadRun3Button_Click);
            this.btnToggleRun3.Click += new System.EventHandler(this.ToggleRun3Button_Click);
            this.btnDeleteRun3.Click += new System.EventHandler(this.DeleteRun3Button_Click);
            this.btnLoadRaceBox3.Click += new System.EventHandler(this.LoadRaceBox3Button_Click);

            // === Run 3 Panel ===
            var panelRun3 = new TableLayoutPanel();
            panelRun3.ColumnCount = 3;
            panelRun3.RowCount = 2;
            panelRun3.AutoSize = true;
            panelRun3.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelRun3.Margin = new Padding(6);
            panelRun3.Padding = new Padding(4);
            panelRun3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun3.Controls.Add(this.btnLoadRun3, 0, 0);
            panelRun3.Controls.Add(this.btnToggleRun3, 1, 0);
            panelRun3.Controls.Add(this.btnDeleteRun3, 2, 0);
            panelRun3.Controls.Add(this.btnLoadRaceBox3, 0, 1);
            panelRun3.SetColumnSpan(this.btnLoadRaceBox3, 3);
            this.topButtonPanel.Controls.Add(panelRun3);


            //---------------------------------------
            // === FormsPlot ===
            this.formsPlot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot1.Location = new System.Drawing.Point(0, 0);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(800, 460);
            this.formsPlot1.TabIndex = 0;

            // === MainForm ===
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 460);
            this.Controls.Add(this.formsPlot1);
            this.Controls.Add(this.topButtonPanel);
            this.Name = "MainForm";
            this.Text = "DragOverlay V1";



            this.ResumeLayout(false);
            this.PerformLayout();

            // === Spacer method (local function) ===
            Control Spacer()
            {
                return new Panel
                {
                    Width = 20,
                    Height = 1
                };
            }
        }

        #endregion
    }
}
