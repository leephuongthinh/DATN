﻿using StudentManagement.Models;
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
    }
}
