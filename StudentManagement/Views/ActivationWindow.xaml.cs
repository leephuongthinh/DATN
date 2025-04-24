using StudentManagement.Services;
using System.Windows;
using System.Windows.Controls;

namespace StudentManagement.Views
{
	public partial class ActivationWindow : Window
	{
		private readonly LicenseService _licenseService;

		public ActivationWindow(LicenseService licenseService)
		{
			InitializeComponent();
			_licenseService = licenseService;
			LicenseKeyTextBox.Focus();
		}

		private void Activate_Click(object sender, RoutedEventArgs e)
		{
			string licenseKey = LicenseKeyTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(licenseKey))
			{
				ShowErrorMessage("Vui lòng nhập mã kích hoạt");
				return;
			}

			// Kiểm tra cả key mặc định và key tự sinh
			if (_licenseService.ValidateLicense(licenseKey))
			{
				DialogResult = true;
				Close();
			}
			else
			{
				ShowErrorMessage(GetLicenseFormatGuidance());
			}
		}

		private string GetLicenseFormatGuidance()
		{
			return "Mã kích hoạt không hợp lệ.";
		}

		private void LicenseKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			// Tự động thêm dấu gạch ngang cho định dạng key tự động
			var textBox = sender as TextBox;
			if (textBox == null) return;

			string text = textBox.Text.Replace("-", "");
			if (text.Length > 0 && !text.StartsWith("DEMO")) // Không áp dụng cho key mặc định
			{
				textBox.Text = FormatWithDashes(text);
				textBox.CaretIndex = textBox.Text.Length;
			}

			StatusMessage.Visibility = Visibility.Collapsed;
		}

		private string FormatWithDashes(string input)
		{
			if (input.Length <= 5) return input;
			if (input.Length <= 11) return $"{input.Substring(0, 5)}-{input.Substring(5)}";
			return $"{input.Substring(0, 5)}-{input.Substring(5, 6)}-{input.Substring(11)}";
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void ShowErrorMessage(string message)
		{
			StatusMessage.Text = message;
			StatusMessage.Visibility = Visibility.Visible;
		}
		public bool ShowActivateButton
		{
			get { return ActivateButton.Visibility == Visibility.Visible; }
			set { ActivateButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
		}
	}
}