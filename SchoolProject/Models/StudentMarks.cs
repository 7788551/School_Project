using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace SchoolProject.Models
{
    public class StudentMarks
    {
        // 🔑 Primary Key
        [Key]
        public int StudentMarkId { get; set; }

        // 🔗 Foreign Keys
        [Required]
        public int ExamId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int SubjectId { get; set; }

        public int? GradeId { get; set; }   // Subject-wise grade (optional)

        // 💯 Marks
        [Column(TypeName = "decimal(5,2)")]
        public decimal? MarksObtained { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MaxMarks { get; set; }

        // 🚫 Absent Flag
        public bool IsAbsent { get; set; }

        // 🧭 Navigation Properties
        public Exam Exam { get; set; }
        public Student Student { get; set; }
        public Class Class { get; set; }
        public Subject Subject { get; set; }
        public GradeMaster? Grade { get; set; }
    }
}
