using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using Newtonsoft.Json;

public class LicenseService
{
	// Configuration
	private const string LicenseFilePath = "license.dat";
	private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("YourSecureKey@123".PadRight(32)); // 256-bit key

	// Default keys
	private static readonly Dictionary<string, LicenseType> _predefinedKeys = new Dictionary<string, LicenseType>
	{
		{ "DEMO-5MIN-ABCDE", LicenseType.Trial5Min },
		{ "DEMO-1HOUR-XYZ12", LicenseType.Trial60Min },
		{ "DEMO-PERM-12345", LicenseType.Permanent }
	};

	// Properties
	public LicenseType CurrentLicense { get; private set; } = LicenseType.Invalid;
	public DateTime ActivationTime { get; private set; }
	public DateTime ExpiryTime { get; private set; }
	public bool IsValid => CurrentLicense != LicenseType.Invalid && DateTime.Now <= ExpiryTime;
	public TimeSpan RemainingTime => IsValid ? ExpiryTime - DateTime.Now : TimeSpan.Zero;

	private DispatcherTimer _licenseCheckTimer;

	public enum LicenseType
	{
		Trial5Min,
		Trial60Min,
		Permanent,
		Invalid
	}

	public event Action LicenseExpired;
	public event Action LicenseValidated;

	public LicenseService()
	{
		InitializeLicenseCheckTimer();
	}
	//public void CheckLicenseOnStartup()
	//{
	//	if (!File.Exists(LicenseFilePath)) return;

	//	try
	//	{
	//		var licenseData = JsonConvert.DeserializeObject<LicenseData>(DecryptLicenseFile());
	//		if (licenseData == null) return;

	//		// Kiểm tra nếu đã hết hạn khi khởi động
	//		if (DateTime.Now > licenseData.ExpiryTime)
	//		{
	//			File.Delete(LicenseFilePath);
	//			// Gửi sự kiện yêu cầu kích hoạt lại
	//			LicenseExpired?.Invoke();
	//		}
	//	}
	//	catch { /* Xử lý lỗi */ }
	//}
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
	public bool CheckExistingLicense()
	{
		if (!File.Exists(LicenseFilePath)) return false;

		try
		{
			string decryptedData = DecryptLicenseFile();
			var licenseData = JsonConvert.DeserializeObject<LicenseData>(decryptedData);

			// Validate the license data
			if (licenseData == null ||
				string.IsNullOrEmpty(licenseData.Key) ||
				licenseData.ExpiryTime == default)
			{
				File.Delete(LicenseFilePath);
				return false;
			}

			// Chỉ cập nhật thời gian nếu đây là lần đầu kích hoạt
			if (CurrentLicense == LicenseType.Invalid)
			{
				ActivationTime = licenseData.ActivationTime;
				ExpiryTime = licenseData.ExpiryTime;
			}

			// Kiểm tra nếu đã hết hạn
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

	private void ActivateLicense(LicenseType type, string licenseKey)
	{
		// Chỉ thiết lập thời gian nếu đây là lần đầu kích hoạt
		if (CurrentLicense == LicenseType.Invalid)
		{
			CurrentLicense = type;
			ActivationTime = DateTime.Now;

			// Tính toán thời gian hết hạn
			switch (type)
			{
				case LicenseType.Trial5Min:
					ExpiryTime = ActivationTime.AddMinutes(5);
					break;
				case LicenseType.Trial60Min:
					ExpiryTime = ActivationTime.AddHours(1);
					break;
				case LicenseType.Permanent:
					ExpiryTime = DateTime.MaxValue;
					break;
				default:
					ExpiryTime = DateTime.MinValue;
					break;
			}

			// Lưu thông tin license
			SaveLicenseData(licenseKey);
		}

		// Luôn khởi động timer cho bản quyền tạm thời
		if (type != LicenseType.Permanent)
		{
			_licenseCheckTimer.Start();
		}
	}

	public bool ValidateLicense(string licenseKey)
	{
		if (string.IsNullOrWhiteSpace(licenseKey)) return false;

		// Check predefined keys first
		if (_predefinedKeys.TryGetValue(licenseKey, out var predefinedType))
		{
			ActivateLicense(predefinedType, licenseKey);
			LicenseValidated?.Invoke();
			return true;
		}

		// Check generated keys
		if (ValidateGeneratedLicense(licenseKey))
		{
			LicenseValidated?.Invoke();
			return true;
		}

		return false;
	}

	private bool ValidateGeneratedLicense(string licenseKey)
	{
		var parts = licenseKey.Split('-');
		if (parts.Length != 3) return false;

		LicenseType type;
		switch (parts[0])
		{
			case "TEMP5": type = LicenseType.Trial5Min; break;
			case "TEMP60": type = LicenseType.Trial60Min; break;
			case "PERM": type = LicenseType.Permanent; break;
			default: type = LicenseType.Invalid; break;
		}

		if (type == LicenseType.Invalid) return false;
		if (!ValidateDatePart(parts[1])) return false;
		if (!VerifyLicenseSignature(parts[2], parts[0] + parts[1])) return false;

		ActivateLicense(type, licenseKey);
		return true;
	}

	

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
			throw new ApplicationException("License save error: " + ex.Message);
		}
	}

	public static string GenerateLicenseKey(LicenseType type)
	{
		string prefix;
		switch (type)
		{
			case LicenseType.Trial5Min: prefix = "TEMP5"; break;
			case LicenseType.Trial60Min: prefix = "TEMP60"; break;
			case LicenseType.Permanent: prefix = "PERM"; break;
			default: throw new ArgumentException("Invalid license type");
		}

		string datePart = DateTime.Now.ToString("yyyyMMdd");
		string data = prefix + datePart;
		string signature = GenerateSignature(data);

		return string.Format("{0}-{1}-{2}", prefix, datePart, signature);
	}

	private static string GenerateSignature(string input)
	{
		using (var sha256 = SHA256.Create())
		{
			byte[] inputBytes = Encoding.UTF8.GetBytes(input + Convert.ToBase64String(EncryptionKey));
			byte[] hashBytes = sha256.ComputeHash(inputBytes);
			return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
		}
	}

	private bool VerifyLicenseSignature(string signature, string data)
	{
		return signature == GenerateSignature(data);
	}

	private bool ValidateDatePart(string datePart)
	{
		return datePart.Length == 8 &&
			   DateTime.TryParseExact(datePart, "yyyyMMdd", null,
				   System.Globalization.DateTimeStyles.None, out _);
	}

	private string DecryptLicenseFile()
	{
		byte[] encrypted = File.ReadAllBytes(LicenseFilePath);
		return DecryptString(encrypted);
	}

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

	private void InitializeLicenseCheckTimer()
	{
		_licenseCheckTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(30)
		};
		_licenseCheckTimer.Tick += (s, e) => CheckLicenseValidity();
	}

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

	// Helper class for license data serialization
	private class LicenseData
	{
		public string Key { get; set; }
		public DateTime ActivationTime { get; set; }
		public DateTime ExpiryTime { get; set; }
		public LicenseType LicenseType { get; set; }
	}
}