using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OpenSurge
{
    partial class AddRequestForm : Form
    {
        public RequestBlock Result { get; private set; }

        public AddRequestForm()
        {
            InitializeComponent();
        }

        public AddRequestForm(RequestBlock existing) : this()
        {
            this.Text      = "Edit Request";
            txtName.Text   = existing.Name;
            int idx = cmbMethod.Items.IndexOf(existing.Method);
            cmbMethod.SelectedIndex = idx >= 0 ? idx : 0;
            txtPath.Text   = existing.Path;

            var lines = new List<string>();
            foreach (var h in existing.Headers)
            {
                if (h.Key.Equals("Websurge-Request-Name", StringComparison.OrdinalIgnoreCase)) continue;
                lines.Add(h.Key + ": " + h.Value);
            }
            txtHeaders.Text = string.Join("\r\n", lines);
            txtBody.Text    = existing.Body ?? "";
        }

        void BtnOk_Click(object sender, EventArgs e)
        {
            string path = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("URL / Path is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPath.Focus();
                return;
            }

            string method = cmbMethod.SelectedItem != null
                ? cmbMethod.SelectedItem.ToString()
                : "GET";

            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = path;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in txtHeaders.Lines)
            {
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string hName  = line.Substring(0, colon).Trim();
                    string hValue = line.Substring(colon + 1).Trim();
                    if (!string.IsNullOrEmpty(hName))
                        headers[hName] = hValue;
                }
            }

            Result = new RequestBlock
            {
                Name    = name,
                Method  = method,
                Path    = path,
                Headers = headers,
                Body    = txtBody.Text.Trim()
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
