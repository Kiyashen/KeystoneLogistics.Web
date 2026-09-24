                
using System;
using System.IO;
using System.Web;

namespace KeystoneLogistics.Services
{
    public class PODService
    {
        /// <summary>
        /// Saves the uploaded file to ~/App_Data/Uploads/ and returns the relative virtual path.
        /// </summary>
        /// <param name="file">The uploaded file</param>
        /// <returns>Virtual path (e.g., "~/App_Data/Uploads/20230824_123456_myfile.pdf") or null if file is null.</returns>
        public string SavePODFile(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return null;

            // 1. Ensure the upload directory exists
            string uploadFolder = HttpContext.Current.Server.MapPath("~/App_Data/Uploads/");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            // 2. Generate a unique filename to avoid collisions
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string originalExtension = Path.GetExtension(file.FileName);
            string safeFileName = $"{timestamp}_{Guid.NewGuid().ToString().Substring(0, 8)}{originalExtension}";

            // 3. Full physical path
            string physicalPath = Path.Combine(uploadFolder, safeFileName);

            // 4. Save the file
            file.SaveAs(physicalPath);

            // 5. Return the virtual path (used to store in DB)
            return $"~/App_Data/Uploads/{safeFileName}";
        }
    }
}