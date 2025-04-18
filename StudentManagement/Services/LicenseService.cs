
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

		private static string GetHiddenDataFolder()
		{
			var tempPath = Path.GetTempPath();
			var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(AppName));
			var hiddenFolderName = Convert.ToBase64String(hash.Take(10).ToArray());
			return Path.Combine(tempPath, hiddenFolderName);
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
						{ "DEMO1-7DAY-KEY", TimeSpan.FromMinutes(5) },
						{ "DEMO2-30DAY-KEY", TimeSpan.FromDays(30) },
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
					{ "DEMO1-7DAY-KEY", TimeSpan.FromMinutes(5) },
					{ "DEMO2-30DAY-KEY", TimeSpan.FromDays(30) },
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
				aes.IV = new byte[16];

				using (var memoryStream = new MemoryStream())
				{
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
					aes.IV = new byte[16];

					using (var memoryStream = new MemoryStream(cipherData))
					using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
					using (var streamReader = new StreamReader(cryptoStream))
					{
						return streamReader.ReadToEnd();
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
			try
			{
				var components = new List<string>
				{
					GetBiosSerialNumber() ?? "NO_BIOS_ID",
					GetProcessorId() ?? "NO_CPU_ID",
					GetDiskId() ?? "NO_DISK_ID",
					GetMacAddress() ?? "NO_MAC_ADDR"
				};

				using (var sha = SHA256.Create())
				{
					var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", components)));
					return BitConverter.ToString(hash).Replace("-", "");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in retrieving Machine ID: {ex.Message}");
				return "UNKNOWN_ID_" + Guid.NewGuid().ToString("N");
			}
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
				if (parts.Length != 5) return false;

				var expectedHash = parts[0].GetHashCode() ^ parts[1].GetHashCode() ^ parts[2].GetHashCode();
				if (expectedHash != int.Parse(parts[4]))
					return false;

				return true;
			}
			catch { return false; }
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
			if (!File.Exists(FilePath))
			{
				var info = $"{DateTime.UtcNow:o}|{DateTime.UtcNow.AddHours(1):o}|{GetMachineId()}|{TrialFlag}";
				info += $"|{(DateTime.UtcNow.GetHashCode() ^ DateTime.UtcNow.AddHours(1).GetHashCode() ^ GetMachineId().GetHashCode())}";
				File.WriteAllText(FilePath, Encrypt(info));
			}
		}

		public static (LicenseState, string) CheckLicense()
		{
			if (IsDebugging())
				return (LicenseState.Invalid, "Debugger detected. License check aborted.");

			if (DetectTampering())
				return (LicenseState.Invalid, "License file has been tampered with.");

			EnsureDataFolder();

			if (!File.Exists(FilePath))
				return (LicenseState.Invalid, "License file not found. Please activate.");

			string decrypted = Decrypt(File.ReadAllText(FilePath));
			if (string.IsNullOrEmpty(decrypted))
				return (LicenseState.Invalid, "Invalid license data.");

			if (!VerifyLicenseIntegrity(decrypted))
				return (LicenseState.Invalid, "License integrity check failed.");

			string[] parts = decrypted.Split('|');
			if (parts.Length < 4)
				return (LicenseState.Invalid, "Invalid license format.");

			if (!DateTime.TryParse(parts[0], null, DateTimeStyles.RoundtripKind, out var activationDate))
				return (LicenseState.Invalid, "Invalid activation date format.");

			if (!DateTime.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out var expirationDate))
				return (LicenseState.Invalid, "Invalid expiration date format.");

			string currentMachineId = GetMachineId();
			if (parts[2] != currentMachineId)
				return (LicenseState.Invalid, "License not valid for this machine.");

			string flag = parts[3];
			if (DateTime.UtcNow > expirationDate)
				return (LicenseState.Expired, flag == ActivatedFlag ? "License has expired." : "Trial period has expired.");

			if (flag == ActivatedFlag)
			{
				var remaining = expirationDate - DateTime.UtcNow;
				return (LicenseState.Activated, $"License active. Expires in {remaining.Days} days.");
			}
			else if (flag == TrialFlag)
			{
				var remaining = expirationDate - DateTime.UtcNow;
				return (LicenseState.Trial, $"Trial period. {remaining.TotalHours:F1} hours remaining.");
			}

			return (LicenseState.Invalid, "Unknown license status.");
		}

		public static bool ValidateKey(string activationKey)
		{
			return ValidLicenseKeys.ContainsKey(activationKey);
		}

		public static bool ActivateLicense(string activationKey)
		{
			if (!ValidateKey(activationKey))
				return false;

			var duration = ValidLicenseKeys[activationKey];
			var activationDate = DateTime.UtcNow;
			var expirationDate = activationDate.Add(duration);

			SaveLicenseInfo(GetMachineId(), activationDate, expirationDate, ActivatedFlag);
			return true;
		}
	}
}

//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Management;
//using System.Net.NetworkInformation;
//using System.Security.Cryptography;
//using System.Text;
//using System.Text.Json;

//namespace DemoLicenseApp
//{
//	public enum LicenseState { Activated, Trial, Expired, Invalid }

//	public static class LicenseService
//	{
//		// Obfuscated constants
//		private static readonly string AppName = Encoding.UTF8.GetString(new byte[] { 0x44, 0x65, 0x6D, 0x6F, 0x4C, 0x69, 0x63, 0x65, 0x6E, 0x73, 0x65, 0x41, 0x70, 0x70 });
//		private static readonly string MetaFile = Encoding.UTF8.GetString(new byte[] { 0x2E, 0x73, 0x79, 0x73, 0x5F, 0x6D, 0x65, 0x74, 0x61 });
//		private static readonly string LicenseKeysFile = Encoding.UTF8.GetString(new byte[] { 0x6C, 0x69, 0x63, 0x65, 0x6E, 0x73, 0x65, 0x5F, 0x6B, 0x65, 0x79, 0x73, 0x2E, 0x62, 0x69, 0x6E });

//		// Dynamic flags
//		private static string TrialFlag => GetFlag(1);
//		private static string ActivatedFlag => GetFlag(2);

//		// Dynamic key generation
//		private static byte[] AesKey => DeriveKey(GetDynamicKeyPart1() + GetDynamicKeyPart2());
//		private static byte[] LicenseKeysAesKey => DeriveKey(GetDynamicKeyPart2() + GetDynamicKeyPart3());

//		// License keys cache
//		private static Dictionary<string, TimeSpan> _validLicenseKeys;
//		//private static Dictionary<string, TimeSpan> ValidLicenseKeys
//		//{
//		//	get
//		//	{
//		//		if (_validLicenseKeys == null)
//		//		{
//		//			_validLicenseKeys = LoadLicenseKeysFromFile();
//		//		}
//		//		return _validLicenseKeys;
//		//	}
//		//}
//		private static Dictionary<string, TimeSpan> GetDefaultLicenseKeysFromEnv()
//		{
//			// Load file .env từ thư mục gốc
//			DotNetEnv.Env.Load();

//			var keys = new Dictionary<string, TimeSpan>();

//			try
//			{
//				var key1 = Environment.GetEnvironmentVariable("DEMO_KEY_1");
//				var key2 = Environment.GetEnvironmentVariable("DEMO_KEY_2");
//				var key3 = Environment.GetEnvironmentVariable("DEMO_KEY_3");

//				if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("DEMO_DURATION_1"), out var duration1))
//					keys[key1] = duration1;

//				if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("DEMO_DURATION_2"), out var duration2))
//					keys[key2] = duration2;

//				if (TimeSpan.TryParse(Environment.GetEnvironmentVariable("DEMO_DURATION_3"), out var duration3))
//					keys[key3] = duration3;
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Error reading .env file: {ex.Message}");
//			}

//			return keys;
//		}

//		// Obfuscated paths
//		private static readonly string DataFolder = GetHiddenDataFolder();
//		public static readonly string FilePath = Path.Combine(DataFolder, MetaFile);
//		private static readonly string LicenseKeysPath = Path.Combine(DataFolder, LicenseKeysFile);

//		private static string GetFlag(int type) => type == 1 ? "TRIAL" : "ACTIVATED";

//		private static string GetDynamicKeyPart1() =>
//			Environment.MachineName.Substring(0, Math.Min(8, Environment.MachineName.Length));

//		private static string GetDynamicKeyPart2() =>
//			Environment.UserName.Substring(0, Math.Min(8, Environment.UserName.Length));

//		private static string GetDynamicKeyPart3() =>
//			Environment.OSVersion.VersionString.Substring(0, Math.Min(8, Environment.OSVersion.VersionString.Length));

//		private static string GetHiddenDataFolder()
//		{
//			var tempPath = Path.GetTempPath();
//			var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(AppName));
//			var hiddenFolderName = Convert.ToBase64String(hash.Take(10).ToArray());
//			return Path.Combine(tempPath, hiddenFolderName);
//		}



//		private static byte[] DeriveKey(string password, int keySize = 16)
//		{
//			using (var deriveBytes = new Rfc2898DeriveBytes(
//				password,
//				salt: Encoding.UTF8.GetBytes("FixedSaltForKeyDerivation"),
//				iterations: 10000,
//				HashAlgorithmName.SHA256))
//			{
//				return deriveBytes.GetBytes(keySize);
//			}
//		}
//		private static Dictionary<string, TimeSpan> LoadLicenseKeysFromFile()
//		{
//			try
//			{
//				if (!File.Exists(LicenseKeysPath))
//				{
//					var defaultKeys = ValidLicenseKeys.GetAllKeys(); // Sử dụng public method để lấy keys
//					SaveLicenseKeysToFile(defaultKeys);
//					return defaultKeys;
//				}

//				byte[] encryptedData = File.ReadAllBytes(LicenseKeysPath);
//				string jsonContent = DecryptData(encryptedData, LicenseKeysAesKey);

//				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//				var config = JsonSerializer.Deserialize<LicenseKeyConfig>(jsonContent, options);

//				var licenseKeys = new Dictionary<string, TimeSpan>();
//				foreach (var kvp in config.LicenseKeys)
//				{
//					if (TimeSpan.TryParse(kvp.Value, out TimeSpan duration))
//					{
//						licenseKeys[kvp.Key] = duration;
//					}
//					else
//					{
//						Console.WriteLine($"Invalid duration format for key: {kvp.Key}");
//					}
//				}

//				return licenseKeys;
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Error loading license keys: {ex.Message}");
//				return ValidLicenseKeys.GetAllKeys(); // Sử dụng public method để lấy keys
//			}
//		}
//		private static class ValidLicenseKeys
//		{
//			private static readonly Dictionary<string, TimeSpan> _licenseKeys;

//			static ValidLicenseKeys()
//			{
//				_licenseKeys = LoadKeysFromEnv();
//			}

//			private static Dictionary<string, TimeSpan> LoadKeysFromEnv()
//			{
//				var keys = new Dictionary<string, TimeSpan>();

//				try
//				{
//					// Đảm bảo load file .env đúng cách
//					string envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
//					if (File.Exists(envPath))
//					{
//						DotNetEnv.Env.Load(envPath);
//					}
//					else
//					{
//						DotNetEnv.Env.Load(); // Thử load từ biến môi trường hệ thống
//					}

//					// Đọc các key - sử dụng tên biến đúng như trong file .env
//					for (int i = 1; i <= 3; i++)
//					{
//						var key = Environment.GetEnvironmentVariable($"LICENSE_KEY_{i}");
//						var durationStr = Environment.GetEnvironmentVariable($"LICENSE_DURATION_{i}");

//						if (!string.IsNullOrEmpty(key)
//							&& !keys.ContainsKey(key)
//							&& TimeSpan.TryParse(durationStr, out var duration))
//						{
//							keys[key] = duration;
//							Console.WriteLine($"Loaded key: {key} with duration: {duration}");
//						}
//					}
//				}
//				catch (Exception ex)
//				{
//					Console.WriteLine($"Error loading license keys from .env: {ex.Message}");

//					// Fallback keys
//					keys = new Dictionary<string, TimeSpan>
//			{
//				{ "DEMO1-7DAY-KEY", TimeSpan.FromMinutes(5) },
//				{ "DEMO2-30DAY-KEY", TimeSpan.FromDays(30) },
//				{ "DEMO3-365DAY-KEY", TimeSpan.FromDays(365) }
//			};
//				}

//				return keys;
//			}

//			public static bool ContainsKey(string key)
//			{
//				bool exists = _licenseKeys.ContainsKey(key);
//				if (!exists)
//				{
//					Console.WriteLine($"Key not found: {key}. Available keys: {string.Join(", ", _licenseKeys.Keys)}");
//				}
//				return exists;
//			}
//			public static TimeSpan GetDuration(string key) => _licenseKeys[key];
//			public static Dictionary<string, TimeSpan> GetAllKeys() => new Dictionary<string, TimeSpan>(_licenseKeys);
//		}
//		//private static Dictionary<string, TimeSpan> LoadLicenseKeysFromFile()
//		//{
//		//	var licenseKeys = new Dictionary<string, TimeSpan>();

//		//	try
//		//	{
//		//		if (!File.Exists(LicenseKeysPath))
//		//		{
//		//			var defaultKeys = new Dictionary<string, TimeSpan>
//		//			{
//		//				{ "DEMO1-7DAY-KEY", TimeSpan.FromMinutes(5) },
//		//				{ "DEMO2-30DAY-KEY", TimeSpan.FromDays(30) },
//		//				{ "DEMO3-365DAY-KEY", TimeSpan.FromDays(365) }
//		//			};
//		//			SaveLicenseKeysToFile(defaultKeys);
//		//			return defaultKeys;
//		//		}

//		//		byte[] encryptedData = File.ReadAllBytes(LicenseKeysPath);
//		//		string jsonContent = DecryptData(encryptedData, LicenseKeysAesKey);

//		//		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
//		//		var config = JsonSerializer.Deserialize<LicenseKeyConfig>(jsonContent, options);

//		//		foreach (var kvp in config.LicenseKeys)
//		//		{
//		//			if (TimeSpan.TryParse(kvp.Value, out TimeSpan duration))
//		//			{
//		//				licenseKeys[kvp.Key] = duration;
//		//			}
//		//			else
//		//			{
//		//				Console.WriteLine($"Invalid duration format for key: {kvp.Key}");
//		//			}
//		//		}
//		//	}
//		//	catch (Exception ex)
//		//	{
//		//		Console.WriteLine($"Error loading license keys: {ex.Message}");
//		//		licenseKeys = new Dictionary<string, TimeSpan>
//		//		{
//		//			{ "DEMO1-7DAY-KEY", TimeSpan.FromMinutes(5) },
//		//			{ "DEMO2-30DAY-KEY", TimeSpan.FromDays(30) },
//		//			{ "DEMO3-365DAY-KEY", TimeSpan.FromDays(365) }
//		//		};
//		//	}

//		//	return licenseKeys;
//		//}

//		private static void SaveLicenseKeysToFile(Dictionary<string, TimeSpan> licenseKeys)
//		{
//			try
//			{
//				EnsureDataFolder();

//				var stringDict = new Dictionary<string, string>();
//				foreach (var kvp in licenseKeys)
//				{
//					stringDict[kvp.Key] = kvp.Value.ToString();
//				}

//				var config = new LicenseKeyConfig { LicenseKeys = stringDict };
//				string jsonContent = JsonSerializer.Serialize(config);

//				byte[] encryptedData = EncryptData(jsonContent, LicenseKeysAesKey);
//				File.WriteAllBytes(LicenseKeysPath, encryptedData);
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Error saving license keys: {ex.Message}");
//				throw;
//			}
//		}

//		private class LicenseKeyConfig
//		{
//			public Dictionary<string, string> LicenseKeys { get; set; }
//		}

//		private static byte[] EncryptData(string plainText, byte[] key)
//		{
//			using (var aes = Aes.Create())
//			{
//				aes.Key = key;
//				aes.IV = new byte[16];

//				using (var memoryStream = new MemoryStream())
//				{
//					using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
//					{
//						byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
//						cryptoStream.Write(plainBytes, 0, plainBytes.Length);
//						cryptoStream.FlushFinalBlock();
//						return memoryStream.ToArray();
//					}
//				}
//			}
//		}

//		private static string DecryptData(byte[] cipherData, byte[] key)
//		{
//			try
//			{
//				using (var aes = Aes.Create())
//				{
//					aes.Key = key;
//					aes.IV = new byte[16];

//					using (var memoryStream = new MemoryStream(cipherData))
//					using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
//					using (var streamReader = new StreamReader(cryptoStream))
//					{
//						return streamReader.ReadToEnd();
//					}
//				}
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Decryption error: {ex.Message}");
//				return string.Empty;
//			}
//		}

//		private static void EnsureDataFolder()
//		{
//			if (!Directory.Exists(DataFolder))
//			{
//				Directory.CreateDirectory(DataFolder);
//			}
//		}

//		public static string GetMachineId()
//		{
//			try
//			{
//				var components = new List<string>
//				{
//					GetBiosSerialNumber() ?? "NO_BIOS_ID",
//					GetProcessorId() ?? "NO_CPU_ID",
//					GetDiskId() ?? "NO_DISK_ID",
//					GetMacAddress() ?? "NO_MAC_ADDR"
//				};

//				using (var sha = SHA256.Create())
//				{
//					var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|", components)));
//					return BitConverter.ToString(hash).Replace("-", "");
//				}
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Error in retrieving Machine ID: {ex.Message}");
//				return "UNKNOWN_ID_" + Guid.NewGuid().ToString("N");
//			}
//		}

//		private static string GetBiosSerialNumber()
//		{
//			try
//			{
//				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
//				{
//					foreach (ManagementObject queryObj in searcher.Get())
//					{
//						try
//						{
//							return queryObj["SerialNumber"]?.ToString();
//						}
//						catch { /* Ignore errors */ }
//					}
//				}
//			}
//			catch { /* Ignore WMI errors */ }
//			return null;
//		}

//		private static string GetProcessorId()
//		{
//			try
//			{
//				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
//				{
//					foreach (ManagementObject queryObj in searcher.Get())
//					{
//						try
//						{
//							return queryObj["ProcessorId"]?.ToString();
//						}
//						catch { /* Ignore errors */ }
//					}
//				}
//			}
//			catch { /* Ignore WMI errors */ }
//			return null;
//		}

//		private static string GetDiskId()
//		{
//			try
//			{
//				using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
//				{
//					foreach (ManagementObject queryObj in searcher.Get())
//					{
//						try
//						{
//							return queryObj["SerialNumber"]?.ToString()?.Trim();
//						}
//						catch { /* Ignore errors */ }
//					}
//				}
//			}
//			catch { /* Ignore WMI errors */ }
//			return null;
//		}

//		private static string GetMacAddress()
//		{
//			try
//			{
//				var nics = NetworkInterface.GetAllNetworkInterfaces();
//				var firstNic = nics.FirstOrDefault();
//				return firstNic?.GetPhysicalAddress().ToString();
//			}
//			catch { return null; }
//		}

//		public static bool IsDebugging()
//		{
//			bool isDebuggerPresent = false;
//			try
//			{
//				isDebuggerPresent = System.Diagnostics.Debugger.IsAttached;
//				if (System.Diagnostics.Debugger.IsLogging())
//					isDebuggerPresent = true;
//			}
//			catch { /* Ignore errors */ }
//			return isDebuggerPresent;
//		}

//		private static bool DetectTampering()
//		{
//			try
//			{
//				if (!File.Exists(FilePath)) return false;

//				var fileInfo = new FileInfo(FilePath);
//				if (fileInfo.LastWriteTime > DateTime.Now.AddMinutes(5))
//					return true;

//				if (fileInfo.Length > 1024 || fileInfo.Length < 10)
//					return true;

//				return false;
//			}
//			catch { return true; }
//		}

//		private static bool VerifyLicenseIntegrity(string licenseData)
//		{
//			try
//			{
//				var parts = licenseData.Split('|');
//				if (parts.Length != 5) return false;

//				var expectedHash = parts[0].GetHashCode() ^ parts[1].GetHashCode() ^ parts[2].GetHashCode();
//				if (expectedHash != int.Parse(parts[4]))
//					return false;

//				return true;
//			}
//			catch { return false; }
//		}

//		public static void SaveLicenseInfo(string machineId, DateTime activationDate, DateTime expirationDate, string licenseFlag)
//		{
//			string licenseInfo = $"{activationDate:o}|{expirationDate:o}|{machineId}|{licenseFlag}";
//			licenseInfo += $"|{(activationDate.GetHashCode() ^ expirationDate.GetHashCode() ^ machineId.GetHashCode())}";

//			try
//			{
//				string encryptedLicenseInfo = Encrypt(licenseInfo);
//				File.WriteAllText(FilePath, encryptedLicenseInfo);
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Error saving license info: {ex.Message}");
//				throw;
//			}
//		}

//		public static string Encrypt(string plainText)
//		{
//			return Convert.ToBase64String(EncryptData(plainText, AesKey));
//		}

//		public static string Decrypt(string cipherText)
//		{
//			try
//			{
//				byte[] cipherData = Convert.FromBase64String(cipherText);
//				return DecryptData(cipherData, AesKey);
//			}
//			catch (Exception ex)
//			{
//				Console.WriteLine($"Decryption error: {ex.Message}");
//				return string.Empty;
//			}
//		}

//		public static void InitializeFirstRun()
//		{
//			EnsureDataFolder();
//			if (!File.Exists(FilePath))
//			{
//				var info = $"{DateTime.UtcNow:o}|{DateTime.UtcNow.AddHours(1):o}|{GetMachineId()}|{TrialFlag}";
//				info += $"|{(DateTime.UtcNow.GetHashCode() ^ DateTime.UtcNow.AddHours(1).GetHashCode() ^ GetMachineId().GetHashCode())}";
//				File.WriteAllText(FilePath, Encrypt(info));
//			}
//		}

//		public static (LicenseState, string) CheckLicense()
//		{
//			if (IsDebugging())
//				return (LicenseState.Invalid, "Debugger detected. License check aborted.");

//			if (DetectTampering())
//				return (LicenseState.Invalid, "License file has been tampered with.");

//			EnsureDataFolder();

//			if (!File.Exists(FilePath))
//				return (LicenseState.Invalid, "License file not found. Please activate.");

//			string decrypted = Decrypt(File.ReadAllText(FilePath));
//			if (string.IsNullOrEmpty(decrypted))
//				return (LicenseState.Invalid, "Invalid license data.");

//			if (!VerifyLicenseIntegrity(decrypted))
//				return (LicenseState.Invalid, "License integrity check failed.");

//			string[] parts = decrypted.Split('|');
//			if (parts.Length < 4)
//				return (LicenseState.Invalid, "Invalid license format.");

//			if (!DateTime.TryParse(parts[0], null, DateTimeStyles.RoundtripKind, out var activationDate))
//				return (LicenseState.Invalid, "Invalid activation date format.");

//			if (!DateTime.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out var expirationDate))
//				return (LicenseState.Invalid, "Invalid expiration date format.");

//			string currentMachineId = GetMachineId();
//			if (parts[2] != currentMachineId)
//				return (LicenseState.Invalid, "License not valid for this machine.");

//			string flag = parts[3];
//			if (DateTime.UtcNow > expirationDate)
//				return (LicenseState.Expired, flag == ActivatedFlag ? "License has expired." : "Trial period has expired.");

//			if (flag == ActivatedFlag)
//			{
//				var remaining = expirationDate - DateTime.UtcNow;
//				return (LicenseState.Activated, $"License active. Expires in {remaining.Days} days.");
//			}
//			else if (flag == TrialFlag)
//			{
//				var remaining = expirationDate - DateTime.UtcNow;
//				return (LicenseState.Trial, $"Trial period. {remaining.TotalHours:F1} hours remaining.");
//			}

//			return (LicenseState.Invalid, "Unknown license status.");
//		}

//		public static bool ValidateKey(string activationKey)
//		{
//			return ValidLicenseKeys.ContainsKey(activationKey);
//		}

//		public static bool ActivateLicense(string activationKey)
//		{
//			if (!ValidateKey(activationKey))
//				return false;

//			var duration = ValidLicenseKeys.GetDuration(activationKey);
//			var activationDate = DateTime.UtcNow;
//			var expirationDate = activationDate.Add(duration);

//			SaveLicenseInfo(GetMachineId(), activationDate, expirationDate, ActivatedFlag);
//			return true;
//		}
//	}
//}