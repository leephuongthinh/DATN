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
    
    public partial class Class
    {
        public System.Guid Id { get; set; }
        public Nullable<System.Guid> IdTrainingForm { get; set; }
        public Nullable<System.Guid> IdFaculty { get; set; }
        public string DisplayName { get; set; }
        public Nullable<System.Guid> IdTeacher { get; set; }
        public Nullable<bool> IsDeleted { get; set; }
        public Nullable<System.Guid> IdThumbnail { get; set; }
    
        public virtual Faculty Faculty { get; set; }
        public virtual Teacher Teacher { get; set; }
        public virtual DatabaseImageTable DatabaseImageTable { get; set; }
        public virtual TrainingForm TrainingForm { get; set; }
    }
}
