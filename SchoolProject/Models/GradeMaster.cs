using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolProject.Models
{
    public class GradeMaster
    {
        // 🔑 Primary Key
        [Key]
        public int GradeId { get; set; }

        // 🏷 Grade (A+, A, B, C)
        [Required]
        [StringLength(5)]
        public string Grade { get; set; } = string.Empty;

        // 📊 Percentage Range
        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MinPercentage { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal MaxPercentage { get; set; }

        // ✅ Active Flag
        public bool IsActive { get; set; } = true;
    }
}
