using System;
using System.Drawing;
using System.Windows.Forms;

namespace TempWidget
{
    /// <summary>First-run screen: one button sets up everything needed for full sensors.</summary>
    internal sealed class WelcomeForm : Form
    {
        private readonly CheckBox _autoStart;

        /// <summary>True: full sensors requested. False: basic sensors. Only valid after OK.</summary>
        public bool FullSensors { get; private set; }
        public bool StartWithWindows => _autoStart.Checked;

        public WelcomeForm()
        {
            Text = "Welcome to TempWidget";
            SuspendLayout(); // controls are laid out for 96 DPI; scaling happens once, in ResumeLayout
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(470, 352);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Controls.Add(new Label { Text = "Set up TempWidget", Font = new Font("Segoe UI Semibold", 13f), AutoSize = true, Location = new Point(20, 18) });
            Controls.Add(new Label
            {
                Text = "CPU temperature, drive temperatures and fan speeds are only available to programs with administrator rights and a hardware driver.",
                Location = new Point(22, 56), Size = new Size(426, 36),
            });

            Controls.Add(new Label { Text = "Full setup does this for you:", Font = new Font("Segoe UI Semibold", 9f), AutoSize = true, Location = new Point(22, 102) });
            Controls.Add(new Label
            {
                Text = "•  Asks Windows for administrator rights (one prompt)\r\n" +
                       (DriverSetup.IsInstalled()
                           ? "\u2022  Uses the PawnIO sensor driver that is already on this PC\r\n"
                           : "\u2022  Installs PawnIO " + DriverSetup.BundledVersion + ", a free, signed sensor driver\r\n") +
                       "•  Nothing is downloaded and nothing is sent anywhere",
                Location = new Point(30, 124), Size = new Size(418, 74),
            });

            _autoStart = new CheckBox { Text = "Start TempWidget with Windows", Checked = true, Location = new Point(24, 206), Size = new Size(300, 22) };
            Controls.Add(_autoStart);

            var full = new Button { Text = "Set up full sensors", FlatStyle = FlatStyle.System };
            full.SetBounds(22, 244, 426, 36);
            full.Click += (s, e) => { FullSensors = true; DialogResult = DialogResult.OK; };
            full.HandleCreated += (s, e) => { if (!Program.IsElevated) Native.ShowShield(full.Handle); };
            Controls.Add(full);

            var basic = new Button { Text = "Continue with basic sensors (GPU, memory, battery, CPU load)" };
            basic.SetBounds(22, 288, 426, 30);
            basic.Click += (s, e) => { FullSensors = false; DialogResult = DialogResult.OK; };
            Controls.Add(basic);

            Controls.Add(new Label { Text = "You can change this later from the widget's right-click menu.", ForeColor = SystemColors.GrayText, AutoSize = true, Location = new Point(22, 326) });
            AcceptButton = full;
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
