//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace StudentManagement.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Users
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Users()
        {
            this.Admin = new HashSet<Admin>();
            this.Document = new HashSet<Document>();
            this.Folder = new HashSet<Folder>();
            this.Notification = new HashSet<Notification>();
            this.Notification1 = new HashSet<Notification>();
            this.NotificationComment = new HashSet<NotificationComment>();
            this.NotificationInfo = new HashSet<NotificationInfo>();
            this.Student = new HashSet<Student>();
            this.Teacher = new HashSet<Teacher>();
            this.User_UserRole_UserInfo = new HashSet<User_UserRole_UserInfo>();
        }
		private System.Guid _id { get; set; }
		public System.Guid Id { get; set; }
		private string _username { get; set; }
		public string Username { get; set; }
		private string _password { get; set; }
        public string Password { get; set; }
		private string _displayName { get; set; }
		public string DisplayName { get; set; }
		private string _email { get; set; }
		public string Email { get; set; }
        public Nullable<System.Guid> IdOTP { get; set; }
        public Nullable<bool> Online { get; set; }
        public Nullable<System.Guid> IdUserRole { get; set; }
		public bool IsDeleted { get; set; } = false;

		public Nullable<System.Guid> IdAvatar { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Admin> Admin { get; set; }
        public virtual DatabaseImageTable DatabaseImageTable { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Document> Document { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Folder> Folder { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Notification> Notification { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Notification> Notification1 { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<NotificationComment> NotificationComment { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<NotificationInfo> NotificationInfo { get; set; }
        public virtual OTP OTP { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Student> Student { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Teacher> Teacher { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<User_UserRole_UserInfo> User_UserRole_UserInfo { get; set; }
        public virtual UserRole UserRole { get; set; }
    }
}
