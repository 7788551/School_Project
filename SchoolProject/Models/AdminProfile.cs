using Microsoft.AspNetCore.Http;

namespace SchoolProject.Models
{
    public class AdminProfile
    {
        // ===== USERS TABLE =====
        public int UserId { get; set; }
        public string Name { get; set; } = null!;
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = null!;

        // ===== ADMIN TABLE =====
        public int AdminId { get; set; }

        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Designation { get; set; }

        public DateTime? JoiningDate { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }

        // ===== IMAGE =====
        public IFormFile? AdminImage { get; set; }          // new upload
        public string? ExistingAdminImage { get; set; }     // old image

        // ===== UI MODE =====
        public bool IsEditMode { get; set; }   // true = edit, false = view
    }
}

