using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;

if (args.Length != 1)
    throw new ArgumentException("Provide an absolute output directory outside the web root, e.g. .local-secrets/teams-join-pilot.");
string directory = Path.GetFullPath(args[0]);
if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
    throw new InvalidOperationException("Output directory must be empty; existing keys are never overwritten.");
Directory.CreateDirectory(directory);
if (OperatingSystem.IsWindows())
{
    string sid = WindowsIdentity.GetCurrent().User!.Value;
    var start = new ProcessStartInfo("icacls.exe") { UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (string argument in new[] { directory, "/inheritance:r", "/grant:r", $"*{sid}:(OI)(CI)F", "*S-1-5-18:(OI)(CI)F" })
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    process.WaitForExit();
    if (process.ExitCode != 0) throw new IOException("Could not restrict secret directory permissions.");
}
else
{
    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
using var rsa = RSA.Create(3072);
var request = new CertificateRequest("CN=Mzala Connect Teams Join Notifications", rsa,
    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment, true));
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
string stateKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
string pfxPath = Path.Combine(directory, "teams-join-notifications.pfx");
File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pkcs12, password));
File.WriteAllBytes(Path.Combine(directory, "teams-join-notifications.cer"), certificate.Export(X509ContentType.Cert));
var settings = new { TeamsJoinPilot = new {
    Enabled = true, NotificationUrl = "https://tutors.belgiumcampus.ac.za/api/teams/meeting-events",
    CertificatePath = pfxPath, CertificatePassword = password, ClientStateKey = stateKey
} };
File.WriteAllText(Path.Combine(directory, "local-settings.json"),
    JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(directory, "deployment.env"),
    "TeamsJoinPilot__Enabled=true\n" +
    "TeamsJoinPilot__NotificationUrl=https://tutors.belgiumcampus.ac.za/api/teams/meeting-events\n" +
    "TeamsJoinPilot__CertificatePath=/run/secrets/teams-join-notifications.pfx\n" +
    $"TeamsJoinPilot__CertificatePassword={password}\nTeamsJoinPilot__ClientStateKey={stateKey}\n");
if (!OperatingSystem.IsWindows())
    foreach (var file in Directory.EnumerateFiles(directory))
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
Console.WriteLine($"Created protected pilot files in {directory}");
Console.WriteLine($"Public certificate thumbprint: {certificate.Thumbprint}; expires {certificate.NotAfter:yyyy-MM-dd}.");
Console.WriteLine("Keep the PFX, local-settings.json and deployment.env private. No trust-store or website TLS certificates were changed.");
