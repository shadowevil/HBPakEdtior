using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PAKFile
{
    public partial class PAKInterface
    {
        private TableLayoutPanel InitializePluginComponents()
        {
            TableLayoutPanel tableLayoutPanel1 = new TableLayoutPanel();
            Panel pivotYPanel = new Panel();
            Label label4 = new Label();
            TextBox numPivY = new TextBox();
            Panel heightPanel = new Panel();
            Label label5 = new Label();
            TextBox numHeight = new TextBox();
            Panel yPanel = new Panel();
            Label label6 = new Label();
            TextBox numY = new TextBox();
            Panel pivotXPanel = new Panel();
            Label label3 = new Label();
            TextBox numPivX = new TextBox();
            Panel widthPanel = new Panel();
            Label label2 = new Label();
            TextBox numWidth = new TextBox();
            Panel xPanel = new Panel();
            Label label1 = new Label();
            TextBox numX = new TextBox();

            tableLayoutPanel1.SuspendLayout();
            pivotYPanel.SuspendLayout();
            heightPanel.SuspendLayout();
            yPanel.SuspendLayout();
            pivotXPanel.SuspendLayout();
            widthPanel.SuspendLayout();
            xPanel.SuspendLayout();

            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
            tableLayoutPanel1.Controls.Add(pivotYPanel, 2, 1);
            tableLayoutPanel1.Controls.Add(heightPanel, 1, 1);
            tableLayoutPanel1.Controls.Add(yPanel, 0, 1);
            tableLayoutPanel1.Controls.Add(pivotXPanel, 2, 0);
            tableLayoutPanel1.Controls.Add(widthPanel, 1, 0);
            tableLayoutPanel1.Controls.Add(xPanel, 0, 0);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(540, 126);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // pivotYPanel
            // 
            pivotYPanel.Controls.Add(label4);
            pivotYPanel.Controls.Add(numPivY);
            pivotYPanel.Dock = DockStyle.Fill;
            pivotYPanel.Location = new Point(363, 66);
            pivotYPanel.Name = "pivotYPanel";
            pivotYPanel.Size = new Size(174, 57);
            pivotYPanel.TabIndex = 5;
            // 
            // label4
            // 
            label4.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label4.Location = new Point(1, 19);
            label4.Name = "label4";
            label4.Size = new Size(62, 25);
            label4.TabIndex = 0;
            label4.Text = "Pivot Y:";
            label4.TextAlign = ContentAlignment.MiddleRight;
            // 
            // numPivY
            // 
            numPivY.Location = new Point(69, 18);
            numPivY.Name = "numPivY";
            numPivY.Size = new Size(98, 27);
            numPivY.TabIndex = 1;
            // 
            // heightPanel
            // 
            heightPanel.Controls.Add(label5);
            heightPanel.Controls.Add(numHeight);
            heightPanel.Dock = DockStyle.Fill;
            heightPanel.Location = new Point(183, 66);
            heightPanel.Name = "heightPanel";
            heightPanel.Size = new Size(174, 57);
            heightPanel.TabIndex = 3;
            // 
            // label5
            // 
            label5.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label5.Location = new Point(1, 19);
            label5.Name = "label5";
            label5.Size = new Size(62, 25);
            label5.TabIndex = 0;
            label5.Text = "Height:";
            label5.TextAlign = ContentAlignment.MiddleRight;
            // 
            // numHeight
            // 
            numHeight.Location = new Point(69, 18);
            numHeight.Name = "numHeight";
            numHeight.Size = new Size(98, 27);
            numHeight.TabIndex = 1;
            // 
            // yPanel
            // 
            yPanel.Controls.Add(label6);
            yPanel.Controls.Add(numY);
            yPanel.Dock = DockStyle.Fill;
            yPanel.Location = new Point(3, 66);
            yPanel.Name = "yPanel";
            yPanel.Size = new Size(174, 57);
            yPanel.TabIndex = 1;
            // 
            // label6
            // 
            label6.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label6.Location = new Point(1, 19);
            label6.Name = "label6";
            label6.Size = new Size(62, 25);
            label6.TabIndex = 0;
            label6.Text = "Y:";
            label6.TextAlign = ContentAlignment.MiddleRight;
            // 
            // numY
            // 
            numY.Location = new Point(69, 18);
            numY.Name = "numY";
            numY.Size = new Size(98, 27);
            numY.TabIndex = 1;
            // 
            // pivotXPanel
            // 
            pivotXPanel.Controls.Add(label3);
            pivotXPanel.Controls.Add(numPivX);
            pivotXPanel.Dock = DockStyle.Fill;
            pivotXPanel.Location = new Point(363, 3);
            pivotXPanel.Name = "pivotXPanel";
            pivotXPanel.Size = new Size(174, 57);
            pivotXPanel.TabIndex = 4;
            // 
            // label3
            // 
            label3.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label3.Location = new Point(1, 19);
            label3.Name = "label3";
            label3.Size = new Size(62, 25);
            label3.TabIndex = 0;
            label3.Text = "Pivot X:";
            label3.TextAlign = ContentAlignment.MiddleRight;
            // 
            // numPivX
            // 
            numPivX.Location = new Point(69, 18);
            numPivX.Name = "numPivX";
            numPivX.Size = new Size(98, 27);
            numPivX.TabIndex = 1;
            // 
            // widthPanel
            // 
            widthPanel.Controls.Add(label2);
            widthPanel.Controls.Add(numWidth);
            widthPanel.Dock = DockStyle.Fill;
            widthPanel.Location = new Point(183, 3);
            widthPanel.Name = "widthPanel";
            widthPanel.Size = new Size(174, 57);
            widthPanel.TabIndex = 2;
            // 
            // label2
            // 
            label2.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label2.Location = new Point(1, 19);
            label2.Name = "label2";
            label2.Size = new Size(62, 25);
            label2.TabIndex = 0;
            label2.Text = "Width:";
            label2.TextAlign = ContentAlignment.MiddleRight;
            // 
            // numWidth
            // 
            numWidth.Location = new Point(69, 18);
            numWidth.Name = "numWidth";
            numWidth.Size = new Size(98, 27);
            numWidth.TabIndex = 1;
            // 
            // xPanel
            // 
            xPanel.Controls.Add(label1);
            xPanel.Controls.Add(numX);
            xPanel.Dock = DockStyle.Fill;
            xPanel.Location = new Point(3, 3);
            xPanel.Name = "xPanel";
            xPanel.Size = new Size(174, 57);
            xPanel.TabIndex = 0;
            // 
            // label1
            // 
            label1.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.Location = new Point(1, 19);
            label1.Name = "label1";
            label1.Size = new Size(62, 25);
            label1.TabIndex = 0;
            label1.Text = "X:";
            label1.TextAlign = ContentAlignment.MiddleRight;
            // 
            // numX
            // 
            numX.Location = new Point(69, 18);
            numX.Name = "numX";
            numX.Size = new Size(98, 27);
            numX.TabIndex = 1;

            tableLayoutPanel1.ResumeLayout(false);
            pivotYPanel.ResumeLayout(false);
            pivotYPanel.PerformLayout();
            heightPanel.ResumeLayout(false);
            heightPanel.PerformLayout();
            yPanel.ResumeLayout(false);
            yPanel.PerformLayout();
            pivotXPanel.ResumeLayout(false);
            pivotXPanel.PerformLayout();
            widthPanel.ResumeLayout(false);
            widthPanel.PerformLayout();
            xPanel.ResumeLayout(false);
            xPanel.PerformLayout();
            return tableLayoutPanel1;
        }
    }
}
