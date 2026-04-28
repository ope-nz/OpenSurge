using System.Drawing;
using System.Windows.Forms;

namespace OpenSurge
{
    partial class AddRequestForm
    {
        void InitializeComponent()
        {
            this.lblName    = new Label();
            this.txtName    = new TextBox();
            this.lblMethod  = new Label();
            this.cmbMethod  = new ComboBox();
            this.lblPath    = new Label();
            this.txtPath    = new TextBox();
            this.lblHeaders = new Label();
            this.txtHeaders = new TextBox();
            this.lblBody    = new Label();
            this.txtBody    = new TextBox();
            this.btnOk      = new Button();
            this.btnCancel  = new Button();

            // Row 1 -- Name
            this.lblName.Text      = "Name:";
            this.lblName.Location  = new Point(8, 16);
            this.lblName.Size      = new Size(72, 20);
            this.lblName.TextAlign = ContentAlignment.MiddleRight;

            this.txtName.Location  = new Point(84, 14);
            this.txtName.Size      = new Size(392, 23);

            // Row 2 -- Method
            this.lblMethod.Text      = "Method:";
            this.lblMethod.Location  = new Point(8, 48);
            this.lblMethod.Size      = new Size(72, 20);
            this.lblMethod.TextAlign = ContentAlignment.MiddleRight;

            this.cmbMethod.Location      = new Point(84, 46);
            this.cmbMethod.Size          = new Size(110, 23);
            this.cmbMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbMethod.Items.AddRange(new object[] {
                "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
            });
            this.cmbMethod.SelectedIndex = 0;

            // Row 3 -- URL/Path
            this.lblPath.Text      = "URL / Path:";
            this.lblPath.Location  = new Point(8, 80);
            this.lblPath.Size      = new Size(72, 20);
            this.lblPath.TextAlign = ContentAlignment.MiddleRight;

            this.txtPath.Location  = new Point(84, 78);
            this.txtPath.Size      = new Size(392, 23);

            // Row 4 -- Headers
            this.lblHeaders.Text      = "Headers (one per line, Name: Value):";
            this.lblHeaders.Location  = new Point(8, 114);
            this.lblHeaders.Size      = new Size(468, 16);
            this.lblHeaders.ForeColor = System.Drawing.Color.DimGray;

            this.txtHeaders.Location   = new Point(8, 132);
            this.txtHeaders.Size       = new Size(468, 72);
            this.txtHeaders.Multiline  = true;
            this.txtHeaders.ScrollBars = ScrollBars.Vertical;
            this.txtHeaders.Font       = new Font("Consolas", 9f);
            this.txtHeaders.AcceptsReturn = true;

            // Row 5 -- Body
            this.lblBody.Text      = "Body:";
            this.lblBody.Location  = new Point(8, 216);
            this.lblBody.Size      = new Size(468, 16);
            this.lblBody.ForeColor = System.Drawing.Color.DimGray;

            this.txtBody.Location   = new Point(8, 234);
            this.txtBody.Size       = new Size(468, 110);
            this.txtBody.Multiline  = true;
            this.txtBody.ScrollBars = ScrollBars.Vertical;
            this.txtBody.Font       = new Font("Consolas", 9f);
            this.txtBody.AcceptsReturn = true;

            // Buttons
            this.btnOk.Text         = "OK";
            this.btnOk.Location     = new Point(316, 360);
            this.btnOk.Size         = new Size(76, 28);
            this.btnOk.DialogResult = DialogResult.None;
            this.btnOk.Click       += BtnOk_Click;

            this.btnCancel.Text         = "Cancel";
            this.btnCancel.Location     = new Point(400, 360);
            this.btnCancel.Size         = new Size(76, 28);
            this.btnCancel.DialogResult = DialogResult.Cancel;

            // Form
            this.Text            = "Add Request";
            this.ClientSize      = new Size(484, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.StartPosition   = FormStartPosition.CenterParent;
            this.AcceptButton    = btnOk;
            this.CancelButton    = btnCancel;

            this.Controls.AddRange(new Control[] {
                lblName, txtName,
                lblMethod, cmbMethod,
                lblPath, txtPath,
                lblHeaders, txtHeaders,
                lblBody, txtBody,
                btnOk, btnCancel
            });
        }

        Label    lblName, lblMethod, lblPath, lblHeaders, lblBody;
        TextBox  txtName, txtPath, txtHeaders, txtBody;
        ComboBox cmbMethod;
        Button   btnOk, btnCancel;
    }
}
