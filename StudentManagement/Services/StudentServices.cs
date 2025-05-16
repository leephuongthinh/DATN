using StudentManagement.Models;
using StudentManagement.Objects;
using StudentManagement.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
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
				DisplayName = student.Users.DisplayName,
				Email = student.Users.Email,
				Faculty = student.Faculty.DisplayName,
				TrainingForm = student.TrainingForm.DisplayName,
				Username = student.Users.Username,
				Status = student.Users.Online == true ? "Trực tuyến" : "Ngoại tuyến"
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

			return DataProvider.Instance.Database.Student
				.FirstOrDefault(s => s.IdUsers == user.Id);
		}


		#endregion


		public Student GetFirstStudent()
		{
			return DataProvider.Instance.Database.Student.FirstOrDefault();
		}

		public DbSet<Student> LoadStudentList()
		{
			return DataProvider.Instance.Database.Student;
		}

		public Student FindStudentByStudentId(Guid idStudent)
		{
			Student a = DataProvider.Instance.Database.Student.Where(studentItem => studentItem.Id == idStudent).FirstOrDefault();
			return a;
		}

		public Student FindStudentByUserId(Guid idUser)
		{
			Student a = DataProvider.Instance.Database.Student.Where(studentItem => studentItem.IdUsers == idUser).FirstOrDefault();
			return a;
		}

		public bool SaveStudentToDatabase(Student student)
		{
			try
			{
				Student savedStudent = FindStudentByStudentId(student.Id);

				if (savedStudent == null)
				{
					DataProvider.Instance.Database.Student.Add(student);
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
		public Student GetStudentbyUser(Users user)
		{

			return DataProvider.Instance.Database.Student.FirstOrDefault(student => student.IdUsers == user.Id);                                           
		}


		public List<Student> GetStudentsByUser(Users user)
		{
			if (user == null)
				throw new ArgumentNullException(nameof(user));

			var students = DataProvider.Instance?.Database?.Student;

			if (students == null)
				throw new InvalidOperationException("Students DbSet is not available.");

			return students
				.Where(student => student.IdUsers == user.Id)
				.ToList();
		}
		public Student GetStudentByUser(Users user)
		{
			return GetStudentsByUser(user).FirstOrDefault();
		}

		internal StudentGrid ConvertStudentToStudentGrid(Student student)
		{
			throw new NotImplementedException();
		}
		public bool DeleteStudentByUserId(Guid userId)
		{
			try
			{
				var student = DataProvider.Instance.Database.Student.FirstOrDefault(s => s.IdUsers == userId);
				if (student == null)
				{
					return false;
				}

				// Tùy chọn: Xóa bản ghi Users liên quan
				var user = DataProvider.Instance.Database.Users.FirstOrDefault(u => u.Id == userId);
				if (user != null)
				{
					DataProvider.Instance.Database.Users.Remove(user);
				}

				DataProvider.Instance.Database.Student.Remove(student);
				DataProvider.Instance.Database.SaveChanges();
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
