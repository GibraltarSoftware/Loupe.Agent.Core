using System;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace WinForms6
{
    public partial class MainForm : Form
    {
        private readonly ILogger<MainForm> _logger;
        public MainForm()
        {
            InitializeComponent();
        }

        public MainForm(ILogger<MainForm> logger)
        {
            _logger = logger;

            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            _logger.LogInformation("{FormName} shown", Name);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            _logger.LogInformation("{FormName} closed", Name);
        }
    }
}
