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

            // === Run 1 Buttons ===
            this.btnLoadRun1.Text = "Load Run 1";
            this.btnLoadRun1.AutoSize = true;
            this.btnLoadRun1.Click += new System.EventHandler(this.LoadRun1Button_Click);

            this.btnToggleRun1.Text = "Hide";
            this.btnToggleRun1.AutoSize = true;
            this.btnToggleRun1.Click += new System.EventHandler(this.ToggleRun1Button_Click);

            this.btnDeleteRun1.Text = "Delete";
            this.btnDeleteRun1.AutoSize = true;
            this.btnDeleteRun1.Click += new System.EventHandler(this.DeleteRun1Button_Click);

            this.topButtonPanel.Controls.Add(btnLoadRun1);
            this.topButtonPanel.Controls.Add(btnToggleRun1);
            this.topButtonPanel.Controls.Add(btnDeleteRun1);
            this.topButtonPanel.Controls.Add(Spacer());

            // === Run 2 Buttons ===
            this.btnLoadRun2.Text = "Load Run 2";
            this.btnLoadRun2.AutoSize = true;
            this.btnLoadRun2.Click += new System.EventHandler(this.LoadRun2Button_Click);

            this.btnToggleRun2.Text = "Hide";
            this.btnToggleRun2.AutoSize = true;
            this.btnToggleRun2.Click += new System.EventHandler(this.ToggleRun2Button_Click);

            this.btnDeleteRun2.Text = "Delete";
            this.btnDeleteRun2.AutoSize = true;
            this.btnDeleteRun2.Click += new System.EventHandler(this.DeleteRun2Button_Click);

            this.topButtonPanel.Controls.Add(btnLoadRun2);
            this.topButtonPanel.Controls.Add(btnToggleRun2);
            this.topButtonPanel.Controls.Add(btnDeleteRun2);
            this.topButtonPanel.Controls.Add(Spacer());

            // === Run 3 Buttons ===
            this.btnLoadRun3.Text = "Load Run 3";
            this.btnLoadRun3.AutoSize = true;
            this.btnLoadRun3.Click += new System.EventHandler(this.LoadRun3Button_Click);

            this.btnToggleRun3.Text = "Hide";
            this.btnToggleRun3.AutoSize = true;
            this.btnToggleRun3.Click += new System.EventHandler(this.ToggleRun3Button_Click);

            this.btnDeleteRun3.Text = "Delete";
            this.btnDeleteRun3.AutoSize = true;
            this.btnDeleteRun3.Click += new System.EventHandler(this.DeleteRun3Button_Click);

            this.topButtonPanel.Controls.Add(btnLoadRun3);
            this.topButtonPanel.Controls.Add(btnToggleRun3);
            this.topButtonPanel.Controls.Add(btnDeleteRun3);

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
            this.Text = "DragOverlay";



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
