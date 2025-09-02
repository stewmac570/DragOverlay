// File: src/CastleOverlayV2/MainForm.Designer.cs
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
        private System.Windows.Forms.Button btnToggleRaceBox1;
        private System.Windows.Forms.Button btnDeleteRaceBox1;
        private System.Windows.Forms.Button btnToggleRaceBox2;
        private System.Windows.Forms.Button btnDeleteRaceBox2;
        private System.Windows.Forms.Button btnToggleRaceBox3;
        private System.Windows.Forms.Button btnDeleteRaceBox3;

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

            this.btnToggleRaceBox1 = new System.Windows.Forms.Button();
            this.btnDeleteRaceBox1 = new System.Windows.Forms.Button();
            this.btnToggleRaceBox2 = new System.Windows.Forms.Button();
            this.btnDeleteRaceBox2 = new System.Windows.Forms.Button();
            this.btnToggleRaceBox3 = new System.Windows.Forms.Button();
            this.btnDeleteRaceBox3 = new System.Windows.Forms.Button();

            this.SuspendLayout();

            // === Top panel ===
            this.topButtonPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.topButtonPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topButtonPanel.Height = 64;
            this.topButtonPanel.Padding = new System.Windows.Forms.Padding(12, 8, 0, 8); // ⬅ slightly more breathing room
            this.topButtonPanel.AutoSize = true;
            this.topButtonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

            // === Run 1 Buttons ===
            this.btnLoadRun1 = new System.Windows.Forms.Button();
            this.btnToggleRun1 = new System.Windows.Forms.Button();
            this.btnDeleteRun1 = new System.Windows.Forms.Button();
            this.btnLoadRaceBox1 = new System.Windows.Forms.Button();

            // --- Castle Run 1
            this.btnLoadRun1.Text = "Load Run 1";
            this.btnLoadRun1.AutoSize = false;                                 // fixed width
            this.btnLoadRun1.Size = new System.Drawing.Size(266, 30);          // ⬅ +33% from 200px → 266px
            this.btnLoadRun1.MinimumSize = new System.Drawing.Size(266, 30);   // enforce width
            this.btnToggleRun1.Text = "Hide";
            this.btnDeleteRun1.Text = "Delete";
            this.btnToggleRun1.AutoSize = true;
            this.btnDeleteRun1.AutoSize = true;
            this.btnLoadRun1.Click += new System.EventHandler(this.LoadRun1Button_Click);
            this.btnToggleRun1.Click += new System.EventHandler(this.ToggleRun1Button_Click);
            this.btnDeleteRun1.Click += new System.EventHandler(this.DeleteRun1Button_Click);

            // --- RaceBox Run 1
            this.btnLoadRaceBox1 = new System.Windows.Forms.Button();
            this.btnToggleRaceBox1 = new System.Windows.Forms.Button();
            this.btnDeleteRaceBox1 = new System.Windows.Forms.Button();

            this.btnLoadRaceBox1.Text = "Load RaceBox 1";
            this.btnLoadRaceBox1.AutoSize = false;                              // fixed width
            this.btnLoadRaceBox1.Size = new System.Drawing.Size(266, 30);       // ⬅ +33%
            this.btnLoadRaceBox1.MinimumSize = new System.Drawing.Size(266, 30);
            this.btnToggleRaceBox1.Text = "Hide";
            this.btnDeleteRaceBox1.Text = "Delete";
            this.btnToggleRaceBox1.AutoSize = true;
            this.btnDeleteRaceBox1.AutoSize = true;

            this.btnLoadRaceBox1.Click += new System.EventHandler(this.LoadRaceBox1Button_Click);
            this.btnToggleRaceBox1.Click += new System.EventHandler(this.ToggleRaceBox1Button_Click);
            this.btnDeleteRaceBox1.Click += new System.EventHandler(this.DeleteRaceBox1Button_Click);

            // === Run 1 Panel ===
            var panelRun1 = new TableLayoutPanel();
            panelRun1.ColumnCount = 3;
            panelRun1.RowCount = 2;
            panelRun1.AutoSize = true;
            panelRun1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelRun1.Margin = new Padding(16, 8, 16, 8); // ⬅ spaced out to match wider buttons
            panelRun1.Padding = new Padding(4);
            panelRun1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Row 0 — Castle
            panelRun1.Controls.Add(this.btnLoadRun1, 0, 0);
            panelRun1.Controls.Add(this.btnToggleRun1, 1, 0);
            panelRun1.Controls.Add(this.btnDeleteRun1, 2, 0);

            // Row 1 — RaceBox
            panelRun1.Controls.Add(this.btnLoadRaceBox1, 0, 1);
            panelRun1.Controls.Add(this.btnToggleRaceBox1, 1, 1);
            panelRun1.Controls.Add(this.btnDeleteRaceBox1, 2, 1);

            this.topButtonPanel.Controls.Add(panelRun1);
            this.topButtonPanel.Controls.Add(Spacer()); // gap between Run 1 and Run 2 groups

            // === Run 2 Buttons ===
            this.btnLoadRun2.Text = "Load Run 2";
            this.btnLoadRun2.AutoSize = false;
            this.btnLoadRun2.Size = new System.Drawing.Size(266, 30);         // ⬅ +33%
            this.btnLoadRun2.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRun2.Text = "Hide";
            this.btnDeleteRun2.Text = "Delete";
            this.btnToggleRun2.AutoSize = true;
            this.btnDeleteRun2.AutoSize = true;

            this.btnLoadRaceBox2.Text = "Load RaceBox 2";
            this.btnLoadRaceBox2.AutoSize = false;
            this.btnLoadRaceBox2.Size = new System.Drawing.Size(266, 30);      // ⬅ +33%
            this.btnLoadRaceBox2.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRaceBox2.Text = "Hide";
            this.btnDeleteRaceBox2.Text = "Delete";
            this.btnToggleRaceBox2.AutoSize = true;
            this.btnDeleteRaceBox2.AutoSize = true;

            this.btnLoadRun2.Click += new System.EventHandler(this.LoadRun2Button_Click);
            this.btnToggleRun2.Click += new System.EventHandler(this.ToggleRun2Button_Click);
            this.btnDeleteRun2.Click += new System.EventHandler(this.DeleteRun2Button_Click);
            this.btnLoadRaceBox2.Click += new System.EventHandler(this.LoadRaceBox2Button_Click);
            this.btnToggleRaceBox2.Click += new System.EventHandler(this.ToggleRaceBox2Button_Click);
            this.btnDeleteRaceBox2.Click += new System.EventHandler(this.DeleteRaceBox2Button_Click);

            // === Run 2 Panel ===
            var panelRun2 = new TableLayoutPanel();
            panelRun2.ColumnCount = 3;
            panelRun2.RowCount = 2;
            panelRun2.AutoSize = true;
            panelRun2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelRun2.Margin = new Padding(16, 8, 16, 8); // ⬅ spaced out
            panelRun2.Padding = new Padding(4);
            panelRun2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            panelRun2.Controls.Add(this.btnLoadRun2, 0, 0);
            panelRun2.Controls.Add(this.btnToggleRun2, 1, 0);
            panelRun2.Controls.Add(this.btnDeleteRun2, 2, 0);

            panelRun2.Controls.Add(this.btnLoadRaceBox2, 0, 1);
            panelRun2.Controls.Add(this.btnToggleRaceBox2, 1, 1);
            panelRun2.Controls.Add(this.btnDeleteRaceBox2, 2, 1);

            this.topButtonPanel.Controls.Add(panelRun2);
            this.topButtonPanel.Controls.Add(Spacer()); // gap between Run 2 and Run 3 groups

            // === Run 3 Buttons ===
            this.btnLoadRun3.Text = "Load Run 3";
            this.btnLoadRun3.AutoSize = false;
            this.btnLoadRun3.Size = new System.Drawing.Size(266, 30);         // ⬅ +33%
            this.btnLoadRun3.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRun3.Text = "Hide";
            this.btnDeleteRun3.Text = "Delete";
            this.btnToggleRun3.AutoSize = true;
            this.btnDeleteRun3.AutoSize = true;

            this.btnLoadRaceBox3.Text = "Load RaceBox 3";
            this.btnLoadRaceBox3.AutoSize = false;
            this.btnLoadRaceBox3.Size = new System.Drawing.Size(266, 30);      // ⬅ +33%
            this.btnLoadRaceBox3.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRaceBox3.Text = "Hide";
            this.btnDeleteRaceBox3.Text = "Delete";
            this.btnToggleRaceBox3.AutoSize = true;
            this.btnDeleteRaceBox3.AutoSize = true;

            this.btnLoadRun3.Click += new System.EventHandler(this.LoadRun3Button_Click);
            this.btnToggleRun3.Click += new System.EventHandler(this.ToggleRun3Button_Click);
            this.btnDeleteRun3.Click += new System.EventHandler(this.DeleteRun3Button_Click);
            this.btnLoadRaceBox3.Click += new System.EventHandler(this.LoadRaceBox3Button_Click);
            this.btnToggleRaceBox3.Click += new System.EventHandler(this.ToggleRaceBox3Button_Click);
            this.btnDeleteRaceBox3.Click += new System.EventHandler(this.DeleteRaceBox3Button_Click);

            // === Run 3 Panel ===
            var panelRun3 = new TableLayoutPanel();
            panelRun3.ColumnCount = 3;
            panelRun3.RowCount = 2;
            panelRun3.AutoSize = true;
            panelRun3.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelRun3.Margin = new Padding(16, 8, 16, 8); // ⬅ spaced out
            panelRun3.Padding = new Padding(4);
            panelRun3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panelRun3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            panelRun3.Controls.Add(this.btnLoadRun3, 0, 0);
            panelRun3.Controls.Add(this.btnToggleRun3, 1, 0);
            panelRun3.Controls.Add(this.btnDeleteRun3, 2, 0);

            panelRun3.Controls.Add(this.btnLoadRaceBox3, 0, 1);
            panelRun3.Controls.Add(this.btnToggleRaceBox3, 1, 1);
            panelRun3.Controls.Add(this.btnDeleteRaceBox3, 2, 1);

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
            System.Windows.Forms.Control Spacer()
            {
                return new System.Windows.Forms.Panel
                {
                    Width = 40,  // ⬅ wider gaps between run groups to suit wider buttons
                    Height = 1
                };
            }
        }

        #endregion
    }
}
