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
    public class AdminServices
    {
        private static AdminServices s_instance;

        public static AdminServices Instance => s_instance ?? (s_instance = new AdminServices());

        public AdminServices() { }

        public Admin GetFirstStudent()
        {
            return DataProvider.Instance.Database.Admin.FirstOrDefault();
        }

        public DbSet<Admin> LoadAdminList()
        {
            return DataProvider.Instance.Database.Admin;
        }

        public Admin FindAdminByAdminId(Guid id)
        {
            Admin a = DataProvider.Instance.Database.Admin.Where(adminItem => adminItem.Id == id).FirstOrDefault();
            return a;
        }
		// Trong StudentServices
		public Student FindStudentByUserId(Guid userId)
		{
			return DataProvider.Instance.Database.Student.FirstOrDefault(s => s.IdUsers == userId);
		}

		// Trong TeacherServices
		public Teacher FindTeacherByUserId(Guid userId)
		{
			return DataProvider.Instance.Database.Teacher.FirstOrDefault(t => t.IdUsers == userId);
		}

		// Trong AdminServices
		public Admin FindAdminByUserId(Guid userId)
		{
			return DataProvider.Instance.Database.Admin.FirstOrDefault(a => a.IdUsers == userId);
		}
		public bool SaveAdminToDatabase(Admin admin)
        {
            try
            {
                Admin savedAdmin = FindAdminByAdminId(admin.Id);

                if (savedAdmin == null)
                {
                    DataProvider.Instance.Database.Admin.Add(admin);
                }
                else
                {
                    //savedFaculty = (faculty.ShallowCopy() as Faculty);
                    Reflection.CopyProperties(admin, savedAdmin);
                }
                DataProvider.Instance.Database.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }

        }

        public Admin GetAdminByUser(Users user)
        {
            return DataProvider.Instance.Database.Admin.FirstOrDefault(admin=>admin.IdUsers == user.Id);
        }
		public bool DeleteAdminById(Guid adminId)
		{
			try
			{
				var admin = FindAdminByAdminId(adminId);
				if (admin == null)
				{
					return false;
				}

				// Optionally, find and delete the associated Users record
				var user = DataProvider.Instance.Database.Users.FirstOrDefault(u => u.Id == admin.IdUsers);
				if (user != null)
				{
					DataProvider.Instance.Database.Users.Remove(user);
				}

				DataProvider.Instance.Database.Admin.Remove(admin);
				DataProvider.Instance.Database.SaveChanges();
				return true;
			}
			catch
			{
				return false;
			}
		}

		public bool DeleteAdminByUserId(Guid userId)
		{
			try
			{
				var admin = FindAdminByUserId(userId);
				if (admin == null)
				{
					return false;
				}

				// Optionally, delete the associated Users record
				var user = DataProvider.Instance.Database.Users.FirstOrDefault(u => u.Id == userId);
				if (user != null)
				{
					DataProvider.Instance.Database.Users.Remove(user);
				}

				DataProvider.Instance.Database.Admin.Remove(admin);
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
