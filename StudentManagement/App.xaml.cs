using System;
using System.Management;
using System.Windows;
using DemoLicenseApp;
using StudentManagement.Views;
using System.IO;


namespace StudentManagement
{
	public partial class App : Application
	{
		//protected override void OnStartup(StartupEventArgs e)
		//{
		//	// 1) Ngăn WPF tự động shutdown khi cửa sổ đầu tiên (ActivationWindow) đóng
		//	this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

		//	base.OnStartup(e);

		//	// 2) Hiển thị cửa sổ nhập mã kích hoạt
		//	var dlg = new ActivationWindow();
		//	bool isActivated = dlg.ShowDialog() ?? false;

		//	if (isActivated)
		//	{
		//		var (state, msg) = LicenseService.CheckLicense();
		//		Console.WriteLine($"License state: {state}, Message: {msg}");

		//		if (state == LicenseState.Activated || state == LicenseState.Trial)
		//		{
		//			// 3) Khởi tạo và gán MainWindow
		//			var main = new MainWindow();
		//			this.MainWindow = main;

		//			// 4) Chuyển ShutdownMode để app chỉ tắt khi MainWindow đóng
		//			this.ShutdownMode = ShutdownMode.OnMainWindowClose;

		//			// 5) Mở MainWindow
		//			main.Show();
		//			return;   // thoát OnStartup, giữ app chạy
		//		}
		//		else
		//		{
		//			MessageBox.Show(
		//				"Activation failed. The application will now exit.",
		//				"License",
		//				MessageBoxButton.OK,
		//				MessageBoxImage.Error
		//			);
		//		}
		//	}
		//	else
		//	{
		//		MessageBox.Show(
		//			"Activation was not completed. The application will now exit.",
		//			"License",
		//			MessageBoxButton.OK,
		//			MessageBoxImage.Error
		//		);
		//	}

		//	// 6) Nếu không kích hoạt, tắt hoàn toàn
		//	Shutdown();
		//}
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// Ngăn ứng dụng tự shutdown khi cửa sổ đầu tiên đóng
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

			// Khởi tạo hệ thống license
			LicenseService.InitializeFirstRun();

			// Kiểm tra trạng thái license
			var (licenseState, message) = LicenseService.CheckLicense();

			switch (licenseState)
			{
				case LicenseState.Activated:
					StartApplication();
					break;

				case LicenseState.Trial:
					if (MessageBox.Show(
						$"Bạn đang dùng bản dùng thử. {message}\nBạn có muốn kích hoạt ngay không?",
						"Bản dùng thử",
						MessageBoxButton.YesNo,
						MessageBoxImage.Question) == MessageBoxResult.Yes)
					{
						ShowActivationWindow();
					}
					else
					{
						StartApplication();
					}
					break;

				case LicenseState.Expired:
					MessageBox.Show(
						"Bản dùng thử đã hết hạn. Vui lòng kích hoạt phần mềm.",
						"Hết hạn",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					ShowActivationWindow();
					break;

				case LicenseState.Invalid:
				default:
					MessageBox.Show(
						"License không hợp lệ. Vui lòng kích hoạt phần mềm.",
						"Lỗi License",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
					ShowActivationWindow();
					break;
			}
		}

		private void ShowActivationWindow()
		{
			var activationWindow = new ActivationWindow();
			bool? result = activationWindow.ShowDialog();

			if (result == true)
			{
				// Kích hoạt thành công
				StartApplication();
			}
			else
			{
				// Hủy kích hoạt
				MessageBox.Show(
					"Ứng dụng không thể chạy khi chưa kích hoạt. Đang thoát...",
					"Yêu cầu kích hoạt",
					MessageBoxButton.OK,
					MessageBoxImage.Stop);
				Shutdown();
			}
		}

		private void StartApplication()
		{
			var mainWindow = new MainWindow();
			this.MainWindow = mainWindow;
			this.ShutdownMode = ShutdownMode.OnMainWindowClose;
			//mainWindow.Show();
		}

		public static string GetMachineId()
		{
			try
			{
				// Thử nhiều cách lấy ID phần cứng
				string identifier = GetHardDiskSerial() ?? GetProcessorId() ?? GetBiosSerial();
				return string.IsNullOrWhiteSpace(identifier) ? "UnknownMachineId" : identifier.Trim();
			}
			catch
			{
				return "ErrorGettingMachineId";
			}
		}

		private static string GetHardDiskSerial()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia"))
				{
					foreach (ManagementObject disk in searcher.Get())
					{
						string serial = disk["SerialNumber"]?.ToString();
						if (!string.IsNullOrWhiteSpace(serial))
							return serial;
					}
				}
			}
			catch { }
			return null;
		}

		private static string GetProcessorId()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
				{
					foreach (ManagementObject cpu in searcher.Get())
					{
						string id = cpu["ProcessorId"]?.ToString();
						if (!string.IsNullOrWhiteSpace(id))
							return id;
					}
				}
			}
			catch { }
			return null;
		}

		private static string GetBiosSerial()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
				{
					foreach (ManagementObject bios in searcher.Get())
					{
						string serial = bios["SerialNumber"]?.ToString();
						if (!string.IsNullOrWhiteSpace(serial))
							return serial;
					}
				}
			}
			catch { }
			return null;
		}
	}


}


