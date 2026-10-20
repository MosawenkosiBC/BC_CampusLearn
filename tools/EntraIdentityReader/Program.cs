using BC_CampusLearn.Data;
using BC_CampusLearn.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (args.Length != 2 || !int.TryParse(args[0], out int userId) || userId <= 0 ||
    !Path.IsPathFullyQualified(args[1]) || !Directory.Exists(args[1]) ||
    !Directory.EnumerateFiles(args[1], "key-*.xml").Any())
{
    Console.Error.WriteLine("Supply a positive BcUserId and the absolute existing Data Protection key directory.");
    return 1;
}

string? connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connection))
{
    Console.Error.WriteLine("Set ConnectionStrings__DefaultConnection to the target database connection string.");
    return 1;
}

try
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
    await using var context = new ApplicationDbContext(options);
    var user = await context.BcUsers.AsNoTracking().Where(u => u.BcUserId == userId)
        .Select(u => new { u.Email, u.EncryptedEntraObjectId }).SingleOrDefaultAsync();
    if (user is null || string.IsNullOrWhiteSpace(user.EncryptedEntraObjectId))
    {
        Console.Error.WriteLine("The user was not found or has no stored Microsoft Object ID.");
        return 1;
    }

    var services = new ServiceCollection();
    services.AddDataProtection().SetApplicationName("BC_CampusLearn")
        .PersistKeysToFileSystem(new DirectoryInfo(args[1])).DisableAutomaticKeyGeneration();
    using var provider = services.BuildServiceProvider();
    var protector = provider.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("BC_CampusLearn.EntraIdentity.v1");
    string objectId = EntraIdentityProtector.UsesReadableIdentity(user.Email) &&
        Guid.TryParse(user.EncryptedEntraObjectId, out _)
            ? user.EncryptedEntraObjectId : protector.Unprotect(user.EncryptedEntraObjectId);
    if (!Guid.TryParse(objectId, out Guid id) || id == Guid.Empty)
        throw new InvalidOperationException();

    Console.WriteLine($"BcUserId: {userId}");
    Console.WriteLine($"Email: {user.Email}");
    Console.WriteLine($"Microsoft Entra Object ID: {id:D}");
    Console.WriteLine("Use this ID for the shared organiser only if the email is mzalaconnect@belgiumcampus.ac.za.");
    return 0;
}
catch
{
    Console.Error.WriteLine("Recovery failed. Check database connectivity and the original Data Protection keys and their access permissions. No database values were changed.");
    return 1;
}
