using System.ComponentModel.DataAnnotations;

namespace KeystoneLogistics.Models
{
    [MetadataType(typeof(DriverMetadata))]
    public partial class Driver
    {
    }

    public class DriverMetadata
    {
        [Required(ErrorMessage = "Driver name is required.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vehicle registration is required.")]
        [Display(Name = "Vehicle Registration")]
        public string VehicleRegistration { get; set; }

        [Display(Name = "Available")]
        public bool? IsAvailable { get; set; }
    }
}
