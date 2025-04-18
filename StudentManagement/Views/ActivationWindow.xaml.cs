using DemoLicenseApp;
using StudentManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace StudentManagement.Views
{
	/// <summary>
	/// Interaction logic for ActivationWindow.xaml
	/// </summary>
	public partial class ActivationWindow : Window
	{
		public ActivationWindow()
		{
			InitializeComponent();
		}

		private void Activate_Click(object sender, RoutedEventArgs e)
		{
			string key = LicenseKeyTextBox.Text.Trim();
			if (LicenseService.ActivateLicense(key))
			{
				MessageBox.Show("Activated successfully!", "Success",
								MessageBoxButton.OK, MessageBoxImage.Information);
				DialogResult = true;
				Close();
			}
			else
			{
				MessageBox.Show("Invalid key. Please try again.", "Error",
								MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}




	}
}
