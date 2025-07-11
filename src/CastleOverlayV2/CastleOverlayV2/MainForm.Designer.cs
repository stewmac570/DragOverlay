namespace CastleOverlayV2
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private System.Windows.Forms.Button loadCsvButton;
        private System.Windows.Forms.Button btnLoadRun1;
        private System.Windows.Forms.Button btnLoadRun2;
        private System.Windows.Forms.Button btnLoadRun3;



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
            this.loadCsvButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // formsPlot1
            //
            this.formsPlot1.Location = new System.Drawing.Point(12, 12);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(760, 400);
            this.formsPlot1.TabIndex = 0;
            //
            // loadCsvButton
            //
            // Inside InitializeComponent()

// Load Run 1
this.btnLoadRun1 = new System.Windows.Forms.Button();
this.btnLoadRun1.Location = new System.Drawing.Point(10, 10);
this.btnLoadRun1.Name = "btnLoadRun1";
this.btnLoadRun1.Size = new System.Drawing.Size(100, 30);
this.btnLoadRun1.Text = "Load Run 1";
this.btnLoadRun1.UseVisualStyleBackColor = true;
this.btnLoadRun1.Click += new System.EventHandler(this.LoadRun1Button_Click);
this.Controls.Add(this.btnLoadRun1);

// Load Run 2
this.btnLoadRun2 = new System.Windows.Forms.Button();
this.btnLoadRun2.Location = new System.Drawing.Point(10, 50);
this.btnLoadRun2.Name = "btnLoadRun2";
this.btnLoadRun2.Size = new System.Drawing.Size(100, 30);
this.btnLoadRun2.Text = "Load Run 2";
this.btnLoadRun2.UseVisualStyleBackColor = true;
this.btnLoadRun2.Click += new System.EventHandler(this.LoadRun2Button_Click);
this.Controls.Add(this.btnLoadRun2);

// Load Run 3
this.btnLoadRun3 = new System.Windows.Forms.Button();
this.btnLoadRun3.Location = new System.Drawing.Point(10, 90);
this.btnLoadRun3.Name = "btnLoadRun3";
this.btnLoadRun3.Size = new System.Drawing.Size(100, 30);
this.btnLoadRun3.Text = "Load Run 3";
this.btnLoadRun3.UseVisualStyleBackColor = true;
this.btnLoadRun3.Click += new System.EventHandler(this.LoadRun3Button_Click);
this.Controls.Add(this.btnLoadRun3);

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 460);
            this.Controls.Add(this.loadCsvButton);
            this.Controls.Add(this.formsPlot1);
            this.Name = "MainForm";
            this.Text = "Castle Log Overlay Tool — Phase 1";
            this.ResumeLayout(false);
        }


        #endregion
    }
}
