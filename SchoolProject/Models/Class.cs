using System.ComponentModel.DataAnnotations;

namespace SchoolProject.Models
{
    public class Class
    {
        [Key]
        public int ClassId { get; set; }

        [Required]
        [StringLength(50)]
        public string ClassName { get; set; } = string.Empty;

        public int ClassOrder { get; set; }
    }
}
