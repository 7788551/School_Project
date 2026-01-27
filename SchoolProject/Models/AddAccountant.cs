using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SchoolProject.Models
{
    public class AddAccountant
    {
        [Required]
        public int SessionId { get; set; }

        [Required]
        public string Name { get; set; } = null!;

        [Required]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public DateTime? JoiningDate { get; set; }
        public string? Designation { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        public string Gender { get; set; } = null!;

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }

        // ✅ NEW: Accountant Profile Image
        public IFormFile? AccountantImage { get; set; }
    }
}
