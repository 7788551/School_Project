using Microsoft.AspNetCore.Http;
using System;

namespace SchoolProject.Models
{
    public class EditTeacher
    {
        public int UserId { get; set; }

        // ============================
        // USERS TABLE
        // ============================
        public string Name { get; set; } = null!;
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = null!;

        // ============================
        // TEACHERS TABLE
        // ============================
        public int SessionId { get; set; }
        public int TeacherTypeId { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public DateTime? JoiningDate { get; set; }

        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }

        public string? Subject { get; set; }
        public string? Designation { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }

        // ============================
        // IMAGE HANDLING
        // ============================
        public string? ExistingTeacherImage { get; set; } // old image from DB
        public IFormFile? TeacherImage { get; set; }      // new uploaded image
    }
}
