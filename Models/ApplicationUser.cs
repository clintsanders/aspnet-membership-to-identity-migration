using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace membership_migration.Models
{
    public class ApplicationUser : IdentityUser
    {
		public bool IsApproved { get; set; }
		public DateTime LastActivityDate { get; set; }
		public string LegacyPasswordHash { get; set; }
		public DateTime CreateDate { get; set; }
		public DateTime LastLoginDate { get; set; }
		public DateTime LastPasswordChangedDate { get; set; }
		public DateTime LastLockoutDate { get; set; }
		public int FailedPasswordAttemptCount { get; set; }
		public DateTime FailedPasswordAttemptWindowStart { get; set; }
		public int FailedPasswordAnswerAttemptCount { get; set; }
		public DateTime FailedPasswordAnswerAttemptWindowStart { get; set; }
		public string Comment { get; set; }

		public ApplicationUser()
		{

		}
	}
}
