using System;
using System.ComponentModel.DataAnnotations;

namespace SchoolProject.Models
{
    public class Exam
    {
        [Key]
        public int ExamId { get; set; }

        [Required(ErrorMessage = "Session is required")]
        public int SessionId { get; set; }     // FK → AcademicSessions

        [Required(ErrorMessage = "Exam name is required")]
        [StringLength(100)]
        public string ExamName { get; set; } = string.Empty;

        // ✅ Derived from ExamSchedule (MIN(StartDate), MAX(EndDate))
        // Nullable because exam may exist before scheduling
        //public DateTime? StartDate { get; set; }
        //public DateTime? EndDate { get; set; }

        //public bool IsPublished { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
