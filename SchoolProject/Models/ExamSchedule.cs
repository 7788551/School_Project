using System;
using System.ComponentModel.DataAnnotations;

namespace SchoolProject.Models
{
    public class ExamSchedule
    {
        [Key]
        public int ScheduleId { get; set; }

        [Required]
        public int ExamId { get; set; }     // FK → Exams

        [Required]
        public int ClassId { get; set; }    // FK → Classes

        [Required]
        public int SubjectId { get; set; }  // FK → Subjects

        [Required]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
    }
}
