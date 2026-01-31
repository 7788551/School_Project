using Microsoft.AspNetCore.Http;
using System;

namespace SchoolProject.Models
{
    public class AccountantProfile
    {
        public int UserId { get; set; }
        public int AccountantId { get; set; }

        // User table
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public string PhoneNumber { get; set; } = "";

        // Accountant table
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

        // Image
        public IFormFile? AccountantImage { get; set; }
        public string? ExistingAccountantImage { get; set; }

        public bool IsEditMode { get; set; }
    }
}
