using System.Globalization;
using System.Text;
using Diten.Platform.Domain.Entities.ESignature;

namespace Diten.Platform.Application.Features.ESignature.Services;

public static class InternalEvidenceDocumentGenerator
{
    public static byte[] Generate(
        SignatureEnvelope envelope,
        SignatureParticipant participant,
        SignerAttestation attestation)
    {
        var lines = new[]
        {
            "INTERNAL APPROVAL EVIDENCE",
            "This document is not a provider-verified or qualified electronic signature.",
            $"Envelope: {envelope.EnvelopeNumber}",
            $"Subject: {envelope.SubjectType}/{envelope.SubjectId}",
            $"Subject version: {envelope.SubjectVersion}",
            $"Source artifact: {envelope.DocumentArtifactId}",
            $"Source SHA-256: {envelope.SourceArtifactHash}",
            $"Signer: {participant.SignerDisplayName} <{participant.SignerEmail}>",
            $"Attested at UTC: {attestation.AttestedAt.ToString("O", CultureInfo.InvariantCulture)}",
            $"Policy: {attestation.PolicyCode}",
            $"Correlation: {attestation.CorrelationId}",
            $"Attestation: {attestation.AttestationText}"
        };

        return BuildSimplePdf(lines);
    }

    private static byte[] BuildSimplePdf(IEnumerable<string> lines)
    {
        var escapedLines = lines.Select(EscapePdfText).ToArray();
        var content = new StringBuilder("BT /F1 10 Tf 50 780 Td 14 TL ");
        foreach (var line in escapedLines)
        {
            content.Append('(').Append(line).Append(") Tj T* ");
        }
        content.Append("ET");

        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
            output.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(output.ToString());
        output.Append("xref\n0 ").Append(objects.Length + 1).Append("\n");
        output.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            output.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        output.Append("trailer << /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\n");
        output.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string EscapePdfText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Select(character => character <= 127 ? character : '?')
            .Aggregate(new StringBuilder(), (builder, character) => builder.Append(character))
            .ToString();
}
