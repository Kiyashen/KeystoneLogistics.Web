using System.ComponentModel.DataAnnotations;

namespace KeystoneLogistics.Models
{
    [MetadataType(typeof(LoadMetadata))]
    public partial class Load
    {
    }

    public class LoadMetadata
    {
        [Required(ErrorMessage = "Tracking number is required.")]
        [Display(Name = "Tracking Number")]
        public string TrackingNumber { get; set; }

        [Required(ErrorMessage = "Pickup location is required.")]
        [Display(Name = "Pickup Location")]
        public string PickupLocation { get; set; }

        [Required(ErrorMessage = "Drop-off location is required.")]
        [Display(Name = "Drop-off Location")]
        public string DropoffLocation { get; set; }

        [Required(ErrorMessage = "Cargo description is required.")]
        [Display(Name = "Cargo Description")]
        public string CargoDescription { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [Display(Name = "Status")]
        public string Status { get; set; }
    }
}
