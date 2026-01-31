using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace SchoolProject.Models
{
    public class TeacherProfile
    {
        // ===== USER TABLE =====
        public int UserId { get; set; }

        [Required]
        public string Name { get; set; } = null!;

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = null!;


        // ===== TEACHER TABLE =====
        public int TeacherId { get; set; }

        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string? Qualification { get; set; }

        public int? ExperienceYears { get; set; }

        public string? Designation { get; set; }

        public string? Subject { get; set; }

        [DataType(DataType.Date)]
        public DateTime? JoiningDate { get; set; }

        public string? AddressLine1 { get; set; }

        public string? AddressLine2 { get; set; }

        public string? City { get; set; }

        public string? State { get; set; }

        public string? Pincode { get; set; }


        // ===== IMAGE HANDLING =====
        public string? ExistingTeacherImage { get; set; }

        public IFormFile? TeacherImage { get; set; }


        // ===== UI STATE =====
        public bool IsEditMode { get; set; }
    }
}
