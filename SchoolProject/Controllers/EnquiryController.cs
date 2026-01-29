using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;

public class EnquiryController : Controller
{
    private readonly IConfiguration _configuration;

    public EnquiryController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SqlConnection GetConnection()
    {
        return new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));
    }
    [HttpPost]
    [AllowAnonymous]
    public IActionResult Submit(Enquiry model)
    {
        using SqlConnection con = GetConnection();

        string query = @"
        INSERT INTO dbo.Enquiries
        (FirstName, LastName, PhoneNumber, Email, Message, CreatedDate, IsRead)
        VALUES
        (@FirstName, @LastName, @PhoneNumber, @Email, @Message, @CreatedDate, @IsRead)";

        using SqlCommand cmd = new SqlCommand(query, con);

        cmd.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = model.FirstName;
        cmd.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = model.LastName;
        cmd.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 20).Value = model.PhoneNumber;
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = model.Email;
        cmd.Parameters.Add("@Message", SqlDbType.NVarChar).Value = model.Message;
        cmd.Parameters.Add("@CreatedDate", SqlDbType.DateTime).Value = DateTime.Now;
        cmd.Parameters.Add("@IsRead", SqlDbType.Bit).Value = false;

        con.Open();
        cmd.ExecuteNonQuery();

        TempData["Success"] = "Your enquiry has been submitted successfully!";
        return RedirectToAction("Contact", "Home");
    }

}
