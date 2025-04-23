using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace DemoLicenseApp
{
	public enum LicenseState { Activated, Trial, Expired, Invalid }

	public static class LicenseService
	{
		// Obfuscated constants
		private static readonly string AppName = Encoding.UTF8.GetString(new byte[] { 0x44, 0x65, 0x6D, 0x6F, 0x4C, 0x69, 0x63, 0x65, 0x6E, 0x73, 0x65, 0x41, 0x70, 0x70 });
		private static readonly string MetaFile = Encoding.UTF8.GetString(new byte[] { 0x2E, 0x73, 0x79, 0x73, 0x5F, 0x6D, 0x65, 0x74, 0x61 });
		private static readonly string LicenseKeysFile = Encoding.UTF8.GetString(new byte[] { 0x6C, 0x69, 0x63, 0x65, 0x6E, 0x73, 0x65, 0x5F, 0x6B, 0x65, 0x79, 0x73, 0x2E, 0x62, 0x69, 0x6E });

		// Dynamic flags
		private static string TrialFlag => GetFlag(1);
		private static string ActivatedFlag => GetFlag(2);

		// Dynamic key generation
		private static byte[] AesKey => DeriveKey(GetDynamicKeyPart1() + GetDynamicKeyPart2());
		private static byte[] LicenseKeysAesKey => DeriveKey(GetDynamicKeyPart2() + GetDynamicKeyPart3());

		// License keys cache
		private static Dictionary<string, TimeSpan> _validLicenseKeys;
		private static Dictionary<string, TimeSpan> ValidLicenseKeys
		{
			get
			{
				if (_validLicenseKeys == null)
				{
					_validLicenseKeys = LoadLicenseKeysFromFile();
				}
				return _validLicenseKeys;
			}
		}

		private static Dictionary<string, TimeSpan> GetDefaultLicenseKeysFromEnv()
		{
			// Load file .env từ thư mục gốc
			DotNetEnv.Env.Load();

			var keys = new Dictionary<string, TimeSpan>();

			try
			{
				var key1 = Environment.GetEnvironmentVariable("DEMO_KEY_1");
				var key2 = Environment.GetEnvironmentVariable("DEMO_KEY_2");
				var key3 = Environment.GetEnvironmentVariable("DEMO_KEY_3");

				if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("DEMO_DURATION_1"), out var duration1))
					keys[key1] = duration1;

				if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("DEMO_DURATION_2"), out var duration2))
					keys[key2] = duration2;

				if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("DEMO_DURATION_3"), out var duration3))
					keys[key3] = duration3;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error reading .env file: {ex.Message}");
			}

			return keys;
		}

		// Obfuscated paths
		private static readonly string DataFolder = GetHiddenDataFolder();
		public static readonly string FilePath = Path.Combine(DataFolder, MetaFile);
		private static readonly string LicenseKeysPath = Path.Combine(DataFolder, LicenseKeysFile);

		private static string GetFlag(int type) => type == 1 ? "TRIAL" : "ACTIVATED";

		private static string GetDynamicKeyPart1() =>
			Environment.MachineName.Substring(0, Math.Min(8, Environment.MachineName.Length));

		private static string GetDynamicKeyPart2() =>
			Environment.UserName.Substring(0, Math.Min(8, Environment.UserName.Length));

		private static string GetDynamicKeyPart3() =>
			Environment.OSVersion.VersionString.Substring(0, Math.Min(8, Environment.OSVersion.VersionString.Length));

		//private static string GetHiddenDataFolder()
		//{
		//	var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		//	var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(AppName));
		//	var hiddenFolderName = Convert.ToBase64String(hash.Take(10).ToArray());
		//	return Path.Combine(appDataPath, hiddenFolderName);
		//}
		private static string GetHiddenDataFolder()
		{
			// Lưu vào AppData/Local để dữ liệu tồn tại sau khi tắt ứng dụng
			var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var folderName = "Protect"; // Thay bằng tên ứng dụng của bạn
			return Path.Combine(appDataPath, folderName);
		}
		private static byte[] DeriveKey(string password, int keySize = 16)
		{
			using (var deriveBytes = new Rfc2898DeriveBytes(
				password,
				salt: Encoding.UTF8.GetBytes("FixedSaltForKeyDerivation"),
				iterations: 10000,
				HashAlgorithmName.SHA256))
			{
				return deriveBytes.GetBytes(keySize);
			}
		}

		private static Dictionary<string, TimeSpan> LoadLicenseKeysFromFile()
		{
			var licenseKeys = new Dictionary<string, TimeSpan>();

			try
			{
				if (!File.Exists(LicenseKeysPath))
				{
					var defaultKeys = new Dictionary<string, TimeSpan>
					{
						{ "DEMO1-7DAY-KEY12", TimeSpan.FromHours(1) },
						{ "DEMO2-30DAY-KEY1", TimeSpan.FromDays(30) },
						{ "DEMO3-365DAY-KEY", TimeSpan.FromDays(365) }
					};
					SaveLicenseKeysToFile(defaultKeys);
					return defaultKeys;
				}

				byte[] encryptedData = File.ReadAllBytes(LicenseKeysPath);
				string jsonContent = DecryptData(encryptedData, LicenseKeysAesKey);

				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var config = JsonSerializer.Deserialize<LicenseKeyConfig>(jsonContent, options);

				foreach (var kvp in config.LicenseKeys)
				{
					if (TimeSpan.TryParse(kvp.Value, out TimeSpan duration))
					{
						licenseKeys[kvp.Key] = duration;
					}
					else
					{
						Console.WriteLine($"Invalid duration format for key: {kvp.Key}");
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error loading license keys: {ex.Message}");
				licenseKeys = new Dictionary<string, TimeSpan>
				{
					{ "DEMO1-7DAY-KEY12", TimeSpan.FromHours(1) },
					{ "DEMO2-30DAY-KEY1", TimeSpan.FromDays(30) },
					{ "DEMO3-365DAY-KEY", TimeSpan.FromDays(365) }
				};
			}

			return licenseKeys;
		}

		private static void SaveLicenseKeysToFile(Dictionary<string, TimeSpan> licenseKeys)
		{
			try
			{
				EnsureDataFolder();

				var stringDict = new Dictionary<string, string>();
				foreach (var kvp in licenseKeys)
				{
					stringDict[kvp.Key] = kvp.Value.ToString();
				}

				var config = new LicenseKeyConfig { LicenseKeys = stringDict };
				string jsonContent = JsonSerializer.Serialize(config);

				byte[] encryptedData = EncryptData(jsonContent, LicenseKeysAesKey);
				File.WriteAllBytes(LicenseKeysPath, encryptedData);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving license keys: {ex.Message}");
				throw;
			}
		}

		private class LicenseKeyConfig
		{
			public Dictionary<string, string> LicenseKeys { get; set; }
		}

		private static byte[] EncryptData(string plainText, byte[] key)
		{
			using (var aes = Aes.Create())
			{
				aes.Key = key;
				aes.GenerateIV(); // Tạo IV ngẫu nhiên

				using (var memoryStream = new MemoryStream())
				{
					// Ghi IV vào đầu dữ liệu mã hóa
					memoryStream.Write(aes.IV, 0, aes.IV.Length);

					using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
					{
						byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
						cryptoStream.Write(plainBytes, 0, plainBytes.Length);
						cryptoStream.FlushFinalBlock();
						return memoryStream.ToArray();
					}
				}
			}
		}

		private static string DecryptData(byte[] cipherData, byte[] key)
		{
			try
			{
				using (var aes = Aes.Create())
				{
					aes.Key = key;

					using (var memoryStream = new MemoryStream(cipherData))
					{
						byte[] iv = new byte[16]; // IV sẽ được lưu trữ ở đầu dữ liệu
						memoryStream.Read(iv, 0, iv.Length); // Đọc IV từ dữ liệu

						aes.IV = iv;

						using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
						using (var streamReader = new StreamReader(cryptoStream))
						{
							return streamReader.ReadToEnd();
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Decryption error: {ex.Message}");
				return string.Empty;
			}
		}


		private static void EnsureDataFolder()
		{
			if (!Directory.Exists(DataFolder))
			{
				Directory.CreateDirectory(DataFolder);
			}
		}

		public static string GetMachineId()
		{
			return $"{GetBiosSerialNumber() ?? "NO_BIOS"}_{GetDiskId() ?? "NO_DISK"}";
		}

		private static string GetBiosSerialNumber()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
				{
					foreach (ManagementObject queryObj in searcher.Get())
					{
						try
						{
							return queryObj["SerialNumber"]?.ToString();
						}
						catch { /* Ignore errors */ }
					}
				}
			}
			catch { /* Ignore WMI errors */ }
			return null;
		}

		private static string GetProcessorId()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
				{
					foreach (ManagementObject queryObj in searcher.Get())
					{
						try
						{
							return queryObj["ProcessorId"]?.ToString();
						}
						catch { /* Ignore errors */ }
					}
				}
			}
			catch { /* Ignore WMI errors */ }
			return null;
		}

		private static string GetDiskId()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
				{
					foreach (ManagementObject queryObj in searcher.Get())
					{
						try
						{
							return queryObj["SerialNumber"]?.ToString()?.Trim();
						}
						catch { /* Ignore errors */ }
					}
				}
			}
			catch { /* Ignore WMI errors */ }
			return null;
		}

		private static string GetMacAddress()
		{
			try
			{
				var nics = NetworkInterface.GetAllNetworkInterfaces();
				var firstNic = nics.FirstOrDefault();
				return firstNic?.GetPhysicalAddress().ToString();
			}
			catch { return null; }
		}

		public static bool IsDebugging()
		{
			bool isDebuggerPresent = false;
			try
			{
				isDebuggerPresent = System.Diagnostics.Debugger.IsAttached;
				if (System.Diagnostics.Debugger.IsLogging())
					isDebuggerPresent = true;
			}
			catch { /* Ignore errors */ }
			return isDebuggerPresent;
		}

		private static bool DetectTampering()
		{
			try
			{
				if (!File.Exists(FilePath)) return false;

				var fileInfo = new FileInfo(FilePath);
				if (fileInfo.LastWriteTime > DateTime.Now.AddMinutes(5))
					return true;

				if (fileInfo.Length > 1024 || fileInfo.Length < 10)
					return true;

				return false;
			}
			catch { return true; }
		}

		private static bool VerifyLicenseIntegrity(string licenseData)
		{
			try
			{
				var parts = licenseData.Split('|');
				if (parts.Length != 5)
				{
					Console.WriteLine("Invalid license format");
					return false;
				}

				// Lấy các phần của license
				string activationDate = parts[0];
				string expirationDate = parts[1];
				string storedMachineId = parts[2].TrimEnd('.');
				string licenseType = parts[3];
				string storedHash = parts[4];

				// Lấy Machine ID hiện tại
				string currentMachineId = GetMachineId().TrimEnd('.');

				// Debug thông tin
				Console.WriteLine($"Current Machine ID: {currentMachineId}");
				Console.WriteLine($"Stored Machine ID: {storedMachineId}");

				// So sánh Machine ID (bỏ qua prefix nếu có)
				if (!storedMachineId.Contains(currentMachineId))
				{
					Console.WriteLine("Machine ID mismatch");
					return false;
				}

				// Kiểm tra ngày tháng
				if (!DateTime.TryParse(activationDate, out DateTime actDate) || 
					!DateTime.TryParse(expirationDate, out DateTime expDate))
				{
					Console.WriteLine("Invalid date format");
					return false;
				}

				// Tính hash đơn giản
				var calculatedHash = (actDate.GetHashCode() ^ expDate.GetHashCode() ^ storedMachineId.GetHashCode()).ToString();
				
				Console.WriteLine($"Hash comparison:");
				Console.WriteLine($"Stored: {storedHash}");
				Console.WriteLine($"Calculated: {calculatedHash}");
				
				// Nếu hash không khớp, thử tính lại với currentMachineId
				if (calculatedHash != storedHash)
				{
					calculatedHash = (actDate.GetHashCode() ^ expDate.GetHashCode() ^ currentMachineId.GetHashCode()).ToString();
					Console.WriteLine($"Recalculated with current Machine ID: {calculatedHash}");
				}
				
				return calculatedHash == storedHash;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error verifying license: {ex.Message}");
				return false;
			}
		}






		public static void SaveLicenseInfo(string machineId, DateTime activationDate, DateTime expirationDate, string licenseFlag)
		{
			string licenseInfo = $"{activationDate:o}|{expirationDate:o}|{machineId}|{licenseFlag}";
			licenseInfo += $"|{(activationDate.GetHashCode() ^ expirationDate.GetHashCode() ^ machineId.GetHashCode())}";

			try
			{
				string encryptedLicenseInfo = Encrypt(licenseInfo);
				File.WriteAllText(FilePath, encryptedLicenseInfo);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error saving license info: {ex.Message}");
				throw;
			}
		}

		public static string Encrypt(string plainText)
		{
			return Convert.ToBase64String(EncryptData(plainText, AesKey));
		}

		public static string Decrypt(string cipherText)
		{
			try
			{
				byte[] cipherData = Convert.FromBase64String(cipherText);
				return DecryptData(cipherData, AesKey);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Decryption error: {ex.Message}");
				return string.Empty;
			}
		}
		public static void InitializeFirstRun()
		{
			EnsureDataFolder();

			// Chỉ tạo license file nếu nó chưa tồn tại
			if (!File.Exists(FilePath))
			{
				// Tạo trial license mặc định (1 ngày)
				var activationDate = DateTime.UtcNow;
				var expirationDate = activationDate.AddDays(1); // Trial 1 ngày
				var machineId = GetMachineId();

				string licenseInfo = $"{activationDate:o}|{expirationDate:o}|{machineId}|{TrialFlag}";
				licenseInfo += $"|{activationDate.GetHashCode() ^ expirationDate.GetHashCode() ^ machineId.GetHashCode()}";

				File.WriteAllText(FilePath, Encrypt(licenseInfo));
				Console.WriteLine("Trial license created for first run.");
			}
		}
		//public static (LicenseState, string, DateTime?) CheckLicense()
		//{
		//	try
		//	{
		//		Console.WriteLine("DEBUG: Running license check");

		//		// UNCOMMENT dòng này nếu bạn muốn BYPASS license check
		//		//return (LicenseState.Activated, "DEBUG: Always activated", DateTime.UtcNow.AddYears(1));

		//		//Debug check -COMMENT dòng này nếu ứng dụng đang chạy trong debugger
		//		 if (IsDebugging())
		//			return (LicenseState.Invalid, "Debugger detected.", null);

		//		// File existence check
		//		if (!File.Exists(FilePath))
		//		{
		//			Console.WriteLine("License file not found at: " + FilePath);
		//			return (LicenseState.Invalid, "License file not found", null);
		//		}

		//		// Read and decrypt
		//		string licenseContent = File.ReadAllText(FilePath);
		//		string decrypted = Decrypt(licenseContent);

		//		if (string.IsNullOrWhiteSpace(decrypted))
		//		{
		//			Console.WriteLine("Decryption failed or empty content");
		//			return (LicenseState.Invalid, "Decryption failed", null);
		//		}

		//		// Verify integrity
		//		if (!VerifyLicenseIntegrity(decrypted))
		//		{
		//			Console.WriteLine("Integrity check failed");
		//			return (LicenseState.Invalid, "Tamper detected", null);
		//		}
		//		Console.WriteLine("Integrity check passed");

		//		// Parse license
		//		var parts = decrypted.Split('|');
		//		if (parts.Length < 4)
		//		{
		//			Console.WriteLine($"Invalid format. Expected 4 parts, got {parts.Length}");
		//			return (LicenseState.Invalid, "Invalid format", null);
		//		}

		//		// Parse dates
		//		if (!DateTime.TryParse(parts[0], out DateTime activationDate) ||
		//			!DateTime.TryParse(parts[1], out DateTime expirationDate))
		//		{
		//			Console.WriteLine($"Date parse failed. Activation: {parts[0]}, Expiration: {parts[1]}");
		//			return (LicenseState.Invalid, "Invalid dates", null);
		//		}
		//		Console.WriteLine("Activation date: " + activationDate);
		//		Console.WriteLine("Expiration date: " + expirationDate);

		//		// Machine check - Tạm thời comment để bypass
		//		//string currentId = GetMachineId();
		//		//if (parts[2] != currentId)
		//		//{
		//		//	Console.WriteLine($"Machine ID mismatch. License: {parts[2]}, Current: {currentId}");
		//		//	return (LicenseState.Invalid, "Machine mismatch", null);
		//		//}

		//		// Check expiration
		//		if (DateTime.UtcNow > expirationDate)
		//			return (LicenseState.Expired, "License expired", expirationDate);
		//		Console.WriteLine("License type: " + parts[3]);

		//		// Return appropriate state
		//		if (parts[3] == ActivatedFlag)
		//		{
		//			return (LicenseState.Activated, "Activated", expirationDate);
		//		}
		//		else if (parts[3] == TrialFlag)
		//		{
		//			return (LicenseState.Trial, "Trial", expirationDate);
		//		}
		//		else
		//		{
		//			return (LicenseState.Invalid, "Unknown license type", null);
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"License check error: {ex}");

		//		return (LicenseState.Invalid, $"Check error: {ex.Message}", null);
		//	}
		//}
		public static (LicenseState, string, DateTime?) CheckLicense()
		{
			try
			{
				Console.WriteLine("DEBUG: Running license check");

				// UNCOMMENT dòng này nếu bạn muốn BYPASS license check
				// return (LicenseState.Activated, "DEBUG: Always activated", DateTime.UtcNow.AddYears(1));

				// Debug check - COMMENT dòng này nếu ứng dụng đang chạy trong debugger
				//if (IsDebugging())
				//{
				//	Console.WriteLine("DEBUG: Debugger detected!");
				//	return (LicenseState.Invalid, "Debugger detected.", null);
				//}

				// File existence check
				Console.WriteLine("Kiểm tra file license tại: " + FilePath);
				if (!File.Exists(FilePath))
				{
					Console.WriteLine("License file not found.");
					return (LicenseState.Invalid, "License file not found", null);
				}

				var info = new FileInfo(FilePath);
				Console.WriteLine("File tồn tại: True");
				Console.WriteLine("Kích thước file: " + info.Length + " bytes");

				// Read and decrypt
				string licenseContent = File.ReadAllText(FilePath);
				Console.WriteLine("Đã đọc được nội dung, độ dài: " + licenseContent.Length);

				string decrypted = Decrypt(licenseContent);
				Console.WriteLine("DEBUG: Nội dung đã giải mã = " + decrypted);

				if (string.IsNullOrWhiteSpace(decrypted))
				{
					Console.WriteLine("Decryption failed hoặc nội dung rỗng");
					return (LicenseState.Invalid, "Decryption failed", null);
				}

				// Verify integrity
				if (!VerifyLicenseIntegrity(decrypted))
				{
					Console.WriteLine("Integrity check failed");
					return (LicenseState.Invalid, "Tamper detected", null);
				}
				Console.WriteLine("Integrity check passed");

				// Parse license
				var parts = decrypted.Split('|');
				Console.WriteLine("DEBUG: Số lượng phần tử sau split = " + parts.Length);
				if (parts.Length < 4)
				{
					Console.WriteLine("Invalid format. Expected 4 parts, got " + parts.Length);
					return (LicenseState.Invalid, "Invalid format", null);
				}

				// Parse dates
				if (!DateTime.TryParse(parts[0], out DateTime activationDate) ||
					!DateTime.TryParse(parts[1], out DateTime expirationDate))
				{
					Console.WriteLine($"Date parse failed. Activation: {parts[0]}, Expiration: {parts[1]}");
					return (LicenseState.Invalid, "Invalid dates", null);
				}
				Console.WriteLine("Activation date: " + activationDate);
				Console.WriteLine("Expiration date: " + expirationDate);

				// Machine check
				string currentId = GetMachineId();
				Console.WriteLine("Machine ID: " + currentId);
				Console.WriteLine("License Machine ID: " + parts[2]);

				if (parts[2] != currentId)
				{
					Console.WriteLine("Machine ID mismatch.");
					return (LicenseState.Invalid, "Machine mismatch", null);
				}

				// Check expiration
				if (DateTime.UtcNow > expirationDate)
				{
					Console.WriteLine("License expired.");
					return (LicenseState.Expired, "License expired", expirationDate);
				}

				Console.WriteLine("License type: " + parts[3]);

				// Return appropriate state
				if (parts[3] == ActivatedFlag)
				{
					Console.WriteLine("DEBUG: License hợp lệ - ACTIVATED");
					return (LicenseState.Activated, "Activated", expirationDate);
				}
				else if (parts[3] == TrialFlag)
				{
					Console.WriteLine("DEBUG: License hợp lệ - TRIAL");
					return (LicenseState.Trial, "Trial", expirationDate);
				}
				else
				{
					Console.WriteLine("Unknown license type.");
					return (LicenseState.Invalid, "Unknown license type", null);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"License check error: {ex}");
				return (LicenseState.Invalid, $"Check error: {ex.Message}", null);
			}
		}

		//public static (LicenseState, string, DateTime?) CheckLicense()
		//{
		//	Console.WriteLine("DEBUG: Bypassing license check temporarily");
		//	return (LicenseState.Activated, "DEBUG: Always activated", DateTime.UtcNow.AddYears(1));
		//	try
		//	{
		//		// Debug check
		//		if (IsDebugging())
		//			return (LicenseState.Invalid, "Debugger detected.", null);

		//		// File existence check
		//		if (!File.Exists(FilePath))
		//		{
		//			Console.WriteLine("License file not found at: " + FilePath);
		//			return (LicenseState.Invalid, "License file not found", null);
		//		}

		//		// Read and decrypt
		//		string licenseContent = File.ReadAllText(FilePath);
		//		string decrypted = Decrypt(licenseContent);

		//		if (string.IsNullOrWhiteSpace(decrypted))
		//		{
		//			Console.WriteLine("Decryption failed or empty content");
		//			return (LicenseState.Invalid, "Decryption failed", null);
		//		}

		//		// Verify integrity
		//		if (!VerifyLicenseIntegrity(decrypted))
		//		{
		//			Console.WriteLine("Integrity check failed");
		//			return (LicenseState.Invalid, "Tamper detected", null);
		//		}

		//		// Parse license
		//		var parts = decrypted.Split('|');
		//		if (parts.Length < 4)
		//		{
		//			Console.WriteLine($"Invalid format. Expected 4 parts, got {parts.Length}");
		//			return (LicenseState.Invalid, "Invalid format", null);
		//		}

		//		// Parse dates
		//		if (!DateTime.TryParse(parts[0], out DateTime activationDate) ||
		//			!DateTime.TryParse(parts[1], out DateTime expirationDate))
		//		{
		//			Console.WriteLine($"Date parse failed. Activation: {parts[0]}, Expiration: {parts[1]}");
		//			return (LicenseState.Invalid, "Invalid dates", null);
		//		}

		//		// Machine check
		//		string currentId = GetMachineId();
		//		if (parts[2] != currentId)
		//		{
		//			Console.WriteLine($"Machine ID mismatch. License: {parts[2]}, Current: {currentId}");
		//			return (LicenseState.Invalid, "Machine mismatch", null);
		//		}

		//		// Check expiration
		//		if (DateTime.UtcNow > expirationDate)
		//			return (LicenseState.Expired, "License expired", expirationDate);

		//		// Return appropriate state
		//		if (parts[3] == ActivatedFlag)
		//		{
		//			return (LicenseState.Activated, "Activated", expirationDate);
		//		}
		//		else if (parts[3] == TrialFlag)
		//		{
		//			return (LicenseState.Trial, "Trial", expirationDate);
		//		}
		//		else
		//		{
		//			return (LicenseState.Invalid, "Unknown license type", null);
		//		}

		//	}
		//	catch (Exception ex)
		//	{
		//		Console.WriteLine($"License check error: {ex}");
		//		return (LicenseState.Invalid, $"Check error: {ex.Message}", null);
		//	}
		//}

		public static bool ValidateKey(string activationKey)
		{
			return ValidLicenseKeys.ContainsKey(activationKey);
		}
		public static bool ActivateLicense(string activationKey)
		{
			if (!ValidLicenseKeys.ContainsKey(activationKey))
				return false;

			try
			{
				var duration = ValidLicenseKeys[activationKey];
				var activationDate = DateTime.UtcNow;
				var expirationDate = activationDate.Add(duration);
				var machineId = GetMachineId().TrimEnd('.'); // Loại bỏ dấu chấm thừa

				// Tạo chuỗi dữ liệu để hash
				var dataToHash = $"{activationDate:o}|{expirationDate:o}|{machineId}|{ActivatedFlag}";
				
				// Tính hash đơn giản
				var hashValue = (activationDate.GetHashCode() ^ expirationDate.GetHashCode() ^ machineId.GetHashCode()).ToString();

				// Tạo license info với hash mới
				string licenseInfo = $"{activationDate:o}|{expirationDate:o}|{machineId}|{ActivatedFlag}|{hashValue}";

				// Debug info
				Console.WriteLine($"Creating new license:");
				Console.WriteLine($"Machine ID: {machineId}");
				Console.WriteLine($"Activation Date: {activationDate:o}");
				Console.WriteLine($"Expiration Date: {expirationDate:o}");
				Console.WriteLine($"Hash: {hashValue}");

				// Lưu license đã mã hóa
				File.WriteAllText(FilePath, Encrypt(licenseInfo));
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error activating license: {ex.Message}");
				return false;
			}
		}
		//public static bool ActivateLicense(string activationKey)
		//{
		//	if (!ValidateKey(activationKey))
		//		return false;

		//	var duration = ValidLicenseKeys[activationKey];
		//	var activationDate = DateTime.UtcNow;
		//	var expirationDate = activationDate.Add(duration);

		//	SaveLicenseInfo(GetMachineId(), activationDate, expirationDate, ActivatedFlag);
		//	return true;
		//}

		public static bool IsLicenseCurrentlyActive()
		{
			try
			{
				var (state, _, expirationDate) = CheckLicense();
				return state == LicenseState.Activated && 
					   expirationDate.HasValue && 
					   expirationDate.Value > DateTime.UtcNow;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error checking license status: {ex.Message}");
				return false;
			}
		}

		public static bool IsActivatedAndValid()
		{
			try
			{
				if (!File.Exists(FilePath))
				{
					return false;
				}

				string licenseContent = File.ReadAllText(FilePath);
				string decrypted = Decrypt(licenseContent);

				if (string.IsNullOrWhiteSpace(decrypted))
				{
					return false;
				}

				var parts = decrypted.Split('|');
				if (parts.Length < 4)
				{
					return false;
				}

				// Kiểm tra ngày hết hạn
				if (!DateTime.TryParse(parts[1], out DateTime expirationDate))
				{
					return false;
				}

				// Kiểm tra trạng thái và thời hạn
				return parts[3] == ActivatedFlag && expirationDate > DateTime.UtcNow;
			}
			catch
			{
				return false;
			}
		}
	}
}
