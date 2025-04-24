using StudentManagement.Services;
using StudentManagement.Views;
using System;
using System.Windows;
using System.Windows.Threading;

namespace StudentManagement
{
	public partial class App : Application
	{
		private LicenseService _licenseService;
		private DispatcherTimer _licenseCheckTimer;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// Khởi tạo dịch vụ bản quyền
			_licenseService = new LicenseService();

			// Kiểm tra bản quyền hiện có
			if (!CheckLicenseValidity())
			{
				Shutdown();
				return;
			}

			// Thiết lập kiểm tra bản quyền định kỳ
			//SetupLicenseCheckTimer();

			// Khởi chạy ứng dụng
			StartApplication();
		}
		//protected override void OnStartup(StartupEventArgs e)
		//{
		//	base.OnStartup(e);
		//	_licenseService = new LicenseService();

		//	_licenseService.CheckLicenseOnStartup(); // Kiểm tra ngay khi khởi động

		//	_licenseService.LicenseExpired += () =>
		//	{
		//		Dispatcher.Invoke(() =>
		//		{
		//			var activationWindow = new ActivationWindow(_licenseService)
		//			{
		//				WindowStartupLocation = WindowStartupLocation.CenterScreen,
		//				Topmost = true
		//			};
		//			if (!CheckLicenseValidity())
		//					{
		//						Shutdown();
		//						return;
		//					}
		//				if (activationWindow.ShowDialog() != true || !CheckLicenseValidity())
		//			{
		//				Shutdown();
		//			}
		//		});
		//	};

		//	if (!_licenseService.IsValid)
		//	{
		//		var activationWindow = new ActivationWindow(_licenseService);
		//		if (activationWindow.ShowDialog() != true)
		//		{
		//			Shutdown();
		//			return;
		//		}
		//	}

		//	StartApplication();
		//}

		private bool CheckLicenseValidity()
		{
			// Kiểm tra bản quyền đã lưu
			bool hasValidLicense = _licenseService.CheckExistingLicense();

			// Nếu không có bản quyền hợp lệ, hiển thị cửa sổ kích hoạt
			if (!hasValidLicense)
			{
				var activationWindow = new ActivationWindow(_licenseService)
				{
					WindowStartupLocation = WindowStartupLocation.CenterScreen
				};

				activationWindow.ShowDialog();

				if (!_licenseService.IsValid)
				{
					MessageBox.Show(
						"Bạn cần có bản quyền hợp lệ để sử dụng ứng dụng.\n\n" +
						"Vui lòng liên hệ nhà cung cấp để được hỗ trợ.",
						"Yêu Cầu Bản Quyền",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return false;
				}
			}

			return true;
		}

		//private void SetupLicenseCheckTimer()
		//{
		//	_licenseCheckTimer = new DispatcherTimer
		//	{
		//		Interval = TimeSpan.FromMinutes(1) // Kiểm tra mỗi phút
		//	};

		//	_licenseCheckTimer.Tick += (s, e) =>
		//	{
		//		if (!_licenseService.IsValid)
		//		{
		//			_licenseCheckTimer.Stop();
		//			HandleLicenseExpired();
		//		}
		//	};

		//	_licenseCheckTimer.Start();
		//}

		private void HandleLicenseExpired()
		{
			// Hiển thị thông báo trên UI thread
			Dispatcher.Invoke(() =>
			{
				MessageBox.Show(
					"Phiên bản dùng thử của bạn đã hết hạn.\n" +
					$"Thời gian còn lại: {_licenseService.RemainingTime:mm\\:ss}\n" +
					"Vui lòng kích hoạt bản quyền đầy đủ.",
					"Hết Hạn Bản Quyền",
					MessageBoxButton.OK,
					MessageBoxImage.Information);

				// Hiển thị lại cửa sổ kích hoạt
				var activationWindow = new ActivationWindow(_licenseService)
				{
					WindowStartupLocation = WindowStartupLocation.CenterScreen
				};

				activationWindow.ShowDialog();

				// Nếu không kích hoạt thành công, đóng ứng dụng
				if (!_licenseService.IsValid)
				{
					Shutdown();
				}
				else
				{
					// Nếu kích hoạt thành công, tiếp tục sử dụng
					_licenseCheckTimer.Start();
				}
			});
		}

		private void StartApplication()
		{
			try
			{
				var mainWindow = new MainWindow();
				MainWindow = mainWindow;
				//mainWindow.Show();
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					$"Không thể khởi động ứng dụng: {ex.Message}",
					"Lỗi Khởi Động",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				Shutdown();
			}
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_licenseCheckTimer?.Stop();
			base.OnExit(e);
		}
	}
}