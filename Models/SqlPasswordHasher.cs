using System;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;


namespace membership_migration.Models
{
	public class SqlPasswordHasher : IPasswordHasher<ApplicationUser>
	{
		//an instance of the default password hasher
		IPasswordHasher<ApplicationUser> _identityPasswordHasher = new PasswordHasher<ApplicationUser>();

		//Hashes the password using old algorithm from the days of ASP.NET Membership
		internal static string HashPasswordInOldFormat(string password)
		{
			var sha1 = new SHA1CryptoServiceProvider();
			var data = Encoding.ASCII.GetBytes(password);
			var sha1data = sha1.ComputeHash(data);
			return Convert.ToBase64String(sha1data);
		}

		//the passwords of the new users will be hashed with new algorithm
		public string HashPassword(ApplicationUser user, string password)
		{
			return _identityPasswordHasher.HashPassword(user, password);
		}

		public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
		{
			string[] passwordProperties = hashedPassword.Split('|');
			if (passwordProperties.Length != 3)
			{
				return _identityPasswordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
			}
			else
			{
				string passwordHash = passwordProperties[0];
				int passwordformat = 1;
				string salt = passwordProperties[2];
				if (String.Equals(EncryptPassword(providedPassword, passwordformat, salt), passwordHash, StringComparison.CurrentCultureIgnoreCase))
				{
					return PasswordVerificationResult.SuccessRehashNeeded;
				}
				else
				{
					return PasswordVerificationResult.Failed;
				}
			}
		}

		//This is copied from the existing SQL providers and is provided only for back-compat.
		private string EncryptPassword(string pass, int passwordFormat, string salt)
		{
			if (passwordFormat == 0) // MembershipPasswordFormat.Clear
				return pass;

			byte[] bIn = Encoding.Unicode.GetBytes(pass);
			byte[] bSalt = Convert.FromBase64String(salt);
			byte[] bRet = null;

			if (passwordFormat == 1)
			{
				// MembershipPasswordFormat.Hashed 
				HashAlgorithm hm = HashAlgorithm.Create("SHA1");
				if (hm is KeyedHashAlgorithm)
				{
					KeyedHashAlgorithm kha = (KeyedHashAlgorithm)hm;
					if (kha.Key.Length == bSalt.Length)
					{
						kha.Key = bSalt;
					}
					else if (kha.Key.Length < bSalt.Length)
					{
						byte[] bKey = new byte[kha.Key.Length];
						Buffer.BlockCopy(bSalt, 0, bKey, 0, bKey.Length);
						kha.Key = bKey;
					}
					else
					{
						byte[] bKey = new byte[kha.Key.Length];
						for (int iter = 0; iter < bKey.Length;)
						{
							int len = Math.Min(bSalt.Length, bKey.Length - iter);
							Buffer.BlockCopy(bSalt, 0, bKey, iter, len);
							iter += len;
						}
						kha.Key = bKey;
					}
					bRet = kha.ComputeHash(bIn);
				}
				else
				{
					byte[] bAll = new byte[bSalt.Length + bIn.Length];
					Buffer.BlockCopy(bSalt, 0, bAll, 0, bSalt.Length);
					Buffer.BlockCopy(bIn, 0, bAll, bSalt.Length, bIn.Length);
					bRet = hm.ComputeHash(bAll);
				}
			}

			return Convert.ToBase64String(bRet);
		}
	}
}
