namespace SchoolProject.Models
{
    public class AddStudent
    {
        public string Name { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;

        public int SessionId { get; set; }
        public int ClassId { get; set; }
        public int SectionId { get; set; }

        public string AdmissionNumber { get; set; } = null!;
        public int? RollNumber { get; set; }

        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = null!;
        public string FatherName { get; set; } = null!;
        public string? MotherName { get; set; }

        public string AddressLine1 { get; set; } = null!;
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }

        public IFormFile? StudentImage { get; set; }
        public string? StudentImageName { get; set; }
    }

}
