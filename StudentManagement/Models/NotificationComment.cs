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
    
    public partial class NotificationComment
    {
        public System.Guid Id { get; set; }
        public Nullable<System.Guid> IdUserComment { get; set; }
        public Nullable<System.Guid> IdNotification { get; set; }
        public string Content { get; set; }
        public Nullable<System.DateTime> Time { get; set; }
    
        public virtual Notification Notification { get; set; }
        public virtual Users Users { get; set; }
    }
}
