using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HBPakEditor
{
    public class InputBox : Form
    {
        private TextBox _inputTextBox;
        private Button _okButton;
        private Button _cancelButton;
        private Label _promptLabel;
        private InputBoxConfiguration _config;

        public string Input => _inputTextBox.Text;

        private InputBox(string prompt, string title, InputBoxConfiguration config)
        {
            _config = config;
            InitializeComponents(prompt, title);
        }

        private void InitializeComponents(string prompt, string title)
        {
            Text = title;
            Width = _config.Width;
            Height = _config.Height;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            _promptLabel = new Label
            {
                Text = prompt,
                Left = 20,
                Top = 20,
                Width = _config.Width - 60,
                Height = 20
            };

            _inputTextBox = new TextBox
            {
                Left = 20,
                Top = 50,
                Width = _config.Width - 60,
                UseSystemPasswordChar = _config.HideInput,
                MaxLength = _config.MaxLength
            };

            if (!string.IsNullOrEmpty(_config.DefaultValue))
            {
                _inputTextBox.Text = _config.DefaultValue;
            }

            _okButton = new Button
            {
                Text = "OK",
                Left = _config.Width - 220,
                Top = 80,
                Width = 75,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Left = _config.Width - 115,
                Top = 80,
                Width = 75,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            _okButton.Click += OnOkClick;
            _cancelButton.Click += (s, e) => Close();

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            Controls.Add(_promptLabel);
            Controls.Add(_inputTextBox);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);
        }

        private void OnOkClick(object? sender, EventArgs e)
        {
            if (_inputTextBox.Text.Length < _config.MinLength)
            {
                MessageBox.Show(
                    $"Input must be at least {_config.MinLength} characters.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                DialogResult = DialogResult.None;
                return;
            }

            if (_config.Required && string.IsNullOrWhiteSpace(_inputTextBox.Text))
            {
                MessageBox.Show(
                    "Input is required.",
                    "Invalid Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                DialogResult = DialogResult.None;
                return;
            }

            Close();
        }

        public static DialogResult Show(string prompt, string title, out string input, InputBoxConfiguration? config = null)
        {
            config ??= new InputBoxConfiguration();
            using var form = new InputBox(prompt, title, config);
            var result = form.ShowDialog();
            input = result == DialogResult.OK ? form.Input : string.Empty;
            return result;
        }

        public static DialogResult Show(string prompt, string title, IWin32Window owner, out string input, InputBoxConfiguration? config = null)
        {
            config ??= new InputBoxConfiguration();
            using var form = new InputBox(prompt, title, config);
            var result = form.ShowDialog(owner);
            input = result == DialogResult.OK ? form.Input : string.Empty;
            return result;
        }
    }

    public class InputBoxConfiguration
    {
        public bool HideInput { get; set; } = false;
        public int MinLength { get; set; } = 0;
        public int MaxLength { get; set; } = 32767;
        public bool Required { get; set; } = false;
        public string DefaultValue { get; set; } = string.Empty;
        public int Width { get; set; } = 410;
        public int Height { get; set; } = 160;
    }
}
