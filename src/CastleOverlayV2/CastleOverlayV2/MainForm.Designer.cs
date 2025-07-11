namespace CastleOverlayV2
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private System.Windows.Forms.Button loadCsvButton;


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
            this.loadCsvButton.Location = new System.Drawing.Point(12, 420);
            this.loadCsvButton.Name = "loadCsvButton";
            this.loadCsvButton.Size = new System.Drawing.Size(120, 30);
            this.loadCsvButton.TabIndex = 1;
            this.loadCsvButton.Text = "Load CSV";
            this.loadCsvButton.UseVisualStyleBackColor = true;
            this.loadCsvButton.Click += new System.EventHandler(this.LoadCsvButton_Click);
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
