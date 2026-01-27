//using System.ComponentModel.DataAnnotations;

//public class AddTeacher
//{
//    internal int UserId;

//    [Required]
//    public int SessionId { get; set; }

//    [Required]
//    public int TeacherTypeId { get; set; }


//    [Required]
//    public string Name { get; set; } = null!;

//    [Required]
//    public DateTime DateOfBirth { get; set; }

//    [Required]
//    [EmailAddress]
//    public string Email { get; set; } = null!;

//    public string? Qualification { get; set; }
//    public int? ExperienceYears { get; set; }
//    public DateTime? JoiningDate { get; set; }
//    public string? Designation { get; set; }
//    public string? Subject { get; set; }

//    [Required]
//    public string PhoneNumber { get; set; } = null!;

//    [Required]
//    public string Gender { get; set; } = null!;

//    public string? AddressLine1 { get; set; }
//    public string? AddressLine2 { get; set; }
//    public string? City { get; set; }
//    public string? State { get; set; }
//    public string? Pincode { get; set; }
//}

using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

public class AddTeacher
{
    // Internal use (captured after Users insert)
    internal int UserId;

    // ======================
    // Session & Type
    // ======================
    [Required(ErrorMessage = "Session is required")]
    public int SessionId { get; set; }

    [Required(ErrorMessage = "Teacher type is required")]
    public int TeacherTypeId { get; set; }

    // ======================
    // Basic Details
    // ======================
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Date of birth is required")]
    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Phone number is required")]
    public string PhoneNumber { get; set; } = null!;

    [Required(ErrorMessage = "Gender is required")]
    public string Gender { get; set; } = null!;

    // ======================
    // Academic & Job Details
    // ======================
    public string? Qualification { get; set; }
    public int? ExperienceYears { get; set; }
    public DateTime? JoiningDate { get; set; }
    public string? Designation { get; set; }
    public string? Subject { get; set; }

    // ======================
    // Address
    // ======================
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }

    // ======================
    // Teacher Image
    // ======================
    public IFormFile? TeacherImage { get; set; }   // for upload
    public string? TeacherImagePath { get; set; }  // optional: for display/edit
}
