using System.ComponentModel.DataAnnotations;
using UpKeep.Contracts.Properties;

namespace UpKeep.Contracts.Properties
{
    public class CreatePropertyRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;
    }
}
