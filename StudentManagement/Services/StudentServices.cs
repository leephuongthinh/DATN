using StudentManagement.Models;
using StudentManagement.Objects;
using StudentManagement.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagement.Services
{
	public class StudentServices
	{
		private static StudentServices s_instance;

		public static StudentServices Instance => s_instance ?? (s_instance = new StudentServices());

		public StudentServices() { }

		#region Convert
		public StudentGrid ConvertStudentToStudentGrid(Student student, int number = 0)
		{
			return new StudentGrid()
			{
				Id = student.Id,
				Number = number,
				DisplayName = student.User.DisplayName,
				Email = student.User.Email,
				Faculty = student.Faculty.DisplayName,
				TrainingForm = student.TrainingForm.DisplayName,
				Username = student.User.Username,
				Status = student.User.Online == true ? "Trực tuyến" : "Ngoại tuyến"
			};
		}
		public Student GetStudentbyUsername(string username)
		{
			var user = DataProvider.Instance.Database.Users
				.FirstOrDefault(u => u.Username == username);

			if (user == null)
			{
				return null;
			}

			return DataProvider.Instance.Database.Students
				.FirstOrDefault(s => s.IdUsers == user.Id);
		}


		#endregion


		public Student GetFirstStudent()
		{
			return DataProvider.Instance.Database.Students.FirstOrDefault();
		}

		public DbSet<Student> LoadStudentList()
		{
			return DataProvider.Instance.Database.Students;
		}

		public Student FindStudentByStudentId(Guid idStudent)
		{
			Student a = DataProvider.Instance.Database.Students.Where(studentItem => studentItem.Id == idStudent).FirstOrDefault();
			return a;
		}

		public Student FindStudentByUserId(Guid idUser)
		{
			Student a = DataProvider.Instance.Database.Students.Where(studentItem => studentItem.IdUsers == idUser).FirstOrDefault();
			return a;
		}

		public bool SaveStudentToDatabase(Student student)
		{
			try
			{
				Student savedStudent = FindStudentByStudentId(student.Id);

				if (savedStudent == null)
				{
					DataProvider.Instance.Database.Students.Add(student);
				}
				else
				{
					//savedFaculty = (faculty.ShallowCopy() as Faculty);
					Reflection.CopyProperties(student, savedStudent);
				}
				DataProvider.Instance.Database.SaveChanges();
				return true;
			}
			catch
			{
				return false;
			}

		}
		public Student GetStudentbyUser(User user)
		{
			if (user == null)
			{
				return null;
			}

			return DataProvider.Instance.Database.Students
				.FirstOrDefault(student => student.Username == user.Username);
		}

		//public Student GetStudentbyUser(User user)
		//{
		//	if (user == null)
		//	{
		//		// Có thể log lỗi hoặc xử lý logic phù hợp
		//		return null;
		//	}

		//	return DataProvider.Instance.Database.Students
		//		.FirstOrDefault(student => student.IdUsers == user.Id);
		//}
		public List<Student> GetStudentsByUser(User user)
		{
			if (user == null)
				throw new ArgumentNullException(nameof(user));

			var students = DataProvider.Instance?.Database?.Students;

			if (students == null)
				throw new InvalidOperationException("Students DbSet is not available.");

			return students
				.Where(student => student.IdUsers == user.Id)
				.ToList();
		}
		public Student GetStudentByUser(User user)
		{
			return GetStudentsByUser(user).FirstOrDefault();
		}

		internal StudentGrid ConvertStudentToStudentGrid(Student student)
		{
			throw new NotImplementedException();
		}
	}
}
