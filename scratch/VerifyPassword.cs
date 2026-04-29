using BCrypt.Net;

var hash = "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC";
string[] commonPasswords = { "Admin123!", "123456", "password", "P@ssword123", "Admin123", "diten", "diten123" };

foreach (var pwd in commonPasswords)
{
    if (BCrypt.Net.BCrypt.Verify(pwd, hash))
    {
        Console.WriteLine($"FOUND: {pwd}");
        return;
    }
}
Console.WriteLine("NOT FOUND");
