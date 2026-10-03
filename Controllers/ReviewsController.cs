using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Controllers
{
    public class ReviewsController : Controller
    {
        public static string FilePath(System.Web.HttpServerUtilityBase server)
        {
            string folder = server.MapPath("~/App_Data");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "reviews.json");
        }

        public static List<Review> Load(string path)
        {
            if (!System.IO.File.Exists(path)) return new List<Review>();
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<List<Review>>(System.IO.File.ReadAllText(path)) ?? new List<Review>();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(int rating, string comment, string reviewerName)
        {
            if (Session["UserRole"]?.ToString() != "Customer")
                return RedirectToAction("Index", "Loads");

            comment = (comment ?? "").Trim();
            reviewerName = (reviewerName ?? "").Trim();
            if (rating < 1) rating = 1;
            if (rating > 5) rating = 5;
            if (comment.Length > 180) comment = comment.Substring(0, 180);
            if (reviewerName.Length > 60) reviewerName = reviewerName.Substring(0, 60);
            if (string.IsNullOrWhiteSpace(reviewerName)) reviewerName = "Anonymous";

            if (!string.IsNullOrWhiteSpace(comment))
            {
                string path = FilePath(Server);
                var reviews = Load(path);
                reviews.Insert(0, new Review
                {
                    ReviewId = reviews.Count + 1,
                    Rating = rating,
                    Comment = comment,
                    CustomerName = reviewerName,
                    DatePosted = DateTime.Now
                });
                var serializer = new JavaScriptSerializer();
                System.IO.File.WriteAllText(path, serializer.Serialize(reviews));
                TempData["SuccessMessage"] = "Thank you. Your review is now on the home page.";
            }

            return RedirectToAction("Index", "Home");
        }
    }
}