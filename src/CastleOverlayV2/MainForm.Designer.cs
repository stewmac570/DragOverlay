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
        private System.Windows.Forms.TableLayoutPanel mainContainer;   // <— NEW


        // 🆕 Per-slot time-shift controls (Castle 1–3, RaceBox 1–3)
        private System.Windows.Forms.Button btnShiftLeftRun1;
        private System.Windows.Forms.Button btnShiftRightRun1;
        private System.Windows.Forms.Button btnShiftResetRun1;

        private System.Windows.Forms.Button btnShiftLeftRun2;
        private System.Windows.Forms.Button btnShiftRightRun2;
        private System.Windows.Forms.Button btnShiftResetRun2;

        private System.Windows.Forms.Button btnShiftLeftRun3;
        private System.Windows.Forms.Button btnShiftRightRun3;
        private System.Windows.Forms.Button btnShiftResetRun3;

        private System.Windows.Forms.Button btnShiftLeftRB1;
        private System.Windows.Forms.Button btnShiftRightRB1;
        private System.Windows.Forms.Button btnShiftResetRB1;

        private System.Windows.Forms.Button btnShiftLeftRB2;
        private System.Windows.Forms.Button btnShiftRightRB2;
        private System.Windows.Forms.Button btnShiftResetRB2;

        private System.Windows.Forms.Button btnShiftLeftRB3;
        private System.Windows.Forms.Button btnShiftRightRB3;
        private System.Windows.Forms.Button btnShiftResetRB3;

        // --- Ellipsis menus per slot ---
        private System.Windows.Forms.ContextMenuStrip ctxRun1;
        private System.Windows.Forms.ToolStripMenuItem miRun1Toggle;
        private System.Windows.Forms.ToolStripMenuItem miRun1Remove;
        private System.Windows.Forms.ToolStripMenuItem miRun1Reset;
        private System.Windows.Forms.Button btnMenuRun1;

        private System.Windows.Forms.ContextMenuStrip ctxRB1;
        private System.Windows.Forms.ToolStripMenuItem miRB1Toggle;
        private System.Windows.Forms.ToolStripMenuItem miRB1Remove;
        private System.Windows.Forms.ToolStripMenuItem miRB1Reset;
        private System.Windows.Forms.Button btnMenuRB1;

        private System.Windows.Forms.ContextMenuStrip ctxRun2;
        private System.Windows.Forms.ToolStripMenuItem miRun2Toggle;
        private System.Windows.Forms.ToolStripMenuItem miRun2Remove;
        private System.Windows.Forms.ToolStripMenuItem miRun2Reset;
        private System.Windows.Forms.Button btnMenuRun2;

        private System.Windows.Forms.ContextMenuStrip ctxRB2;
        private System.Windows.Forms.ToolStripMenuItem miRB2Toggle;
        private System.Windows.Forms.ToolStripMenuItem miRB2Remove;
        private System.Windows.Forms.ToolStripMenuItem miRB2Reset;
        private System.Windows.Forms.Button btnMenuRB2;

        private System.Windows.Forms.ContextMenuStrip ctxRun3;
        private System.Windows.Forms.ToolStripMenuItem miRun3Toggle;
        private System.Windows.Forms.ToolStripMenuItem miRun3Remove;
        private System.Windows.Forms.ToolStripMenuItem miRun3Reset;
        private System.Windows.Forms.Button btnMenuRun3;

        private System.Windows.Forms.ContextMenuStrip ctxRB3;
        private System.Windows.Forms.ToolStripMenuItem miRB3Toggle;
        private System.Windows.Forms.ToolStripMenuItem miRB3Remove;
        private System.Windows.Forms.ToolStripMenuItem miRB3Reset;
        private System.Windows.Forms.Button btnMenuRB3;

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
            this.components = new System.ComponentModel.Container();

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

            // 🆕 Instantiate shift buttons
            this.btnShiftLeftRun1 = new System.Windows.Forms.Button();
            this.btnShiftRightRun1 = new System.Windows.Forms.Button();
            this.btnShiftResetRun1 = new System.Windows.Forms.Button();

            this.btnShiftLeftRun2 = new System.Windows.Forms.Button();
            this.btnShiftRightRun2 = new System.Windows.Forms.Button();
            this.btnShiftResetRun2 = new System.Windows.Forms.Button();

            this.btnShiftLeftRun3 = new System.Windows.Forms.Button();
            this.btnShiftRightRun3 = new System.Windows.Forms.Button();
            this.btnShiftResetRun3 = new System.Windows.Forms.Button();

            this.btnShiftLeftRB1 = new System.Windows.Forms.Button();
            this.btnShiftRightRB1 = new System.Windows.Forms.Button();
            this.btnShiftResetRB1 = new System.Windows.Forms.Button();

            this.btnShiftLeftRB2 = new System.Windows.Forms.Button();
            this.btnShiftRightRB2 = new System.Windows.Forms.Button();
            this.btnShiftResetRB2 = new System.Windows.Forms.Button();

            this.btnShiftLeftRB3 = new System.Windows.Forms.Button();
            this.btnShiftRightRB3 = new System.Windows.Forms.Button();
            this.btnShiftResetRB3 = new System.Windows.Forms.Button();

            // ===== Menus (instantiate) =====
            this.ctxRun1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRun1Toggle = new System.Windows.Forms.ToolStripMenuItem();
            this.miRun1Remove = new System.Windows.Forms.ToolStripMenuItem();
            this.miRun1Reset = new System.Windows.Forms.ToolStripMenuItem();
            this.btnMenuRun1 = new System.Windows.Forms.Button();

            this.ctxRB1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRB1Toggle = new System.Windows.Forms.ToolStripMenuItem();
            this.miRB1Remove = new System.Windows.Forms.ToolStripMenuItem();
            this.miRB1Reset = new System.Windows.Forms.ToolStripMenuItem();
            this.btnMenuRB1 = new System.Windows.Forms.Button();

            this.ctxRun2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRun2Toggle = new System.Windows.Forms.ToolStripMenuItem();
            this.miRun2Remove = new System.Windows.Forms.ToolStripMenuItem();
            this.miRun2Reset = new System.Windows.Forms.ToolStripMenuItem();
            this.btnMenuRun2 = new System.Windows.Forms.Button();

            this.ctxRB2 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRB2Toggle = new System.Windows.Forms.ToolStripMenuItem();
            this.miRB2Remove = new System.Windows.Forms.ToolStripMenuItem();
            this.miRB2Reset = new System.Windows.Forms.ToolStripMenuItem();
            this.btnMenuRB2 = new System.Windows.Forms.Button();

            this.ctxRun3 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRun3Toggle = new System.Windows.Forms.ToolStripMenuItem();
            this.miRun3Remove = new System.Windows.Forms.ToolStripMenuItem();
            this.miRun3Reset = new System.Windows.Forms.ToolStripMenuItem();
            this.btnMenuRun3 = new System.Windows.Forms.Button();

            this.ctxRB3 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRB3Toggle = new System.Windows.Forms.ToolStripMenuItem();
            this.miRB3Remove = new System.Windows.Forms.ToolStripMenuItem();
            this.miRB3Reset = new System.Windows.Forms.ToolStripMenuItem();
            this.btnMenuRB3 = new System.Windows.Forms.Button();

            this.SuspendLayout();
            // === Main container (2 rows: buttons + plot) ===
            this.mainContainer = new System.Windows.Forms.TableLayoutPanel();
            this.mainContainer.Name = "mainContainer";
            this.mainContainer.ColumnCount = 1;
            this.mainContainer.RowCount = 2;
            this.mainContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainContainer.Margin = new System.Windows.Forms.Padding(0);
            this.mainContainer.Padding = new System.Windows.Forms.Padding(0);

            // Row 0 auto-sizes to fit the buttons
            this.mainContainer.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            // Row 1 takes the remaining vertical space for the plot
            this.mainContainer.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            // Single column consumes all width
            this.mainContainer.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));


            // === Top panel (single horizontal row, no wrapping) ===
            this.topButtonPanel.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.topButtonPanel.WrapContents = false;
            this.topButtonPanel.AutoScroll = false;                         // no scroll, keep height tight
            this.topButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill; // fills row 0 cell
            this.topButtonPanel.Padding = new System.Windows.Forms.Padding(4, 2, 4, 2); // thinner chrome
            this.topButtonPanel.AutoSize = true;                            // let it size to content
            this.topButtonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.topButtonPanel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 2); // tiny gap to plot




            // === Run 1 Buttons ===
            this.btnLoadRun1.Text = "Load Run 1";
            this.btnLoadRun1.AutoSize = false;                               // fixed width
            this.btnLoadRun1.Size = new System.Drawing.Size(266, 30);        // +33%
            this.btnLoadRun1.MinimumSize = new System.Drawing.Size(266, 30);
            this.btnToggleRun1.Text = "Hide";
            this.btnDeleteRun1.Text = "Delete";
            this.btnToggleRun1.AutoSize = true;
            this.btnDeleteRun1.AutoSize = true;
            this.btnLoadRun1.Click += new System.EventHandler(this.LoadRun1Button_Click);
            this.btnToggleRun1.Click += new System.EventHandler(this.ToggleRun1Button_Click);
            this.btnDeleteRun1.Click += new System.EventHandler(this.DeleteRun1Button_Click);

            // 🆕 Shift controls — Castle Run 1
            ConfigureShiftButton(this.btnShiftLeftRun1, "«", "Shift left (active step)");
            ConfigureShiftButton(this.btnShiftRightRun1, "»", "Shift right (active step)");
            ConfigureShiftButton(this.btnShiftResetRun1, "⟲", "Reset shift to 0");

            this.btnShiftLeftRun1.Click += new System.EventHandler(this.ShiftLeftRun1_Click);
            this.btnShiftRightRun1.Click += new System.EventHandler(this.ShiftRightRun1_Click);
            this.btnShiftResetRun1.Click += new System.EventHandler(this.ShiftResetRun1_Click);

            // --- RaceBox Run 1
            this.btnLoadRaceBox1.Text = "Load RaceBox 1";
            this.btnLoadRaceBox1.AutoSize = false;                           // fixed width
            this.btnLoadRaceBox1.Size = new System.Drawing.Size(266, 30);    // +33%
            this.btnLoadRaceBox1.MinimumSize = new System.Drawing.Size(266, 30);
            this.btnToggleRaceBox1.Text = "Hide";
            this.btnDeleteRaceBox1.Text = "Delete";
            this.btnToggleRaceBox1.AutoSize = true;
            this.btnDeleteRaceBox1.AutoSize = true;
            this.btnLoadRaceBox1.Click += new System.EventHandler(this.LoadRaceBox1Button_Click);
            this.btnToggleRaceBox1.Click += new System.EventHandler(this.ToggleRaceBox1Button_Click);
            this.btnDeleteRaceBox1.Click += new System.EventHandler(this.DeleteRaceBox1Button_Click);

            // 🆕 Shift controls — RaceBox 1 (slot 4)
            ConfigureShiftButton(this.btnShiftLeftRB1, "«", "Shift left (active step)");
            ConfigureShiftButton(this.btnShiftRightRB1, "»", "Shift right (active step)");
            ConfigureShiftButton(this.btnShiftResetRB1, "⟲", "Reset shift to 0");

            this.btnShiftLeftRB1.Click += new System.EventHandler(this.ShiftLeftRB1_Click);
            this.btnShiftRightRB1.Click += new System.EventHandler(this.ShiftRightRB1_Click);
            this.btnShiftResetRB1.Click += new System.EventHandler(this.ShiftResetRB1_Click);

            // === Hide Toggle/Delete/Reset (moved to menu)
            this.btnToggleRun1.Visible = false;
            this.btnDeleteRun1.Visible = false;
            this.btnShiftResetRun1.Visible = false;
            this.btnToggleRaceBox1.Visible = false;
            this.btnDeleteRaceBox1.Visible = false;
            this.btnShiftResetRB1.Visible = false;

            // === Run 1 Panel ===
            var panelRun1 = new System.Windows.Forms.TableLayoutPanel();
            panelRun1.ColumnCount = 7; // Load, Toggle, Delete, «, », ⟲, …
            panelRun1.RowCount = 2;
            panelRun1.AutoSize = true;
            panelRun1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            panelRun1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 0);
            panelRun1.Padding = new System.Windows.Forms.Padding(0);
            for (int i = 0; i < 7; i++)
                panelRun1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));

            // Row 0 — Castle
            panelRun1.Controls.Add(this.btnLoadRun1, 0, 0);
            panelRun1.Controls.Add(this.btnToggleRun1, 1, 0);
            panelRun1.Controls.Add(this.btnDeleteRun1, 2, 0);
            panelRun1.Controls.Add(this.btnShiftLeftRun1, 3, 0);
            panelRun1.Controls.Add(this.btnShiftRightRun1, 4, 0);
            panelRun1.Controls.Add(this.btnShiftResetRun1, 5, 0);

            // Row 1 — RaceBox
            panelRun1.Controls.Add(this.btnLoadRaceBox1, 0, 1);
            panelRun1.Controls.Add(this.btnToggleRaceBox1, 1, 1);
            panelRun1.Controls.Add(this.btnDeleteRaceBox1, 2, 1);
            panelRun1.Controls.Add(this.btnShiftLeftRB1, 3, 1);
            panelRun1.Controls.Add(this.btnShiftRightRB1, 4, 1);
            panelRun1.Controls.Add(this.btnShiftResetRB1, 5, 1);

            // ===== Menus for Run1 / RB1 =====
            // ---- Helper to configure any 3-item menu quickly ----
            void ConfigureMenu(System.Windows.Forms.ContextMenuStrip ctx,
                               System.Windows.Forms.ToolStripMenuItem mToggle,
                               System.Windows.Forms.ToolStripMenuItem mRemove,
                               System.Windows.Forms.ToolStripMenuItem mReset,
                               System.EventHandler onToggle, System.EventHandler onRemove, System.EventHandler onReset)
            {
                mToggle.Text = "Toggle Visibility";
                mRemove.Text = "Remove";
                mReset.Text = "Reset Nudge";

                mToggle.Click += onToggle;
                mRemove.Click += onRemove;
                mReset.Click += onReset;

                ctx.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { mToggle, mRemove, mReset });
            }

            // ---- Helper to configure “…” button quickly ----
            void ConfigureEllipsisButton(System.Windows.Forms.Button btn, System.Windows.Forms.ContextMenuStrip ctx)
            {
                btn.Text = "…";
                btn.AutoSize = false;
                btn.Size = new System.Drawing.Size(28, 30);
                btn.MinimumSize = new System.Drawing.Size(28, 30);
                btn.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
                btn.UseVisualStyleBackColor = true;
                btn.ContextMenuStrip = ctx;
                btn.Click += (s, e) =>
                {
                    if (btn.ContextMenuStrip != null)
                        btn.ContextMenuStrip.Show(btn, new System.Drawing.Point(0, btn.Height));
                };
            }

            // Bind menus to existing buttons via handlers in MainForm.cs
            ConfigureMenu(this.ctxRun1, this.miRun1Toggle, this.miRun1Remove, this.miRun1Reset,
                this.miRun1Toggle_Click, this.miRun1Remove_Click, this.miRun1Reset_Click);
            ConfigureMenu(this.ctxRB1, this.miRB1Toggle, this.miRB1Remove, this.miRB1Reset,
                this.miRB1Toggle_Click, this.miRB1Remove_Click, this.miRB1Reset_Click);

            ConfigureEllipsisButton(this.btnMenuRun1, this.ctxRun1);
            ConfigureEllipsisButton(this.btnMenuRB1, this.ctxRB1);

            panelRun1.Controls.Add(this.btnMenuRun1, 6, 0);
            panelRun1.Controls.Add(this.btnMenuRB1, 6, 1);

            this.topButtonPanel.Controls.Add(panelRun1);
            this.topButtonPanel.Controls.Add(Spacer());

            // === Run 2 Buttons ===
            this.btnLoadRun2.Text = "Load Run 2";
            this.btnLoadRun2.AutoSize = false;
            this.btnLoadRun2.Size = new System.Drawing.Size(266, 30);
            this.btnLoadRun2.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRun2.Text = "Hide";
            this.btnDeleteRun2.Text = "Delete";
            this.btnToggleRun2.AutoSize = true;
            this.btnDeleteRun2.AutoSize = true;

            this.btnLoadRun2.Click += new System.EventHandler(this.LoadRun2Button_Click);
            this.btnToggleRun2.Click += new System.EventHandler(this.ToggleRun2Button_Click);
            this.btnDeleteRun2.Click += new System.EventHandler(this.DeleteRun2Button_Click);

            this.btnLoadRaceBox2.Text = "Load RaceBox 2";
            this.btnLoadRaceBox2.AutoSize = false;
            this.btnLoadRaceBox2.Size = new System.Drawing.Size(266, 30);
            this.btnLoadRaceBox2.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRaceBox2.Text = "Hide";
            this.btnDeleteRaceBox2.Text = "Delete";
            this.btnToggleRaceBox2.AutoSize = true;
            this.btnDeleteRaceBox2.AutoSize = true;

            this.btnLoadRaceBox2.Click += new System.EventHandler(this.LoadRaceBox2Button_Click);
            this.btnToggleRaceBox2.Click += new System.EventHandler(this.ToggleRaceBox2Button_Click);
            this.btnDeleteRaceBox2.Click += new System.EventHandler(this.DeleteRaceBox2Button_Click);

            // 🆕 Shift controls — Castle Run 2
            ConfigureShiftButton(this.btnShiftLeftRun2, "«", "Shift left (active step)");
            ConfigureShiftButton(this.btnShiftRightRun2, "»", "Shift right (active step)");
            ConfigureShiftButton(this.btnShiftResetRun2, "⟲", "Reset shift to 0");
            this.btnShiftLeftRun2.Click += new System.EventHandler(this.ShiftLeftRun2_Click);
            this.btnShiftRightRun2.Click += new System.EventHandler(this.ShiftRightRun2_Click);
            this.btnShiftResetRun2.Click += new System.EventHandler(this.ShiftResetRun2_Click);

            // 🆕 Shift controls — RaceBox 2 (slot 5)
            ConfigureShiftButton(this.btnShiftLeftRB2, "«", "Shift left (active step)");
            ConfigureShiftButton(this.btnShiftRightRB2, "»", "Shift right (active step)");
            ConfigureShiftButton(this.btnShiftResetRB2, "⟲", "Reset shift to 0");
            this.btnShiftLeftRB2.Click += new System.EventHandler(this.ShiftLeftRB2_Click);
            this.btnShiftRightRB2.Click += new System.EventHandler(this.ShiftRightRB2_Click);
            this.btnShiftResetRB2.Click += new System.EventHandler(this.ShiftResetRB2_Click);

            // === Hide Toggle/Delete/Reset (moved to menu)
            this.btnToggleRun2.Visible = false;
            this.btnDeleteRun2.Visible = false;
            this.btnShiftResetRun2.Visible = false;
            this.btnToggleRaceBox2.Visible = false;
            this.btnDeleteRaceBox2.Visible = false;
            this.btnShiftResetRB2.Visible = false;

            // === Run 2 Panel ===
            var panelRun2 = new System.Windows.Forms.TableLayoutPanel();
            panelRun2.ColumnCount = 7;
            panelRun2.RowCount = 2;
            panelRun2.AutoSize = true;
            panelRun2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            panelRun2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 0);
            panelRun2.Padding = new System.Windows.Forms.Padding(0);
            for (int i = 0; i < 7; i++)
                panelRun2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));

            // Row 0 — Castle
            panelRun2.Controls.Add(this.btnLoadRun2, 0, 0);
            panelRun2.Controls.Add(this.btnToggleRun2, 1, 0);
            panelRun2.Controls.Add(this.btnDeleteRun2, 2, 0);
            panelRun2.Controls.Add(this.btnShiftLeftRun2, 3, 0);
            panelRun2.Controls.Add(this.btnShiftRightRun2, 4, 0);
            panelRun2.Controls.Add(this.btnShiftResetRun2, 5, 0);

            // Row 1 — RaceBox
            panelRun2.Controls.Add(this.btnLoadRaceBox2, 0, 1);
            panelRun2.Controls.Add(this.btnToggleRaceBox2, 1, 1);
            panelRun2.Controls.Add(this.btnDeleteRaceBox2, 2, 1);
            panelRun2.Controls.Add(this.btnShiftLeftRB2, 3, 1);
            panelRun2.Controls.Add(this.btnShiftRightRB2, 4, 1);
            panelRun2.Controls.Add(this.btnShiftResetRB2, 5, 1);

            // ===== Menus for Run2 / RB2 =====
            ConfigureMenu(this.ctxRun2, this.miRun2Toggle, this.miRun2Remove, this.miRun2Reset,
                this.miRun2Toggle_Click, this.miRun2Remove_Click, this.miRun2Reset_Click);
            ConfigureMenu(this.ctxRB2, this.miRB2Toggle, this.miRB2Remove, this.miRB2Reset,
                this.miRB2Toggle_Click, this.miRB2Remove_Click, this.miRB2Reset_Click);

            ConfigureEllipsisButton(this.btnMenuRun2, this.ctxRun2);
            ConfigureEllipsisButton(this.btnMenuRB2, this.ctxRB2);

            panelRun2.Controls.Add(this.btnMenuRun2, 6, 0);
            panelRun2.Controls.Add(this.btnMenuRB2, 6, 1);

            this.topButtonPanel.Controls.Add(panelRun2);
            this.topButtonPanel.Controls.Add(Spacer());

            // === Run 3 Buttons ===
            this.btnLoadRun3.Text = "Load Run 3";
            this.btnLoadRun3.AutoSize = false;
            this.btnLoadRun3.Size = new System.Drawing.Size(266, 30);
            this.btnLoadRun3.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRun3.Text = "Hide";
            this.btnDeleteRun3.Text = "Delete";
            this.btnToggleRun3.AutoSize = true;
            this.btnDeleteRun3.AutoSize = true;

            this.btnLoadRun3.Click += new System.EventHandler(this.LoadRun3Button_Click);
            this.btnToggleRun3.Click += new System.EventHandler(this.ToggleRun3Button_Click);
            this.btnDeleteRun3.Click += new System.EventHandler(this.DeleteRun3Button_Click);

            this.btnLoadRaceBox3.Text = "Load RaceBox 3";
            this.btnLoadRaceBox3.AutoSize = false;
            this.btnLoadRaceBox3.Size = new System.Drawing.Size(266, 30);
            this.btnLoadRaceBox3.MinimumSize = new System.Drawing.Size(266, 30);

            this.btnToggleRaceBox3.Text = "Hide";
            this.btnDeleteRaceBox3.Text = "Delete";
            this.btnToggleRaceBox3.AutoSize = true;
            this.btnDeleteRaceBox3.AutoSize = true;

            this.btnLoadRaceBox3.Click += new System.EventHandler(this.LoadRaceBox3Button_Click);
            this.btnToggleRaceBox3.Click += new System.EventHandler(this.ToggleRaceBox3Button_Click);
            this.btnDeleteRaceBox3.Click += new System.EventHandler(this.DeleteRaceBox3Button_Click);

            // 🆕 Shift controls — Castle Run 3
            ConfigureShiftButton(this.btnShiftLeftRun3, "«", "Shift left (active step)");
            ConfigureShiftButton(this.btnShiftRightRun3, "»", "Shift right (active step)");
            ConfigureShiftButton(this.btnShiftResetRun3, "⟲", "Reset shift to 0");
            this.btnShiftLeftRun3.Click += new System.EventHandler(this.ShiftLeftRun3_Click);
            this.btnShiftRightRun3.Click += new System.EventHandler(this.ShiftRightRun3_Click);
            this.btnShiftResetRun3.Click += new System.EventHandler(this.ShiftResetRun3_Click);

            // 🆕 Shift controls — RaceBox 3 (slot 6)
            ConfigureShiftButton(this.btnShiftLeftRB3, "«", "Shift left (active step)");
            ConfigureShiftButton(this.btnShiftRightRB3, "»", "Shift right (active step)");
            ConfigureShiftButton(this.btnShiftResetRB3, "⟲", "Reset shift to 0");
            this.btnShiftLeftRB3.Click += new System.EventHandler(this.ShiftLeftRB3_Click);
            this.btnShiftRightRB3.Click += new System.EventHandler(this.ShiftRightRB3_Click);
            this.btnShiftResetRB3.Click += new System.EventHandler(this.ShiftResetRB3_Click);

            // === Hide Toggle/Delete/Reset (moved to menu)
            this.btnToggleRun3.Visible = false;
            this.btnDeleteRun3.Visible = false;
            this.btnShiftResetRun3.Visible = false;
            this.btnToggleRaceBox3.Visible = false;
            this.btnDeleteRaceBox3.Visible = false;
            this.btnShiftResetRB3.Visible = false;

            // === Run 3 Panel ===
            var panelRun3 = new System.Windows.Forms.TableLayoutPanel();
            panelRun3.ColumnCount = 7;
            panelRun3.RowCount = 2;
            panelRun3.AutoSize = true;
            panelRun3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            panelRun3.Margin = new System.Windows.Forms.Padding(4, 4, 4, 0);
            panelRun3.Padding = new System.Windows.Forms.Padding(0);
            for (int i = 0; i < 7; i++)
                panelRun3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));

            // Row 0 — Castle
            panelRun3.Controls.Add(this.btnLoadRun3, 0, 0);
            panelRun3.Controls.Add(this.btnToggleRun3, 1, 0);
            panelRun3.Controls.Add(this.btnDeleteRun3, 2, 0);
            panelRun3.Controls.Add(this.btnShiftLeftRun3, 3, 0);
            panelRun3.Controls.Add(this.btnShiftRightRun3, 4, 0);
            panelRun3.Controls.Add(this.btnShiftResetRun3, 5, 0);

            // Row 1 — RaceBox
            panelRun3.Controls.Add(this.btnLoadRaceBox3, 0, 1);
            panelRun3.Controls.Add(this.btnToggleRaceBox3, 1, 1);
            panelRun3.Controls.Add(this.btnDeleteRaceBox3, 2, 1);
            panelRun3.Controls.Add(this.btnShiftLeftRB3, 3, 1);
            panelRun3.Controls.Add(this.btnShiftRightRB3, 4, 1);
            panelRun3.Controls.Add(this.btnShiftResetRB3, 5, 1);

            // ===== Menus for Run3 / RB3 =====
            ConfigureMenu(this.ctxRun3, this.miRun3Toggle, this.miRun3Remove, this.miRun3Reset,
                this.miRun3Toggle_Click, this.miRun3Remove_Click, this.miRun3Reset_Click);
            ConfigureMenu(this.ctxRB3, this.miRB3Toggle, this.miRB3Remove, this.miRB3Reset,
                this.miRB3Toggle_Click, this.miRB3Remove_Click, this.miRB3Reset_Click);

            ConfigureEllipsisButton(this.btnMenuRun3, this.ctxRun3);
            ConfigureEllipsisButton(this.btnMenuRB3, this.ctxRB3);

            panelRun3.Controls.Add(this.btnMenuRun3, 6, 0);
            panelRun3.Controls.Add(this.btnMenuRB3, 6, 1);

            this.topButtonPanel.Controls.Add(panelRun3);

            //---------------------------------------
            // === FormsPlot ===
            this.formsPlot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot1.Location = new System.Drawing.Point(0, 0);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(800, 460);
            this.formsPlot1.TabIndex = 0;
            this.formsPlot1.Margin = new System.Windows.Forms.Padding(0);

            // squeeze the control's chrome
            this.formsPlot1.Margin = new System.Windows.Forms.Padding(0);
            this.formsPlot1.Padding = new System.Windows.Forms.Padding(0);


            // Put top panel in row 0 (auto-sized)
            this.topButtonPanel.Margin = new System.Windows.Forms.Padding(0);
            this.topButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill; // inside the table cell

            // Put plot in row 1 (fills remaining space)
            this.formsPlot1.Margin = new System.Windows.Forms.Padding(0);
            this.formsPlot1.Dock = System.Windows.Forms.DockStyle.Fill;

            // Add both to the table, then add the table to the form
            this.mainContainer.Controls.Add(this.topButtonPanel, 0, 0);
            this.mainContainer.Controls.Add(this.formsPlot1, 0, 1);

            this.Controls.Add(this.mainContainer);


            // (belt & suspenders)
            this.formsPlot1.SendToBack();
            this.topButtonPanel.BringToFront();

            //this.Controls.SetChildIndex(this.topButtonPanel, 0);
            //this.Controls.SetChildIndex(this.formsPlot1, 1);

            this.Name = "MainForm";
            this.Text = "DragOverlay V1";

            this.ResumeLayout(false);
            this.PerformLayout();
        }
        #endregion

        // ===== Local helpers (must be OUTSIDE InitializeComponent) =====
        private void ConfigureShiftButton(System.Windows.Forms.Button btn, string text, string tip)
        {
            btn.Text = text;
            btn.AutoSize = false;
            btn.Size = new System.Drawing.Size(28, 30);
            btn.MinimumSize = new System.Drawing.Size(28, 30);
            btn.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
            var tt = new System.Windows.Forms.ToolTip(this.components);
            tt.SetToolTip(btn, tip);
        }

        private System.Windows.Forms.Control Spacer()
        {
            return new System.Windows.Forms.Panel
            {
                Width = 40,
                Height = 1
            };
        }
    }
}
