using System;
using System.ComponentModel.DataAnnotations;

namespace SchoolProject.Models
{
    public class GalleryImage
    {
        [Key]
        public int GalleryImageId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(255)]
        public string ImagePath { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}

