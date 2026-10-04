using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Email;

/// <summary>
/// BL-454 fix round 3 — whether a mail is a RESET or an INVITATION was decided by one untested line: the service
/// method the caller picks. Measured here on the wire: each public send method, against a loopback SMTP endpoint that
/// records what reached it (nothing leaves the machine), carries the words of its own purpose.
/// </summary>
public sealed class TenantUserEmailFixRoundThreeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private const string Recipient = "ayse@ditenpharma.test";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Each_send_method_puts_its_own_purposes_words_on_the_wire(bool reset)
    {
        using var smtp = new LoopbackSmtp();
        var service = Service(smtp.Port);
        using var expected = await service.BuildMessageAsync(Recipient, "token-3", CancellationToken.None, isPasswordReset: reset);
        using var other = await service.BuildMessageAsync(Recipient, "token-3", CancellationToken.None, isPasswordReset: !reset);

        if (reset)
        {
            await service.SendTenantUserPasswordResetAsync(Recipient, "token-3", CancellationToken.None);
        }
        else
        {
            await service.SendTenantUserInvitationAsync(Recipient, "token-3", CancellationToken.None);
        }

        var wire = Assert.Single(smtp.Messages);
        Assert.Equal(expected.Subject, Subject(wire));
        Assert.NotEqual(other.Subject, Subject(wire));
        Assert.Contains(Normalize(expected.Body), Normalize(Decoded(wire)));
    }

    private static TenantUserInvitationEmailService Service(int port) => new(
        Options.Create(new SmtpOptions { Host = "127.0.0.1", Port = port, EnableSsl = false, FromEmail = "no-reply@di10.test" }),
        Options.Create(new PlatformServiceOptions { FrontendBaseUrl = "https://app.di10.test/" }),
        new Context(),
        new InMemoryUserRepository([new User(Recipient, "hash", "Ayşe", "Kaya", Tenant)]),
        new FixedIdentity(new TenantEmailIdentity("Diten Pharma", "tr", null, null)),
        NullLogger<TenantUserInvitationEmailService>.Instance);

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string Subject(string wire)
    {
        var lines = wire.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase));
        Assert.True(start >= 0, "No Subject header.");
        var value = new StringBuilder(lines[start]["Subject:".Length..]);
        for (var i = start + 1; i < lines.Length && lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]); i++)
        {
            value.Append(lines[i]);
        }

        return DecodeWords(value.ToString().Trim());
    }

    private static string DecodeWords(string header) =>
        Regex.Replace(Regex.Replace(header, @"\?=\s+=\?", "?==?"), @"=\?utf-8\?(?<enc>[BbQq])\?(?<text>[^?]*)\?=", match =>
            match.Groups["enc"].Value is "B" or "b"
                ? Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups["text"].Value))
                : QuotedPrintable(match.Groups["text"].Value.Replace('_', ' ')), RegexOptions.IgnoreCase);

    /// <summary>Every body part of the message, decoded (base64 or quoted-printable), one after another.</summary>
    private static string Decoded(string wire)
    {
        var text = new StringBuilder();
        var parts = Regex.Split(wire.Replace("\r\n", "\n"), @"\n--[^\n]+\n");
        foreach (var part in parts)
        {
            var split = part.IndexOf("\n\n", StringComparison.Ordinal);
            if (split < 0)
            {
                continue;
            }

            var headers = part[..split];
            var body = part[(split + 2)..];
            if (headers.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase))
            {
                var base64 = string.Concat(body.Split('\n').TakeWhile(line => line.Length > 0 && !line.StartsWith("--", StringComparison.Ordinal)));
                text.Append(Encoding.UTF8.GetString(Convert.FromBase64String(base64))).Append('\n');
            }
            else if (headers.Contains("Content-Transfer-Encoding: quoted-printable", StringComparison.OrdinalIgnoreCase))
            {
                text.Append(QuotedPrintable(body.Replace("=\n", string.Empty))).Append('\n');
            }
        }

        return text.ToString();
    }

    private static string QuotedPrintable(string value)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '=' && i + 2 < value.Length && Uri.IsHexDigit(value[i + 1]) && Uri.IsHexDigit(value[i + 2]))
            {
                bytes.Add(Convert.ToByte(value.Substring(i + 1, 2), 16));
                i += 2;
            }
            else
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(value[i].ToString()));
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private sealed class FixedIdentity(TenantEmailIdentity? answer) : ITenantEmailIdentityClient
    {
        public Task<TenantEmailIdentity?> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(answer);
    }

    private sealed class Context : ITenantContext
    {
        public Guid TenantId { get; set; } = Tenant;
        public bool IsResolved { get; set; } = true;
        public bool IsPlatformContext { get; set; }
        public Guid? TargetTenantId { get; set; }
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
        public void SetPlatformContext(Guid targetTenantId) => TargetTenantId = targetTenantId;
    }

    /// <summary>
    /// The smallest SMTP endpoint System.Net.Mail will talk to, on loopback only: it accepts one session at a time and
    /// keeps each message's DATA. No AUTH is offered, so no credential is ever sent to it.
    /// </summary>
    private sealed class LoopbackSmtp : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();

        public LoopbackSmtp()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(LoopAsync);
        }

        public int Port { get; }
        public List<string> Messages { get; } = [];

        private async Task LoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch
                {
                    return;
                }

                using (client)
                {
                    await ServeAsync(client.GetStream());
                }
            }
        }

        private async Task ServeAsync(NetworkStream stream)
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 loopback ESMTP");
            while (await reader.ReadLineAsync() is { } line)
            {
                var verb = line.Length >= 4 ? line[..4].ToUpperInvariant() : line.ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                    case "HELO":
                        await writer.WriteLineAsync("250 loopback");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 go ahead");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                        {
                            data.Append(dataLine.StartsWith("..", StringComparison.Ordinal) ? dataLine[1..] : dataLine).Append("\r\n");
                        }

                        lock (Messages)
                        {
                            Messages.Add(data.ToString());
                        }

                        await writer.WriteLineAsync("250 queued");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 bye");
                        return;
                    default:
                        await writer.WriteLineAsync("250 ok");
                        break;
                }
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }
}
