using System;
using System.Windows;

namespace MultiBrowser
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettingsToUI();
        }

        private void LoadSettingsToUI()
        {
            // 1. Số lượng cửa sổ
            switch (_settings.WindowCount)
            {
                case 2:
                    RbOpt2.IsChecked = true;
                    break;
                case 6:
                    RbOpt6.IsChecked = true;
                    break;
                case 8:
                    RbOpt8.IsChecked = true;
                    break;
                case 4:
                default:
                    RbOpt4.IsChecked = true;
                    break;
            }

            // 2. URLs
            TxtTab1Url.Text = _settings.DefaultUrlTab1;
            TxtTab2Url.Text = _settings.DefaultUrlTab2;

            // 3. Automation
            TxtWatchInterval.Text = _settings.AutoWatchIntervalSec.ToString();
            ChkAutoLikePref.IsChecked = _settings.AutoLikeEnabled;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Lấy số lượng cửa sổ
            if (RbOpt2.IsChecked == true) _settings.WindowCount = 2;
            else if (RbOpt6.IsChecked == true) _settings.WindowCount = 6;
            else if (RbOpt8.IsChecked == true) _settings.WindowCount = 8;
            else _settings.WindowCount = 4;

            // Lấy URLs
            if (!string.IsNullOrWhiteSpace(TxtTab1Url.Text))
                _settings.DefaultUrlTab1 = TxtTab1Url.Text.Trim();

            if (!string.IsNullOrWhiteSpace(TxtTab2Url.Text))
                _settings.DefaultUrlTab2 = TxtTab2Url.Text.Trim();

            // Lấy Delay
            if (int.TryParse(TxtWatchInterval.Text, out int interval) && interval >= 1)
                _settings.AutoWatchIntervalSec = interval;
            else
                _settings.AutoWatchIntervalSec = 8;

            // Lấy Auto-Like
            _settings.AutoLikeEnabled = ChkAutoLikePref.IsChecked == true;

            // Lưu ra file settings.json
            _settings.Save();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
