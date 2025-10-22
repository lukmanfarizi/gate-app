using System.Drawing;
using System.Windows.Forms;

namespace GateApp.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel tableLayoutPanel;
    private Panel topPanel;
    private Label scannerLabel;
    private Panel cameraPanel1;
    private Panel cameraPanel2;
    private Panel cameraPanel3;
    private Panel cameraPanel4;
    private PictureBox pictureBox1;
    private PictureBox pictureBox2;
    private PictureBox pictureBox3;
    private PictureBox pictureBox4;
    private Button cameraToggleButton1;
    private Button cameraToggleButton2;
    private Button cameraToggleButton3;
    private Button cameraToggleButton4;
    private Panel bottomPanel;
    private TextBox logTextBox;
    private TextBox scannerTextBox;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel apiStatusLabel;
    private ToolStripStatusLabel cameraStatusLabel;
    private ToolStripStatusLabel gateStatusLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        tableLayoutPanel = new TableLayoutPanel();
        cameraPanel1 = new Panel();
        pictureBox1 = new PictureBox();
        cameraToggleButton1 = new Button();
        cameraPanel2 = new Panel();
        pictureBox2 = new PictureBox();
        cameraToggleButton2 = new Button();
        cameraPanel3 = new Panel();
        pictureBox3 = new PictureBox();
        cameraToggleButton3 = new Button();
        cameraPanel4 = new Panel();
        pictureBox4 = new PictureBox();
        cameraToggleButton4 = new Button();
        topPanel = new Panel();
        scannerTextBox = new TextBox();
        scannerLabel = new Label();
        bottomPanel = new Panel();
        logTextBox = new TextBox();
        statusStrip = new StatusStrip();
        apiStatusLabel = new ToolStripStatusLabel();
        cameraStatusLabel = new ToolStripStatusLabel();
        gateStatusLabel = new ToolStripStatusLabel();
        tableLayoutPanel.SuspendLayout();
        cameraPanel1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
        cameraPanel2.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
        cameraPanel3.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
        cameraPanel4.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureBox4).BeginInit();
        topPanel.SuspendLayout();
        bottomPanel.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // tableLayoutPanel
        // 
        tableLayoutPanel.ColumnCount = 2;
        tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel.Controls.Add(cameraPanel1, 0, 0);
        tableLayoutPanel.Controls.Add(cameraPanel2, 1, 0);
        tableLayoutPanel.Controls.Add(cameraPanel3, 0, 1);
        tableLayoutPanel.Controls.Add(cameraPanel4, 1, 1);
        tableLayoutPanel.Dock = DockStyle.Fill;
        tableLayoutPanel.Location = new Point(0, 80);
        tableLayoutPanel.Margin = new Padding(3, 4, 3, 4);
        tableLayoutPanel.Name = "tableLayoutPanel";
        tableLayoutPanel.RowCount = 2;
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel.Size = new Size(1463, 802);
        tableLayoutPanel.TabIndex = 0;
        // 
        // cameraPanel1
        // 
        cameraPanel1.Controls.Add(pictureBox1);
        cameraPanel1.Controls.Add(cameraToggleButton1);
        cameraPanel1.Dock = DockStyle.Fill;
        cameraPanel1.Location = new Point(6, 7);
        cameraPanel1.Margin = new Padding(6, 7, 6, 7);
        cameraPanel1.Name = "cameraPanel1";
        cameraPanel1.Size = new Size(719, 387);
        cameraPanel1.TabIndex = 4;
        // 
        // pictureBox1
        // 
        pictureBox1.BackColor = Color.Black;
        pictureBox1.Dock = DockStyle.Fill;
        pictureBox1.Location = new Point(0, 0);
        pictureBox1.Margin = new Padding(0);
        pictureBox1.Name = "pictureBox1";
        pictureBox1.Size = new Size(719, 342);
        pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox1.TabIndex = 0;
        pictureBox1.TabStop = false;
        // 
        // cameraToggleButton1
        // 
        cameraToggleButton1.Dock = DockStyle.Bottom;
        cameraToggleButton1.Location = new Point(0, 342);
        cameraToggleButton1.Margin = new Padding(0);
        cameraToggleButton1.Name = "cameraToggleButton1";
        cameraToggleButton1.Size = new Size(719, 45);
        cameraToggleButton1.TabIndex = 1;
        cameraToggleButton1.TabStop = false;
        cameraToggleButton1.Text = "Start";
        cameraToggleButton1.UseVisualStyleBackColor = true;
        // 
        // cameraPanel2
        // 
        cameraPanel2.Controls.Add(pictureBox2);
        cameraPanel2.Controls.Add(cameraToggleButton2);
        cameraPanel2.Dock = DockStyle.Fill;
        cameraPanel2.Location = new Point(737, 7);
        cameraPanel2.Margin = new Padding(6, 7, 6, 7);
        cameraPanel2.Name = "cameraPanel2";
        cameraPanel2.Size = new Size(720, 387);
        cameraPanel2.TabIndex = 5;
        // 
        // pictureBox2
        // 
        pictureBox2.BackColor = Color.Black;
        pictureBox2.Dock = DockStyle.Fill;
        pictureBox2.Location = new Point(0, 0);
        pictureBox2.Margin = new Padding(0);
        pictureBox2.Name = "pictureBox2";
        pictureBox2.Size = new Size(720, 342);
        pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox2.TabIndex = 0;
        pictureBox2.TabStop = false;
        // 
        // cameraToggleButton2
        // 
        cameraToggleButton2.Dock = DockStyle.Bottom;
        cameraToggleButton2.Location = new Point(0, 342);
        cameraToggleButton2.Margin = new Padding(0);
        cameraToggleButton2.Name = "cameraToggleButton2";
        cameraToggleButton2.Size = new Size(720, 45);
        cameraToggleButton2.TabIndex = 1;
        cameraToggleButton2.TabStop = false;
        cameraToggleButton2.Text = "Start";
        cameraToggleButton2.UseVisualStyleBackColor = true;
        // 
        // cameraPanel3
        // 
        cameraPanel3.Controls.Add(pictureBox3);
        cameraPanel3.Controls.Add(cameraToggleButton3);
        cameraPanel3.Dock = DockStyle.Fill;
        cameraPanel3.Location = new Point(6, 408);
        cameraPanel3.Margin = new Padding(6, 7, 6, 7);
        cameraPanel3.Name = "cameraPanel3";
        cameraPanel3.Size = new Size(719, 387);
        cameraPanel3.TabIndex = 6;
        // 
        // pictureBox3
        // 
        pictureBox3.BackColor = Color.Black;
        pictureBox3.Dock = DockStyle.Fill;
        pictureBox3.Location = new Point(0, 0);
        pictureBox3.Margin = new Padding(0);
        pictureBox3.Name = "pictureBox3";
        pictureBox3.Size = new Size(719, 342);
        pictureBox3.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox3.TabIndex = 0;
        pictureBox3.TabStop = false;
        // 
        // cameraToggleButton3
        // 
        cameraToggleButton3.Dock = DockStyle.Bottom;
        cameraToggleButton3.Location = new Point(0, 342);
        cameraToggleButton3.Margin = new Padding(0);
        cameraToggleButton3.Name = "cameraToggleButton3";
        cameraToggleButton3.Size = new Size(719, 45);
        cameraToggleButton3.TabIndex = 1;
        cameraToggleButton3.TabStop = false;
        cameraToggleButton3.Text = "Start";
        cameraToggleButton3.UseVisualStyleBackColor = true;
        // 
        // cameraPanel4
        // 
        cameraPanel4.Controls.Add(pictureBox4);
        cameraPanel4.Controls.Add(cameraToggleButton4);
        cameraPanel4.Dock = DockStyle.Fill;
        cameraPanel4.Location = new Point(737, 408);
        cameraPanel4.Margin = new Padding(6, 7, 6, 7);
        cameraPanel4.Name = "cameraPanel4";
        cameraPanel4.Size = new Size(720, 387);
        cameraPanel4.TabIndex = 7;
        // 
        // pictureBox4
        // 
        pictureBox4.BackColor = Color.Black;
        pictureBox4.Dock = DockStyle.Fill;
        pictureBox4.Location = new Point(0, 0);
        pictureBox4.Margin = new Padding(0);
        pictureBox4.Name = "pictureBox4";
        pictureBox4.Size = new Size(720, 342);
        pictureBox4.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox4.TabIndex = 0;
        pictureBox4.TabStop = false;
        // 
        // cameraToggleButton4
        // 
        cameraToggleButton4.Dock = DockStyle.Bottom;
        cameraToggleButton4.Location = new Point(0, 342);
        cameraToggleButton4.Margin = new Padding(0);
        cameraToggleButton4.Name = "cameraToggleButton4";
        cameraToggleButton4.Size = new Size(720, 45);
        cameraToggleButton4.TabIndex = 1;
        cameraToggleButton4.TabStop = false;
        cameraToggleButton4.Text = "Start";
        cameraToggleButton4.UseVisualStyleBackColor = true;
        // 
        // topPanel
        // 
        topPanel.Controls.Add(scannerTextBox);
        topPanel.Controls.Add(scannerLabel);
        topPanel.Dock = DockStyle.Top;
        topPanel.Location = new Point(0, 0);
        topPanel.Margin = new Padding(3, 4, 3, 4);
        topPanel.Name = "topPanel";
        topPanel.Padding = new Padding(11, 13, 11, 7);
        topPanel.Size = new Size(1463, 80);
        topPanel.TabIndex = 3;
        // 
        // scannerTextBox
        // 
        scannerTextBox.Dock = DockStyle.Fill;
        scannerTextBox.Font = new Font("Consolas", 18F);
        scannerTextBox.Location = new Point(159, 13);
        scannerTextBox.Margin = new Padding(11, 0, 0, 0);
        scannerTextBox.Name = "scannerTextBox";
        scannerTextBox.Size = new Size(1293, 43);
        scannerTextBox.TabIndex = 1;
        scannerTextBox.TabStop = false;
        // 
        // scannerLabel
        // 
        scannerLabel.AutoSize = true;
        scannerLabel.Dock = DockStyle.Left;
        scannerLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        scannerLabel.Location = new Point(11, 13);
        scannerLabel.Margin = new Padding(0, 0, 11, 0);
        scannerLabel.Name = "scannerLabel";
        scannerLabel.Padding = new Padding(0, 7, 0, 0);
        scannerLabel.Size = new Size(148, 35);
        scannerLabel.TabIndex = 0;
        scannerLabel.Text = "Scan QR Code:";
        // 
        // bottomPanel
        // 
        bottomPanel.Controls.Add(logTextBox);
        bottomPanel.Controls.Add(statusStrip);
        bottomPanel.Dock = DockStyle.Bottom;
        bottomPanel.Location = new Point(0, 882);
        bottomPanel.Margin = new Padding(3, 4, 3, 4);
        bottomPanel.Name = "bottomPanel";
        bottomPanel.Size = new Size(1463, 293);
        bottomPanel.TabIndex = 1;
        // 
        // logTextBox
        // 
        logTextBox.BackColor = Color.Black;
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Font = new Font("Consolas", 10F);
        logTextBox.ForeColor = Color.Lime;
        logTextBox.Location = new Point(0, 0);
        logTextBox.Margin = new Padding(3, 4, 3, 4);
        logTextBox.Multiline = true;
        logTextBox.Name = "logTextBox";
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Size = new Size(1463, 262);
        logTextBox.TabIndex = 0;
        // 
        // statusStrip
        // 
        statusStrip.ImageScalingSize = new Size(20, 20);
        statusStrip.Items.AddRange(new ToolStripItem[] { apiStatusLabel, cameraStatusLabel, gateStatusLabel });
        statusStrip.Location = new Point(0, 262);
        statusStrip.Name = "statusStrip";
        statusStrip.Padding = new Padding(1, 0, 16, 0);
        statusStrip.Size = new Size(1463, 31);
        statusStrip.TabIndex = 2;
        // 
        // apiStatusLabel
        // 
        apiStatusLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        apiStatusLabel.Name = "apiStatusLabel";
        apiStatusLabel.Size = new Size(469, 25);
        apiStatusLabel.Spring = true;
        apiStatusLabel.Text = "API: Idle";
        // 
        // cameraStatusLabel
        // 
        cameraStatusLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        cameraStatusLabel.Name = "cameraStatusLabel";
        cameraStatusLabel.Size = new Size(469, 25);
        cameraStatusLabel.Spring = true;
        cameraStatusLabel.Text = "Cameras: Idle";
        // 
        // gateStatusLabel
        // 
        gateStatusLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        gateStatusLabel.Name = "gateStatusLabel";
        gateStatusLabel.Size = new Size(469, 25);
        gateStatusLabel.Spring = true;
        gateStatusLabel.Text = "Gate: Idle";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1463, 1175);
        Controls.Add(tableLayoutPanel);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
        Margin = new Padding(3, 4, 3, 4);
        Name = "MainForm";
        Text = "GateApp";
        WindowState = FormWindowState.Maximized;
        FormClosing += MainForm_FormClosing;
        Shown += MainForm_Shown;
        tableLayoutPanel.ResumeLayout(false);
        cameraPanel1.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
        cameraPanel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
        cameraPanel3.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)pictureBox3).EndInit();
        cameraPanel4.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)pictureBox4).EndInit();
        topPanel.ResumeLayout(false);
        topPanel.PerformLayout();
        bottomPanel.ResumeLayout(false);
        bottomPanel.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
    }
}
