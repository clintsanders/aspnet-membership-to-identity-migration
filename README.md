# aspnet-membership-to-identity-migration
Sample project and steps to migrate ASP.NET Membership users to ASP.NET Core Identity

The project was created by executing the following command in the root project directory

  `dotnet new react --auth individual -uld`
  
## Add User Properties used in Membership store  
In ApplicationUser.cs added the following properties that are not included with IdentityUser object

```c#
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
```

## Identity Server SQL Database Setup
Update the connection string in AppSettings.json for the target database server
```json
"ConnectionStrings": {
  "IdentityConnection": "Server=INS15-CS\\SQLEXPRESS;Database=identity-users;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

In Startup.cs update the name of the connection string created in AppSettings.json in ConfigureServices()

```c#
services.AddDbContext<ApplicationDbContext>(options =>
  options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
```

Apply entity framework migrations to create tables used by Identity Server
From the package manager console execute the following command

`PM> update-database`

## Copy Users and Roles from Membership tables to Identity tables
Execute the following sql commands

```sql
-- INSERT USERS
INSERT INTO [aspnet-core_identity].dbo.AspNetUsers
(Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, 
	ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, 
	AccessFailedCount, LastActivityDate, LegacyPasswordHash, IsApproved, CreateDate, LastLoginDate, 
	LastPasswordChangedDate, LastLockoutDate, Comment)

SELECT aspnet_Users.UserId, aspnet_Users.UserName, UPPER(aspnet_Users.UserName), aspnet_Membership.Email, 
UPPER(aspnet_Membership.Email), 'true',	
(aspnet_Membership.Password + '|' + CAST(aspnet_Membership.PasswordFormat as varchar) + '|' + aspnet_Membership.PasswordSalt),	
NewID(), NewID(), NULL, 'false', 'false', aspnet_Membership.LastLockoutDate, aspnet_Membership.IsLockedOut,
0, aspnet_Users.LastActivityDate,	aspnet_Membership.Password, aspnet_Membership.IsApproved,
aspnet_Membership.CreateDate,	aspnet_Membership.LastLoginDate, aspnet_Membership.LastPasswordChangedDate,
aspnet_Membership.LastLockoutDate, aspnet_Membership.Comment
FROM aspnet_Users
LEFT OUTER JOIN aspnet_Membership ON aspnet_Users.UserId = aspnet_Membership.UserId

-- INSERT ROLES
INSERT INTO [aspnet-core_identity_test].dbo.AspNetRoles(Id, Name)
SELECT aspnet_Roles.RoleId, aspnet_Roles.RoleName
FROM aspnet_Roles;

-- INSERT USER ROLES
INSERT INTO [aspnet-core_identity_test].dbo.AspNetUserRoles(UserId, RoleId)
SELECT UserId, RoleId
FROM aspnet_UsersInRoles;
```

## Override password hasher for user authentication
Add the following class to the project

```c#
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
```

## Add role service configuration for Identity in Startup.cs

```c#
services.AddDefaultIdentity<ApplicationUser>()
		.AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();
```

## References Used
https://travis.io/blog/2015/03/24/migrate-from-aspnet-membership-to-aspnet-identity/

https://docs.microsoft.com/en-us/aspnet/identity/overview/migrations/migrating-an-existing-website-from-sql-membership-to-aspnet-identity



