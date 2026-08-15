using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

using Models;

namespace DbModels
{
    [Table("Users", Schema = "dbo")]
    public class UserDbM : User
	{
        [Key]     
        public override Guid UserId { get; set; }

        [Required]
        public override string UserName { get; set; }

        [Required]
        public override string PasswordHash { get; set; }

        [Required]
        public override string UserRole { get; set; }
    }
}

