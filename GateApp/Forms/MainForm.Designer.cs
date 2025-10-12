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
        components = new System.ComponentModel.Container();
        tableLayoutPanel = new TableLayoutPanel();
        topPanel = new Panel();
        scannerLabel = new Label();
        pictureBox1 = new PictureBox();
        pictureBox2 = new PictureBox();
        pictureBox3 = new PictureBox();
        pictureBox4 = new PictureBox();
        bottomPanel = new Panel();
        logTextBox = new TextBox();
        scannerTextBox = new TextBox();
        statusStrip = new StatusStrip();
        apiStatusLabel = new ToolStripStatusLabel();
        cameraStatusLabel = new ToolStripStatusLabel();
        gateStatusLabel = new ToolStripStatusLabel();
        tableLayoutPanel.SuspendLayout();
        topPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox3).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pictureBox4).BeginInit();
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
        tableLayoutPanel.Location = new Point(0, 60);
        tableLayoutPanel.Name = "tableLayoutPanel";
        tableLayoutPanel.RowCount = 2;
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel.Size = new Size(1280, 660);
        tableLayoutPanel.TabIndex = 0;
        //
        // topPanel
        //
        topPanel.Controls.Add(scannerTextBox);
        topPanel.Controls.Add(scannerLabel);
        topPanel.Dock = DockStyle.Top;
        topPanel.Height = 60;
        topPanel.Padding = new Padding(10, 10, 10, 5);
        topPanel.Name = "topPanel";
        topPanel.TabIndex = 3;
        //
        // scannerLabel
        //
        scannerLabel.AutoSize = true;
        scannerLabel.Dock = DockStyle.Left;
        scannerLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
        scannerLabel.Location = new Point(10, 10);
        scannerLabel.Margin = new Padding(0, 0, 10, 0);
        scannerLabel.Name = "scannerLabel";
        scannerLabel.Padding = new Padding(0, 5, 0, 0);
        scannerLabel.Size = new Size(132, 26);
        scannerLabel.TabIndex = 0;
        scannerLabel.Text = "Scan QR Code:";
        //
        // pictureBox1
        //
        pictureBox1.Dock = DockStyle.Fill;
        pictureBox1.BackColor = Color.Black;
        pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox1.Margin = new Padding(5);
        // 
        // pictureBox2
        // 
        pictureBox2.Dock = DockStyle.Fill;
        pictureBox2.BackColor = Color.Black;
        pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox2.Margin = new Padding(5);
        // 
        // pictureBox3
        // 
        pictureBox3.Dock = DockStyle.Fill;
        pictureBox3.BackColor = Color.Black;
        pictureBox3.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox3.Margin = new Padding(5);
        // 
        // pictureBox4
        // 
        pictureBox4.Dock = DockStyle.Fill;
        pictureBox4.BackColor = Color.Black;
        pictureBox4.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBox4.Margin = new Padding(5);
        // 
        // bottomPanel
        // 
        bottomPanel.Controls.Add(logTextBox);
        bottomPanel.Controls.Add(statusStrip);
        bottomPanel.Dock = DockStyle.Bottom;
        bottomPanel.Height = 220;
        bottomPanel.Name = "bottomPanel";
        bottomPanel.TabIndex = 1;
        // 
        // logTextBox
        // 
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Multiline = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.ReadOnly = true;
        logTextBox.BackColor = Color.Black;
        logTextBox.ForeColor = Color.Lime;
        logTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
        logTextBox.TabIndex = 0;
        // 
        // scannerTextBox
        //
        scannerTextBox.Dock = DockStyle.Fill;
        scannerTextBox.Font = new Font("Consolas", 18F, FontStyle.Regular, GraphicsUnit.Point);
        scannerTextBox.Margin = new Padding(10, 0, 0, 0);
        scannerTextBox.TabIndex = 1;
        scannerTextBox.TabStop = false;
        // 
        // statusStrip
        // 
        statusStrip.Dock = DockStyle.Bottom;
        statusStrip.Items.AddRange(new ToolStripItem[] { apiStatusLabel, cameraStatusLabel, gateStatusLabel });
        statusStrip.Location = new Point(0, 198);
        statusStrip.Name = "statusStrip";
        statusStrip.TabIndex = 2;
        // 
        // apiStatusLabel
        // 
        apiStatusLabel.Text = "API: Idle";
        apiStatusLabel.Spring = true;
        // 
        // cameraStatusLabel
        // 
        cameraStatusLabel.Text = "Cameras: Idle";
        cameraStatusLabel.Spring = true;
        // 
        // gateStatusLabel
        // 
        gateStatusLabel.Text = "Gate: Idle";
        gateStatusLabel.Spring = true;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 940);
        Controls.Add(tableLayoutPanel);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
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
        bottomPanel.ResumeLayout(false);
        bottomPanel.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        topPanel.ResumeLayout(false);
        topPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
