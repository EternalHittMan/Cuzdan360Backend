using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cuzdan360Backend.Models;

namespace Cuzdan360Backend.Models.Finance
{
    public class UserAsset
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int AssetTypeId { get; set; }
        [ForeignKey("AssetTypeId")]
        public AssetType? AssetType { get; set; }

        // The quantity of the asset (e.g. 10.5 Grams of Gold, 100 USD)
        [Column(TypeName = "decimal(18,8)")]
        public decimal Amount { get; set; }
    }
}
