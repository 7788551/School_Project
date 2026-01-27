using System.ComponentModel.DataAnnotations;

namespace SchoolProject.Models
{
    public class ExamSubject
    {
        [Key]
        public int ExamSubjectId { get; set; }

        [Required]
        public int ExamId { get; set; }     // FK → Exams

        [Required]
        public int ClassId { get; set; }    // FK → Classes

        [Required]
        public int SubjectId { get; set; }  // FK → Subjects

        [Required]
        [StringLength(20)]
        public string EvaluationType { get; set; } = "Marks";
        // Marks | Grade

        public decimal? MaxMarks { get; set; }
        public decimal? PassMarks { get; set; }
    }
}
