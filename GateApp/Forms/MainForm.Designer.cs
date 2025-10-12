using System.Drawing;
using System.Windows.Forms;

namespace GateApp.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel tableLayoutPanel;
    private Panel topPanel;
    private Label scannerLabel;
    private PictureBox pictureBox1;
    private PictureBox pictureBox2;
    private PictureBox pictureBox3;
    private PictureBox pictureBox4;
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
        pictureBox1 = new PictureBox();
        pictureBox2 = new PictureBox();
        pictureBox3 = new PictureBox();
        pictureBox4 = new PictureBox();
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
        ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
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
        tableLayoutPanel.Controls.Add(pictureBox1, 0, 0);
        tableLayoutPanel.Controls.Add(pictureBox2, 1, 0);
        tableLayoutPanel.Controls.Add(pictureBox3, 0, 1);
        tableLayoutPanel.Controls.Add(pictureBox4, 1, 1);
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
        // pictureBox1
        // 
        pictureBox1.BackColor = Color.Black;
        pictureBox1.Dock = DockStyle.Fill;
        pictureBox1.Location = new Point(6, 7);
        pictureBox1.Margin = new Padding(6, 7, 6, 7);
        pictureBox1.Name = "pictureBox1";
        pictureBox1.Size = new Size(719, 387);
        pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox1.TabIndex = 0;
        pictureBox1.TabStop = false;
        // 
        // pictureBox2
        // 
        pictureBox2.BackColor = Color.Black;
        pictureBox2.Dock = DockStyle.Fill;
        pictureBox2.Location = new Point(737, 7);
        pictureBox2.Margin = new Padding(6, 7, 6, 7);
        pictureBox2.Name = "pictureBox2";
        pictureBox2.Size = new Size(720, 387);
        pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox2.TabIndex = 1;
        pictureBox2.TabStop = false;
        // 
        // pictureBox3
        // 
        pictureBox3.BackColor = Color.Black;
        pictureBox3.Dock = DockStyle.Fill;
        pictureBox3.Location = new Point(6, 408);
        pictureBox3.Margin = new Padding(6, 7, 6, 7);
        pictureBox3.Name = "pictureBox3";
        pictureBox3.Size = new Size(719, 387);
        pictureBox3.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox3.TabIndex = 2;
        pictureBox3.TabStop = false;
        // 
        // pictureBox4
        // 
        pictureBox4.BackColor = Color.Black;
        pictureBox4.Dock = DockStyle.Fill;
        pictureBox4.Location = new Point(737, 408);
        pictureBox4.Margin = new Padding(6, 7, 6, 7);
        pictureBox4.Name = "pictureBox4";
        pictureBox4.Size = new Size(720, 387);
        pictureBox4.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox4.TabIndex = 3;
        pictureBox4.TabStop = false;
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
        logTextBox.Size = new Size(1463, 267);
        logTextBox.TabIndex = 0;
        // 
        // statusStrip
        // 
        statusStrip.ImageScalingSize = new Size(20, 20);
        statusStrip.Items.AddRange(new ToolStripItem[] { apiStatusLabel, cameraStatusLabel, gateStatusLabel });
        statusStrip.Location = new Point(0, 267);
        statusStrip.Name = "statusStrip";
        statusStrip.Padding = new Padding(1, 0, 16, 0);
        statusStrip.Size = new Size(1463, 26);
        statusStrip.TabIndex = 2;
        // 
        // apiStatusLabel
        // 
        apiStatusLabel.Name = "apiStatusLabel";
        apiStatusLabel.Size = new Size(482, 20);
        apiStatusLabel.Spring = true;
        apiStatusLabel.Text = "API: Idle";
        // 
        // cameraStatusLabel
        // 
        cameraStatusLabel.Name = "cameraStatusLabel";
        cameraStatusLabel.Size = new Size(482, 20);
        cameraStatusLabel.Spring = true;
        cameraStatusLabel.Text = "Cameras: Idle";
        // 
        // gateStatusLabel
        // 
        gateStatusLabel.Name = "gateStatusLabel";
        gateStatusLabel.Size = new Size(482, 20);
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
        ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox3).EndInit();
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
