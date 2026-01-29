using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace SchoolProject.Controllers
{
    [Authorize]
    public class AdminGalleryController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public AdminGalleryController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        // ---------------- LIST ----------------
        public IActionResult Showdata()
        {
            List<GalleryImage> list = new();

            using SqlConnection con = GetConnection();
            SqlCommand cmd = new("SELECT * FROM GalleryImages ORDER BY CreatedDate DESC", con);

            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new GalleryImage
                {
                    GalleryImageId = Convert.ToInt32(reader["GalleryImageId"]),
                    Title = reader["Title"].ToString(),
                    ImagePath = reader["ImagePath"].ToString(),
                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                });
            }

            return View(list);
        }

        // ---------------- CREATE (GET) ----------------
        [HttpGet]
        public IActionResult AddGalleryImage()
        {
            return View();
        }

        // ---------------- CREATE (POST) ----------------
        [HttpPost]
        public IActionResult AddGalleryImage(GalleryImage model, IFormFile ImageFile)
        {
            try
            {
                if (ImageFile == null || ImageFile.Length == 0)
                    return View(model);

                string folder = Path.Combine(_env.WebRootPath, "uploads/gallery");
                Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                using SqlConnection con = GetConnection();
                SqlCommand cmd = new(@"
                    INSERT INTO GalleryImages (Title, ImagePath)
                    VALUES (@Title, @ImagePath)", con);

                cmd.Parameters.AddWithValue("@Title", model.Title);
                cmd.Parameters.AddWithValue("@ImagePath", fileName);

                con.Open();
                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "Image uploaded successfully.";
                return RedirectToAction("Showdata");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(model);
            }
        }

        // ---------------- DELETE ----------------
        [HttpPost]
        public IActionResult Delete(int id)
        {
            try
            {
                string oldImage = null;

                using SqlConnection con = GetConnection();

                // get old image name
                SqlCommand getCmd = new(
                    "SELECT ImagePath FROM GalleryImages WHERE GalleryImageId=@id", con);
                getCmd.Parameters.AddWithValue("@id", id);

                con.Open();
                oldImage = getCmd.ExecuteScalar()?.ToString();
                con.Close();

                // delete db record
                SqlCommand delCmd = new(
                    "DELETE FROM GalleryImages WHERE GalleryImageId=@id", con);
                delCmd.Parameters.AddWithValue("@id", id);

                con.Open();
                delCmd.ExecuteNonQuery();

                // delete physical file
                if (!string.IsNullOrEmpty(oldImage))
                {
                    string path = Path.Combine(_env.WebRootPath, "uploads/gallery", oldImage);
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                return RedirectToAction("Showdata");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Showdata");
            }
        }

        // ---------------- EDIT (GET) ----------------
        [HttpGet]
        public IActionResult EditGalleryImage(int id)
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            GalleryImageId,
            Title,
            ImagePath
        FROM GalleryImages
        WHERE GalleryImageId = @Id
    ", con);

            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return NotFound();

            return View(new GalleryImage
            {
                GalleryImageId = dr.GetInt32(0),
                Title = dr.GetString(1),
                ImagePath = dr.IsDBNull(2) ? null : dr.GetString(2)
            });
        }

        // ---------------- EDIT (POST) ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditGalleryImage(GalleryImage model, IFormFile ImageFile)
        {
            if (model.GalleryImageId <= 0)
            {
                TempData["Error"] = "Invalid gallery record";
                return RedirectToAction("Showdata");
            }

            string finalImageName = model.ImagePath;

            // ============================
            // IMAGE UPDATE LOGIC (SAME AS STUDENT)
            // ============================
            if (ImageFile != null && ImageFile.Length > 0)
            {
                string uploadFolder = Path.Combine(
                    _env.WebRootPath,
                    "uploads",
                    "gallery"
                );

                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                // delete OLD image only if NEW uploaded
                if (!string.IsNullOrEmpty(model.ImagePath))
                {
                    string oldPath = Path.Combine(uploadFolder, model.ImagePath);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // keep original name safely
                string originalName = Path.GetFileName(ImageFile.FileName);
                string safeName = originalName.Replace(" ", "_");

                string newPath = Path.Combine(uploadFolder, safeName);

                // avoid overwrite
                if (System.IO.File.Exists(newPath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    safeName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    newPath = Path.Combine(uploadFolder, safeName);
                }

                using var stream = new FileStream(newPath, FileMode.Create);
                ImageFile.CopyTo(stream);

                finalImageName = safeName;
            }

            using SqlConnection con = GetConnection();
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                using SqlCommand cmd = new SqlCommand(@"
            UPDATE GalleryImages
            SET 
                Title = @Title,
                ImagePath = @ImagePath
            WHERE GalleryImageId = @Id
        ", con, tran);

                cmd.Parameters.AddWithValue("@Id", model.GalleryImageId);
                cmd.Parameters.AddWithValue("@Title", model.Title);
                cmd.Parameters.AddWithValue("@ImagePath", (object?)finalImageName ?? DBNull.Value);

                cmd.ExecuteNonQuery();

                tran.Commit();
                TempData["SuccessMessage"] = "Gallery image updated successfully.";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Gallery update failed.";
                return View(model);
            }

            return RedirectToAction("Showdata");
        }

    }
}
