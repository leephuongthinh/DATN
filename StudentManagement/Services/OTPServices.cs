using StudentManagement.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagement.Services
{
    public class OTPServices
    {
        private static OTPServices s_instance;

        public static OTPServices Instance => s_instance ?? (s_instance = new OTPServices());

        public OTPServices() { }

		public bool CheckGetOTPFromEmail(string email, string OTP)
		{
			var user = UserServices.Instance.GetUserByGmail(email).FirstOrDefault();
			if (user == null || user.IdOTP == null || user.OTP == null)
				return false;

			return user.OTP.Code.Equals(OTP);
		}

		public async Task SaveOTP(string email, string OTP)
		{
			var user = UserServices.Instance.GetUserByGmail(email).FirstOrDefault();
			if (user == null)
				return;

			var otp = new OTP()
			{
				Id = Guid.NewGuid(),
				Code = OTP,
				Time = DateTime.Now,
			};

			if (user.OTP != null)
			{
				// Nếu người dùng đã có OTP, xóa OTP cũ
				DataProvider.Instance.Database.OTPs.Remove(user.OTP);
			}

			DataProvider.Instance.Database.OTPs.Add(otp);
			user.OTP = otp;

			await DataProvider.Instance.Database.SaveChangesAsync();
		}

		public void DeleteOTPOverTime()
		{
			var listOTP = DataProvider.Instance.Database.OTPs
				.Where(otp => DbFunctions.DiffMinutes(otp.Time, DateTime.Now) > 5)
				.ToList();

			if (listOTP.Count == 0)
				return;

			foreach (var otp in listOTP)
			{
				DataProvider.Instance.Database.OTPs.Remove(otp);
				var user = UserServices.Instance.GetUserByOTP(otp);
				if (user != null)
				{
					user.IdOTP = null; // Xoá liên kết OTP với người dùng
				}
			}

			DataProvider.Instance.Database.SaveChanges();
		}

		//public void SendEmail(string toEmail, string subject, string body)
		//{
		//	try
		//	{
		//		var smtpClient = new SmtpClient("smtp.gmail.com")
		//		{
		//			Port = 587,
		//			Credentials = new NetworkCredential("ducduong.tektra@gmail.com", "29012020duong"),
		//			EnableSsl = true,
		//		};

		//		var mailMessage = new MailMessage
		//		{
		//			From = new MailAddress("ducduong.tektra@gmail.com"),
		//			Subject = subject,
		//			Body = body,
		//			IsBodyHtml = true,
		//		};

		//		mailMessage.To.Add(toEmail);

		//		smtpClient.Send(mailMessage);
		//	}
		//	catch (SmtpException ex)
		//	{
		//		Console.WriteLine($"SMTP Exception: {ex.Message}");
		//	}
		//}
	}
}
