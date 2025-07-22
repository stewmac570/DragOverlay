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

            this.SuspendLayout();

            // === Top Button Panel ===
            this.topButtonPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.topButtonPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topButtonPanel.Height = 60;
            this.topButtonPanel.Padding = new System.Windows.Forms.Padding(10, 5, 0, 5);
            this.topButtonPanel.AutoSize = true;
            this.topButtonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
//-------------------------------------
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
