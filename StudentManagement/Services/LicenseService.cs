//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Security.Cryptography;
//using System.Text;
//using System.Windows.Threading;
//using Newtonsoft.Json;

//public class LicenseService
//{
//	// Configuration
//	private const string LicenseFilePath = "license.dat";
//	private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("YourSecureKey@123".PadRight(32)); // 256-bit key

//	// Default keys
//	private static readonly Dictionary<string, LicenseType> _predefinedKeys = new Dictionary<string, LicenseType>
//	{
//		{ "DEMO-1MOTH-ABCDE", LicenseType.Trial1Moth },
//		{ "DEMO-6MOTH-XYZ12", LicenseType.Trial6Moth },
//		{ "DEMO-PERM-12345", LicenseType.Permanent }
//	};

//	// Properties
//	public LicenseType CurrentLicense { get; private set; } = LicenseType.Invalid;
//	public DateTime ActivationTime { get; private set; }
//	public DateTime ExpiryTime { get; private set; }
//	public bool IsValid => CurrentLicense != LicenseType.Invalid && DateTime.Now <= ExpiryTime;
//	public TimeSpan RemainingTime => IsValid ? ExpiryTime - DateTime.Now : TimeSpan.Zero;

//	private DispatcherTimer _licenseCheckTimer;

//	public enum LicenseType
//	{
//		Trial1Moth,
//		Trial6Moth,
//		Permanent,
//		Invalid
//	}

//	public event Action LicenseExpired;
//	public event Action LicenseValidated;

//	public LicenseService()
//	{
//		InitializeLicenseCheckTimer();
//	}
//	//public void CheckLicenseOnStartup()
//	//{
//	//	if (!File.Exists(LicenseFilePath)) return;

//	//	try
//	//	{
//	//		var licenseData = JsonConvert.DeserializeObject<LicenseData>(DecryptLicenseFile());
//	//		if (licenseData == null) return;

//	//		// Kiểm tra nếu đã hết hạn khi khởi động
//	//		if (DateTime.Now > licenseData.ExpiryTime)
//	//		{
//	//			File.Delete(LicenseFilePath);
//	//			// Gửi sự kiện yêu cầu kích hoạt lại
//	//			LicenseExpired?.Invoke();
//	//		}
//	//	}
//	//	catch { /* Xử lý lỗi */ }
//	//}
//	public void CheckLicenseOnStartup()
//	{
//		if (!File.Exists(LicenseFilePath))
//		{
//			CurrentLicense = LicenseType.Invalid;
//			return;
//		}

//		try
//		{
//			string decryptedData = DecryptLicenseFile();
//			var licenseData = JsonConvert.DeserializeObject<LicenseData>(decryptedData);

//			if (licenseData == null ||
//				string.IsNullOrEmpty(licenseData.Key) ||
//				licenseData.ExpiryTime == default)
//			{
//				File.Delete(LicenseFilePath);
//				CurrentLicense = LicenseType.Invalid;
//				return;
//			}

//			// Cập nhật trạng thái từ file
//			CurrentLicense = licenseData.LicenseType;
//			ActivationTime = licenseData.ActivationTime;
//			ExpiryTime = licenseData.ExpiryTime;

//			// Kích hoạt sự kiện nếu đã hết hạn
//			if (DateTime.Now > ExpiryTime)
//			{
//				File.Delete(LicenseFilePath);
//				CurrentLicense = LicenseType.Invalid;
//				LicenseExpired?.Invoke();
//			}
//		}
//		catch
//		{
//			try { File.Delete(LicenseFilePath); } catch { }
//			CurrentLicense = LicenseType.Invalid;
//		}
//	}
//	public bool CheckExistingLicense()
//	{
//		if (!File.Exists(LicenseFilePath)) return false;

//		try
//		{
//			string decryptedData = DecryptLicenseFile();
//			var licenseData = JsonConvert.DeserializeObject<LicenseData>(decryptedData);

//			// Validate the license data
//			if (licenseData == null ||
//				string.IsNullOrEmpty(licenseData.Key) ||
//				licenseData.ExpiryTime == default)
//			{
//				File.Delete(LicenseFilePath);
//				return false;
//			}

//			// Chỉ cập nhật thời gian nếu đây là lần đầu kích hoạt
//			if (CurrentLicense == LicenseType.Invalid)
//			{
//				ActivationTime = licenseData.ActivationTime;
//				ExpiryTime = licenseData.ExpiryTime;
//			}

//			// Kiểm tra nếu đã hết hạn
//			if (DateTime.Now > ExpiryTime)
//			{
//				File.Delete(LicenseFilePath);
//				return false;
//			}

//			return true;
//		}
//		catch
//		{
//			try { File.Delete(LicenseFilePath); } catch { }
//			return false;
//		}
//	}

//	private void ActivateLicense(LicenseType type, string licenseKey)
//	{
//		// Chỉ thiết lập thời gian nếu đây là lần đầu kích hoạt
//		if (CurrentLicense == LicenseType.Invalid)
//		{
//			CurrentLicense = type;
//			ActivationTime = DateTime.Now;

//			// Tính toán thời gian hết hạn
//			switch (type)
//			{
//				case LicenseType.Trial1Moth:
//					ExpiryTime = ActivationTime.AddMonths(1);
//					break;
//				case LicenseType.Trial6Moth:
//					ExpiryTime = ActivationTime.AddMonths(6);
//					break;
//				case LicenseType.Permanent:
//					ExpiryTime = DateTime.MaxValue;
//					break;
//				default:
//					ExpiryTime = DateTime.MinValue;
//					break;
//			}

//			// Lưu thông tin license
//			SaveLicenseData(licenseKey);
//		}

//		// Luôn khởi động timer cho bản quyền tạm thời
//		if (type != LicenseType.Permanent)
//		{
//			_licenseCheckTimer.Start();
//		}
//	}

//	public bool ValidateLicense(string licenseKey)
//	{
//		if (string.IsNullOrWhiteSpace(licenseKey)) return false;

//		// Check predefined keys first
//		if (_predefinedKeys.TryGetValue(licenseKey, out var predefinedType))
//		{
//			ActivateLicense(predefinedType, licenseKey);
//			LicenseValidated?.Invoke();
//			return true;
//		}

//		// Check generated keys
//		if (ValidateGeneratedLicense(licenseKey))
//		{
//			LicenseValidated?.Invoke();
//			return true;
//		}

//		return false;
//	}

//	private bool ValidateGeneratedLicense(string licenseKey)
//	{
//		var parts = licenseKey.Split('-');
//		if (parts.Length != 3) return false;

//		LicenseType type;
//		switch (parts[0])
//		{
//			case "TEMP1": type = LicenseType.Trial1Moth; break;
//			case "TEMP60": type = LicenseType.Trial6Moth; break;
//			case "PERM": type = LicenseType.Permanent; break;
//			default: type = LicenseType.Invalid; break;
//		}

//		if (type == LicenseType.Invalid) return false;
//		if (!ValidateDatePart(parts[1])) return false;
//		if (!VerifyLicenseSignature(parts[2], parts[0] + parts[1])) return false;

//		ActivateLicense(type, licenseKey);
//		return true;
//	}



//	private void SaveLicenseData(string licenseKey)
//	{
//		var licenseData = new LicenseData
//		{
//			Key = licenseKey,
//			ActivationTime = this.ActivationTime,
//			ExpiryTime = this.ExpiryTime,
//			LicenseType = this.CurrentLicense
//		};

//		string serializedData = JsonConvert.SerializeObject(licenseData);
//		byte[] encrypted = EncryptString(serializedData);

//		try
//		{
//			File.WriteAllBytes(LicenseFilePath, encrypted);
//		}
//		catch (Exception ex)
//		{
//			throw new ApplicationException("License save error: " + ex.Message);
//		}
//	}

//	public static string GenerateLicenseKey(LicenseType type)
//	{
//		string prefix;
//		switch (type)
//		{
//			case LicenseType.Trial1Moth: prefix = "TEMP1"; break;
//			case LicenseType.Trial6Moth: prefix = "TEMP60"; break;
//			case LicenseType.Permanent: prefix = "PERM"; break;
//			default: throw new ArgumentException("Invalid license type");
//		}

//		string datePart = DateTime.Now.ToString("yyyyMMdd");
//		string data = prefix + datePart;
//		string signature = GenerateSignature(data);

//		return string.Format("{0}-{1}-{2}", prefix, datePart, signature);
//	}

//	private static string GenerateSignature(string input)
//	{
//		using (var sha256 = SHA256.Create())
//		{
//			byte[] inputBytes = Encoding.UTF8.GetBytes(input + Convert.ToBase64String(EncryptionKey));
//			byte[] hashBytes = sha256.ComputeHash(inputBytes);
//			return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
//		}
//	}

//	private bool VerifyLicenseSignature(string signature, string data)
//	{
//		return signature == GenerateSignature(data);
//	}

//	private bool ValidateDatePart(string datePart)
//	{
//		return datePart.Length == 8 &&
//			   DateTime.TryParseExact(datePart, "yyyyMMdd", null,
//				   System.Globalization.DateTimeStyles.None, out _);
//	}

//	private string DecryptLicenseFile()
//	{
//		byte[] encrypted = File.ReadAllBytes(LicenseFilePath);
//		return DecryptString(encrypted);
//	}

//	private byte[] EncryptString(string plainText)
//	{
//		using (Aes aes = Aes.Create())
//		{
//			aes.Key = EncryptionKey;
//			aes.IV = new byte[16];

//			using (var ms = new MemoryStream())
//			{
//				using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
//				{
//					using (var sw = new StreamWriter(cs))
//					{
//						sw.Write(plainText);
//					}
//				}
//				return ms.ToArray();
//			}
//		}
//	}

//	private string DecryptString(byte[] cipherText)
//	{
//		using (Aes aes = Aes.Create())
//		{
//			aes.Key = EncryptionKey;
//			aes.IV = new byte[16];

//			using (var ms = new MemoryStream(cipherText))
//			{
//				using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
//				{
//					using (var sr = new StreamReader(cs))
//					{
//						return sr.ReadToEnd();
//					}
//				}
//			}
//		}
//	}

//	private void InitializeLicenseCheckTimer()
//	{
//		_licenseCheckTimer = new DispatcherTimer
//		{
//			Interval = TimeSpan.FromSeconds(30)
//		};
//		_licenseCheckTimer.Tick += (s, e) => CheckLicenseValidity();
//	}

//	private void CheckLicenseValidity()
//	{
//		if (DateTime.Now > ExpiryTime)
//		{
//			_licenseCheckTimer.Stop();
//			CurrentLicense = LicenseType.Invalid;
//			LicenseExpired?.Invoke();

//			try { File.Delete(LicenseFilePath); } catch { }
//		}
//	}

//	// Helper class for license data serialization
//	private class LicenseData
//	{
//		public string Key { get; set; }
//		public DateTime ActivationTime { get; set; }
//		public DateTime ExpiryTime { get; set; }
//		public LicenseType LicenseType { get; set; }
//	}
//}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using DocumentFormat.OpenXml.EMMA;
using Newtonsoft.Json;
using StudentManagement.Models;

public class LicenseService
{
	// Cấu hình
	private const string LicenseFilePath = "license.dat"; // Đường dẫn file bản quyền
	private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("YourSecureKey@123".PadRight(32)); // Khóa mã hóa 256-bit

	// Danh sách key mặc định
	private static readonly Dictionary<string, LicenseType> _predefinedKeys = new Dictionary<string, LicenseType>
	{
		{ "DEMO-1MOTH-ABCDE", LicenseType.Trial1Moth },
		{ "DEMO-6MOTH-XYZ12", LicenseType.Trial6Moth },
		{ "DEMO-PERM-12345", LicenseType.Permanent }
	};

	// Thuộc tính
	public LicenseType CurrentLicense { get; private set; } = LicenseType.Invalid; // Loại bản quyền hiện tại
	public DateTime ActivationTime { get; private set; } // Thời gian kích hoạt
	public DateTime ExpiryTime { get; private set; } // Thời gian hết hạn
	public bool IsValid => CurrentLicense != LicenseType.Invalid && DateTime.Now <= ExpiryTime; // Kiểm tra bản quyền hợp lệ
	public TimeSpan RemainingTime => IsValid ? ExpiryTime - DateTime.Now : TimeSpan.Zero; // Thời gian còn lại

	private DispatcherTimer _licenseCheckTimer; // Timer kiểm tra bản quyền

	public enum LicenseType
	{
		Trial1Moth, // Thử nghiệm 1 tháng
		Trial6Moth, // Thử nghiệm 6 tháng
		Permanent,  // Vĩnh viễn
		Invalid     // Không hợp lệ
	}

	public event Action LicenseExpired; // Sự kiện khi bản quyền hết hạn
	public event Action LicenseValidated; // Sự kiện khi bản quyền được xác thực

	public LicenseService()
	{
		InitializeLicenseCheckTimer(); // Khởi tạo timer
	}

	// Kiểm tra bản quyền khi khởi động
	public void CheckLicenseOnStartup()
	{
		if (!File.Exists(LicenseFilePath))
		{
			CurrentLicense = LicenseType.Invalid;
			return;
		}

		try
		{
			string decryptedData = DecryptLicenseFile();
			var licenseData = JsonConvert.DeserializeObject<LicenseData>(decryptedData);

			if (licenseData == null ||
				string.IsNullOrEmpty(licenseData.Key) ||
				licenseData.ExpiryTime == default)
			{
				File.Delete(LicenseFilePath);
				CurrentLicense = LicenseType.Invalid;
				return;
			}

			// Cập nhật trạng thái từ file
			CurrentLicense = licenseData.LicenseType;
			ActivationTime = licenseData.ActivationTime;
			ExpiryTime = licenseData.ExpiryTime;

			// Kích hoạt sự kiện nếu đã hết hạn
			if (DateTime.Now > ExpiryTime)
			{
				File.Delete(LicenseFilePath);
				CurrentLicense = LicenseType.Invalid;
				LicenseExpired?.Invoke();
			}
		}
		catch
		{
			try { File.Delete(LicenseFilePath); } catch { }
			CurrentLicense = LicenseType.Invalid;
		}
	}

	// Kiểm tra bản quyền hiện có
	public bool CheckExistingLicense()
	{
		if (!File.Exists(LicenseFilePath)) return false;

		try
		{
			string decryptedData = DecryptLicenseFile();
			var licenseData = JsonConvert.DeserializeObject<LicenseData>(decryptedData);

			if (licenseData == null ||
				string.IsNullOrEmpty(licenseData.Key) ||
				licenseData.ExpiryTime == default)
			{
				File.Delete(LicenseFilePath);
				return false;
			}

			if (CurrentLicense == LicenseType.Invalid)
			{
				ActivationTime = licenseData.ActivationTime;
				ExpiryTime = licenseData.ExpiryTime;
			}

			if (DateTime.Now > ExpiryTime)
			{
				File.Delete(LicenseFilePath);
				return false;
			}

			return true;
		}
		catch
		{
			try { File.Delete(LicenseFilePath); } catch { }
			return false;
		}
	}

	// Kích hoạt bản quyền
	private void ActivateLicense(LicenseType type, string licenseKey)
	{
		if (CurrentLicense == LicenseType.Invalid)
		{
			CurrentLicense = type;
			ActivationTime = DateTime.Now;

			// Tính thời gian hết hạn
			switch (type)
			{
				case LicenseType.Trial1Moth:
					ExpiryTime = ActivationTime.AddMonths(1);
					break;
				case LicenseType.Trial6Moth:
					ExpiryTime = ActivationTime.AddMonths(6);
					break;
				case LicenseType.Permanent:
					ExpiryTime = DateTime.MaxValue;
					break;
				default:
					ExpiryTime = DateTime.MinValue;
					break;
			}

			// Lưu vào file
			SaveLicenseData(licenseKey);

			// Với Trial1Moth, lưu vào cơ sở dữ liệu với token
			if (type == LicenseType.Trial1Moth)
			{
				string token = Guid.NewGuid().ToString(); // Tạo token duy nhất
				SaveLicenseToDatabase(licenseKey, token, type, ActivationTime, ExpiryTime);
			}
		}

		// Khởi động timer cho bản quyền tạm thời
		if (type != LicenseType.Permanent)
		{
			_licenseCheckTimer.Start();
		}
	}

	// Xác thực key bản quyền
	public bool ValidateLicense(string licenseKey)
	{
		if (string.IsNullOrWhiteSpace(licenseKey)) return false;

		// Kiểm tra key mặc định
		if (_predefinedKeys.TryGetValue(licenseKey, out var predefinedType) && predefinedType == LicenseType.Trial1Moth)
		{
			if (IsKeyUsedInDatabase(licenseKey))
			{
				return false; // Key đã được sử dụng
			}
			ActivateLicense(predefinedType, licenseKey);
			LicenseValidated?.Invoke();
			return true;
		}
		else if (_predefinedKeys.TryGetValue(licenseKey, out predefinedType))
		{
			ActivateLicense(predefinedType, licenseKey);
			LicenseValidated?.Invoke();
			return true;
		}

		// Kiểm tra key động
		if (ValidateGeneratedLicense(licenseKey))
		{
			LicenseValidated?.Invoke();
			return true;
		}

		return false;
	}

	// Xác thực key động
	private bool ValidateGeneratedLicense(string licenseKey)
	{
		var parts = licenseKey.Split('-');
		if (parts.Length != 3) return false;

		LicenseType type;
		switch (parts[0])
		{
			case "TEMP1": type = LicenseType.Trial1Moth; break;
			case "TEMP60": type = LicenseType.Trial6Moth; break;
			case "PERM": type = LicenseType.Permanent; break;
			default: type = LicenseType.Invalid; break;
		}

		if (type == LicenseType.Invalid) return false;
		if (!ValidateDatePart(parts[1])) return false;
		if (!VerifyLicenseSignature(parts[2], parts[0] + parts[1])) return false;

		// Với Trial1Moth, kiểm tra key đã dùng chưa
		if (type == LicenseType.Trial1Moth && IsKeyUsedInDatabase(licenseKey))
		{
			return false; // Key đã được sử dụng
		}

		ActivateLicense(type, licenseKey);
		return true;
	}

	// Kiểm tra key đã được sử dụng trong cơ sở dữ liệu chưa (sử dụng Entity Framework)
	// Kiểm tra xem LicenseKey đã được sử dụng và đang hoạt động hay chưa
	private bool IsKeyUsedInDatabase(string licenseKey)
	{
		using (var context = new StudentManagementEntities()) // DbContext thực tế
		{
			return context.Security.Any(s => s.LicenseKey == licenseKey && s.IsActive);
		}
	}

	// Lưu thông tin License vào cơ sở dữ liệu
	private void SaveLicenseToDatabase(string licenseKey, string token, LicenseType type, DateTime activationTime, DateTime expiryTime)
	{
		using (var context = new StudentManagementEntities()) // DbContext thực tế
		{
			var licenseToken = new Security
			{
				LicenseKey = licenseKey,
				Token = token,
				LicenseType = type.ToString(),
				ActivationTime = activationTime,
				ExpiryTime = expiryTime,
				IsActive = true,
				CreatedAt = DateTime.Now
			};

			context.Security.Add(licenseToken); // DbSet đúng là Security
			context.SaveChanges();
		}
	}



	// Lưu thông tin bản quyền vào file
	private void SaveLicenseData(string licenseKey)
	{
		var licenseData = new LicenseData
		{
			Key = licenseKey,
			ActivationTime = this.ActivationTime,
			ExpiryTime = this.ExpiryTime,
			LicenseType = this.CurrentLicense
		};

		string serializedData = JsonConvert.SerializeObject(licenseData);
		byte[] encrypted = EncryptString(serializedData);

		try
		{
			File.WriteAllBytes(LicenseFilePath, encrypted);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("Lỗi lưu bản quyền: " + ex.Message);
		}
	}

	// Tạo key bản quyền
	public static string GenerateLicenseKey(LicenseType type)
	{
		string prefix;
		switch (type)
		{
			case LicenseType.Trial1Moth: prefix = "TEMP1"; break;
			case LicenseType.Trial6Moth: prefix = "TEMP60"; break;
			case LicenseType.Permanent: prefix = "PERM"; break;
			default: throw new ArgumentException("Loại bản quyền không hợp lệ");
		}

		string datePart = DateTime.Now.ToString("yyyyMMdd");
		string data = prefix + datePart;
		string signature = GenerateSignature(data);

		return string.Format("{0}-{1}-{2}", prefix, datePart, signature);
	}

	// Tạo chữ ký
	private static string GenerateSignature(string input)
	{
		using (var sha256 = SHA256.Create())
		{
			byte[] inputBytes = Encoding.UTF8.GetBytes(input + Convert.ToBase64String(EncryptionKey));
			byte[] hashBytes = sha256.ComputeHash(inputBytes);
			return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
		}
	}

	// Xác minh chữ ký
	private bool VerifyLicenseSignature(string signature, string data)
	{
		return signature == GenerateSignature(data);
	}

	// Kiểm tra định dạng ngày
	private bool ValidateDatePart(string datePart)
	{
		return datePart.Length == 8 &&
			   DateTime.TryParseExact(datePart, "yyyyMMdd", null,
				   System.Globalization.DateTimeStyles.None, out _);
	}

	// Giải mã file bản quyền
	private string DecryptLicenseFile()
	{
		byte[] encrypted = File.ReadAllBytes(LicenseFilePath);
		return DecryptString(encrypted);
	}

	// Mã hóa chuỗi
	private byte[] EncryptString(string plainText)
	{
		using (Aes aes = Aes.Create())
		{
			aes.Key = EncryptionKey;
			aes.IV = new byte[16];

			using (var ms = new MemoryStream())
			{
				using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
				{
					using (var sw = new StreamWriter(cs))
					{
						sw.Write(plainText);
					}
				}
				return ms.ToArray();
			}
		}
	}

	// Giải mã chuỗi
	private string DecryptString(byte[] cipherText)
	{
		using (Aes aes = Aes.Create())
		{
			aes.Key = EncryptionKey;
			aes.IV = new byte[16];

			using (var ms = new MemoryStream(cipherText))
			{
				using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
				{
					using (var sr = new StreamReader(cs))
					{
						return sr.ReadToEnd();
					}
				}
			}
		}
	}

	// Khởi tạo timer kiểm tra bản quyền
	private void InitializeLicenseCheckTimer()
	{
		_licenseCheckTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(30)
		};
		_licenseCheckTimer.Tick += (s, e) => CheckLicenseValidity();
	}

	// Kiểm tra tính hợp lệ của bản quyền
	private void CheckLicenseValidity()
	{
		if (DateTime.Now > ExpiryTime)
		{
			_licenseCheckTimer.Stop();
			CurrentLicense = LicenseType.Invalid;
			LicenseExpired?.Invoke();

			try { File.Delete(LicenseFilePath); } catch { }
		}
	}

	// Lớp hỗ trợ cho việc tuần tự hóa dữ liệu bản quyền
	private class LicenseData
	{
		public string Key { get; set; }
		public DateTime ActivationTime { get; set; }
		public DateTime ExpiryTime { get; set; }
		public LicenseType LicenseType { get; set; }
	}
}