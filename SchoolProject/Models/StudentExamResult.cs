using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace SchoolProject.Models
{
    public class StudentExamResult
    {
        // 🔑 Primary Key
        [Key]
        public int StudentExamResultId { get; set; }

        // 🔗 Foreign Keys
        [Required]
        public int ExamId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int GradeId { get; set; }   // Overall grade

        // 📊 Totals
        [Required]
        [Column(TypeName = "decimal(7,2)")]
        public decimal TotalMarksObtained { get; set; }

        [Required]
        [Column(TypeName = "decimal(7,2)")]
        public decimal TotalMaxMarks { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Percentage { get; set; }

        // 📌 Result Status (Pass / Fail / Absent)
        [Required]
        [StringLength(10)]
        public string ResultStatus { get; set; } = string.Empty;

        // 🧭 Navigation Properties
        public Exam Exam { get; set; }
        public Student Student { get; set; }
        public Class Class { get; set; }
        public GradeMaster Grade { get; set; }
    }
}
