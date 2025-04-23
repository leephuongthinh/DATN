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
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// Ngăn ứng dụng tự shutdown khi cửa sổ đầu tiên đóng
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

			try
			{
				// Khởi tạo hệ thống license
				Console.WriteLine("Initializing license...");
				LicenseService.InitializeFirstRun();
				Console.WriteLine("License initialization completed");

				// Kiểm tra trạng thái license
				var (licenseState, message, expirationDate) = LicenseService.CheckLicense();

				switch (licenseState)
				{
					case LicenseState.Activated:
						if (expirationDate.HasValue && expirationDate.Value > DateTime.UtcNow)
						{
							Console.WriteLine("License đã activated và còn hạn - Starting application");
							StartApplication();
							return;
						}
						else
						{
							MessageBox.Show(
								"License đã hết hạn. Vui lòng kích hoạt lại.",
								"Hết hạn license",
								MessageBoxButton.OK,
								MessageBoxImage.Warning);
							ShowActivationWindow();
						}
						break;

					case LicenseState.Trial:
						if (expirationDate.HasValue && expirationDate.Value > DateTime.UtcNow)
						{
							var remainingTime = expirationDate.Value - DateTime.UtcNow;
							if (MessageBox.Show(
								$"Bạn đang dùng bản dùng thử. Còn {remainingTime.Days} ngày {remainingTime.Hours} giờ.\nBạn có muốn kích hoạt ngay không?",
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
						}
						else
						{
							MessageBox.Show(
								"Thời gian dùng thử đã hết. Vui lòng kích hoạt phần mềm.",
								"Hết hạn dùng thử",
								MessageBoxButton.OK,
								MessageBoxImage.Warning);
							ShowActivationWindow();
						}
						break;

					default:
						MessageBox.Show(
							"Vui lòng kích hoạt phần mềm để sử dụng.",
							"Yêu cầu kích hoạt",
							MessageBoxButton.OK,
							MessageBoxImage.Information);
						ShowActivationWindow();
						break;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Lỗi khởi động: {ex}");
				MessageBox.Show(
					"Có lỗi xảy ra khi khởi động ứng dụng. Vui lòng thử lại.",
					"Lỗi",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				Shutdown();
			}
		}

		private void ShowActivationWindow()
		{
			try
			{
				var activationWindow = new ActivationWindow();
				bool? result = activationWindow.ShowDialog();

				if (result == true)
				{
					// Kiểm tra lại sau khi kích hoạt
					if (LicenseService.IsActivatedAndValid())
					{
						Console.WriteLine("Kích hoạt thành công - Starting application");
						StartApplication();
						return;
					}
					
					MessageBox.Show(
						"Kích hoạt không thành công. Vui lòng thử lại.",
						"Lỗi kích hoạt",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}

				Shutdown();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Lỗi trong quá trình kích hoạt: {ex}");
				MessageBox.Show(
					"Có lỗi xảy ra trong quá trình kích hoạt. Vui lòng thử lại.",
					"Lỗi",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				Shutdown();
			}
		}

		private void StartApplication()
		{
			var mainWindow = new MainWindow();
			this.MainWindow = mainWindow;
			this.ShutdownMode = ShutdownMode.OnMainWindowClose;
			mainWindow.Show();
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


