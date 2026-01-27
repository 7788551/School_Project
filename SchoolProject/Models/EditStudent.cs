using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SchoolProject.Models
{
    public class EditStudent
    {
        // =======================
        // USERS
        // =======================
        public int UserId { get; set; }

        // =======================
        // STUDENT PRIMARY KEY
        // =======================
        public int StudentId { get; set; }   // 🔥 REQUIRED for update

        // =======================
        // BASIC DETAILS
        // =======================
        [Required]
        public string Name { get; set; } = null!;

        [Required]
        public string PhoneNumber { get; set; } = null!;

        // =======================
        // SESSION (READ ONLY)
        // =======================
        public int SessionId { get; set; }
        public string SessionName { get; set; } = null!;

        // =======================
        // CLASS / SECTION
        // =======================
        public int ClassId { get; set; }
        public int SectionId { get; set; }

        // =======================
        // STUDENT DETAILS
        // =======================
        public string AdmissionNumber { get; set; } = null!;
        public string? RollNumber { get; set; }

        public DateTime DateOfBirth { get; set; }
        public string? Gender { get; set; }

        [Required]
        public string FatherName { get; set; } = null!;

        public string? MotherName { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }

        // =======================
        // 🔥 IMAGE HANDLING (FINAL)
        // =======================

        // Existing image filename from DB
        public string? ExistingStudentImage { get; set; }

        // New uploaded image (ONLY if user selects one)
        public IFormFile? StudentImageFile { get; set; }
    }
}
